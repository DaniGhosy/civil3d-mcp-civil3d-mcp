using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using App = Autodesk.AutoCAD.ApplicationServices.Application;
using AcDbObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

[assembly: CommandClass(typeof(Civil3DMcpPlugin.PressureNetworkBulkImportCommand))]

namespace Civil3DMcpPlugin;

/// <summary>
/// Bulk pressure-network import — reads a JSON file (the exact shape
/// io_epanet/civil3d_export.py's construir_plan_importacion_local already produces) and loops
/// PressurePipeCommands.AddPressurePipeToNetwork / AddPressureComponentToNetwork /
/// FindPressurePartsListId — the SAME methods the MCP add_pipe/add_fitting/add_appurtenance/
/// create_network handlers call, widened from private to internal for this reuse. No creation
/// logic is duplicated; RunImport below is the single shared core, called from two entry points:
///
/// 1. EPANETIMPORT — a native AutoCAD command (loaded via the same NETLOAD as the rest of the
///    plugin, typed directly at the Civil3D command line, prompts for a file path). Bypasses the
///    MCP/JSON-RPC protocol entirely — built for the case that motivated this file: a real network
///    (302 pipes) means 300+ individually-approved MCP calls, not viable token-wise.
/// 2. "importPressureNetworkFromFile" — a plugin method registered in CommandDispatcher.cs, so it
///    IS reachable over MCP after all: as an ordinary civil3d_pressure_pipe-style call, or (better
///    for a large network, given the default 120s CIVIL3D_COMMAND_TIMEOUT) via
///    civil3d_job.start({operation: "importPressureNetworkFromFile", parameters: {filePath}}) —
///    the job system already generalizes to "any registered dispatcher method", so no separate
///    job-specific wiring was needed beyond this dispatcher entry.
///
/// Both entry points report per-item success/failure without aborting the whole batch on one bad
/// row; RunImport itself never commits or aborts the transaction — that stays the caller's
/// responsibility, since the two entry points manage their transaction lifecycle differently
/// (EPANETIMPORT owns a manual using-scoped Transaction; the MCP path runs inside
/// CivilExecution.WriteAsync, which commits automatically after RunImport returns, or rolls back
/// automatically if RunImport throws).
/// </summary>
public class PressureNetworkBulkImportCommand
{
  private sealed record Point3DDto(double X, double Y, double Z)
  {
    public Point3d ToPoint3d() => new(X, Y, Z);
  }

  private sealed record PipeDto(Point3DDto Start, Point3DDto End, string PartName);
  private sealed record FittingDto(Point3DDto Position, string PartName);
  private sealed record AppurtenanceDto(Point3DDto Position, string PartName);

  private sealed record ImportPlan(
    string NetworkName,
    string PartsList,
    List<PipeDto>? Pipes,
    List<FittingDto>? Fittings,
    List<AppurtenanceDto>? Appurtenances);

  private static readonly JsonSerializerOptions DeserializeOptions = new()
  {
    PropertyNameCaseInsensitive = true,
  };

  // -------------------------------------------------------------------------
  // EPANETIMPORT (native AutoCAD command, outside MCP)
  // -------------------------------------------------------------------------

  [CommandMethod("EPANETIMPORT")]
  public void EpanetImport()
  {
    var doc = App.DocumentManager.MdiActiveDocument;
    if (doc == null)
    {
      return;
    }

    var ed = doc.Editor;

    var pathResult = ed.GetString("\nEPANETIMPORT - path to import JSON file: ");
    if (pathResult.Status != PromptStatus.OK)
    {
      return;
    }

    var path = pathResult.StringResult.Trim().Trim('"');
    if (!File.Exists(path))
    {
      ed.WriteMessage($"\nEPANETIMPORT: file not found: {path}\n");
      return;
    }

    ImportPlan? plan;
    try
    {
      var json = File.ReadAllText(path);
      plan = JsonSerializer.Deserialize<ImportPlan>(json, DeserializeOptions);
    }
    catch (System.Exception ex)
    {
      ed.WriteMessage($"\nEPANETIMPORT: could not read/parse '{path}' — {ex.Message}\n");
      return;
    }

    if (plan == null || string.IsNullOrWhiteSpace(plan.NetworkName) || string.IsNullOrWhiteSpace(plan.PartsList))
    {
      ed.WriteMessage("\nEPANETIMPORT: JSON must include non-empty 'networkName' and 'partsList'. Nothing was created.\n");
      return;
    }

    var db = doc.Database;
    var civilDoc = CivilApplication.ActiveDocument;

    using var docLock = doc.LockDocument();
    using var transaction = db.TransactionManager.StartTransaction();

    Dictionary<string, object?> summary;
    try
    {
      summary = RunImport(civilDoc, db, transaction, plan);
    }
    catch (System.Exception ex)
    {
      ed.WriteMessage(
        $"\nEPANETIMPORT: could not create network '{plan.NetworkName}' with parts list '{plan.PartsList}' — {ex.Message}\n" +
        "Nothing was created.\n");
      return;
    }

    transaction.Commit();
    WriteSummaryToEditor(ed, summary, path);
  }

  private static void WriteSummaryToEditor(Editor ed, Dictionary<string, object?> summary, string sourcePath)
  {
    var pipes = (Dictionary<string, object?>)summary["pipes"]!;
    var fittings = (Dictionary<string, object?>)summary["fittings"]!;
    var appurtenances = (Dictionary<string, object?>)summary["appurtenances"]!;
    var totalFailed = (int)summary["totalFailed"]!;

    ed.WriteMessage(
      $"\nEPANETIMPORT complete — network '{summary["networkName"]}' (parts list '{summary["partsList"]}'):\n" +
      $"  Pipes:         {pipes["ok"]} ok, {pipes["failed"]} failed (of {pipes["total"]})\n" +
      $"  Fittings:      {fittings["ok"]} ok, {fittings["failed"]} failed (of {fittings["total"]})\n" +
      $"  Appurtenances: {appurtenances["ok"]} ok, {appurtenances["failed"]} failed (of {appurtenances["total"]})\n");

    if (totalFailed == 0)
    {
      ed.WriteMessage("  No failures.\n");
      return;
    }

    var allFailures = ((List<string>)pipes["failures"]!)
      .Concat((List<string>)fittings["failures"]!)
      .Concat((List<string>)appurtenances["failures"]!)
      .ToList();

    try
    {
      var reportPath = Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? ".",
        Path.GetFileNameWithoutExtension(sourcePath) + "_errors.txt");
      File.WriteAllLines(reportPath, allFailures);
      ed.WriteMessage($"  {totalFailed} failure(s) — written to: {reportPath}\n");
    }
    catch (System.Exception ex)
    {
      ed.WriteMessage($"  {totalFailed} failure(s) — could not write error report ({ex.Message}), listing here:\n");
      foreach (var failure in allFailures)
      {
        ed.WriteMessage($"    {failure}\n");
      }
    }
  }

  // -------------------------------------------------------------------------
  // importPressureNetworkFromFile (MCP-reachable — plain call or via civil3d_job)
  // -------------------------------------------------------------------------

  public static Task<object?> ImportPressureNetworkFromFileAsync(JsonObject? parameters)
  {
    var filePath = PluginRuntime.GetRequiredString(parameters, "filePath");
    if (!File.Exists(filePath))
    {
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"File not found: {filePath}");
    }

    ImportPlan plan;
    try
    {
      var json = File.ReadAllText(filePath);
      plan = JsonSerializer.Deserialize<ImportPlan>(json, DeserializeOptions)
        ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Empty or invalid import JSON file.");
    }
    catch (JsonRpcDispatchException)
    {
      throw;
    }
    catch (System.Exception ex)
    {
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Could not read/parse '{filePath}': {ex.Message}");
    }

    if (string.IsNullOrWhiteSpace(plan.NetworkName) || string.IsNullOrWhiteSpace(plan.PartsList))
    {
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "JSON must include non-empty 'networkName' and 'partsList'.");
    }

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
      (object?)RunImport(civilDoc, database, transaction, plan));
  }

  // -------------------------------------------------------------------------
  // Shared core — never commits or aborts; that stays the caller's responsibility.
  // -------------------------------------------------------------------------

  private static Dictionary<string, object?> RunImport(object civilDoc, Database db, Transaction transaction, ImportPlan plan)
  {
    var networkId = PressurePipeNetwork.Create(db, plan.NetworkName);
    var network = CivilObjectUtils.GetRequiredObject<AcDbObject>(transaction, networkId, OpenMode.ForWrite);
    var partsListId = PressurePipeCommands.FindPressurePartsListId(civilDoc, transaction, plan.PartsList);
    ((PressurePipeNetwork)network).PartsListId = partsListId;

    var pipeFailures = new List<string>();
    var pipeOk = 0;
    var pipes = plan.Pipes ?? new List<PipeDto>();
    for (var i = 0; i < pipes.Count; i++)
    {
      var item = pipes[i];
      try
      {
        PressurePipeCommands.AddPressurePipeToNetwork(
          network, transaction, item.PartName, item.Start.ToPoint3d(), item.End.ToPoint3d(), diameter: null);
        pipeOk++;
      }
      catch (System.Exception ex)
      {
        pipeFailures.Add($"pipe[{i}] partName='{item.PartName}': {ex.Message}");
      }
    }

    var fittingFailures = new List<string>();
    var fittingOk = 0;
    var fittings = plan.Fittings ?? new List<FittingDto>();
    for (var i = 0; i < fittings.Count; i++)
    {
      var item = fittings[i];
      try
      {
        PressurePipeCommands.AddPressureComponentToNetwork(
          network, transaction, item.PartName, item.Position.ToPoint3d(), rotation: 0.0, isFitting: true);
        fittingOk++;
      }
      catch (System.Exception ex)
      {
        fittingFailures.Add($"fitting[{i}] partName='{item.PartName}': {ex.Message}");
      }
    }

    var appurtenanceFailures = new List<string>();
    var appurtenanceOk = 0;
    var appurtenances = plan.Appurtenances ?? new List<AppurtenanceDto>();
    for (var i = 0; i < appurtenances.Count; i++)
    {
      var item = appurtenances[i];
      try
      {
        PressurePipeCommands.AddPressureComponentToNetwork(
          network, transaction, item.PartName, item.Position.ToPoint3d(), rotation: 0.0, isFitting: false);
        appurtenanceOk++;
      }
      catch (System.Exception ex)
      {
        appurtenanceFailures.Add($"appurtenance[{i}] partName='{item.PartName}': {ex.Message}");
      }
    }

    return new Dictionary<string, object?>
    {
      ["networkName"] = plan.NetworkName,
      ["partsList"] = plan.PartsList,
      ["pipes"] = new Dictionary<string, object?> { ["ok"] = pipeOk, ["failed"] = pipeFailures.Count, ["total"] = pipes.Count, ["failures"] = pipeFailures },
      ["fittings"] = new Dictionary<string, object?> { ["ok"] = fittingOk, ["failed"] = fittingFailures.Count, ["total"] = fittings.Count, ["failures"] = fittingFailures },
      ["appurtenances"] = new Dictionary<string, object?> { ["ok"] = appurtenanceOk, ["failed"] = appurtenanceFailures.Count, ["total"] = appurtenances.Count, ["failures"] = appurtenanceFailures },
      ["totalFailed"] = pipeFailures.Count + fittingFailures.Count + appurtenanceFailures.Count,
    };
  }
}
