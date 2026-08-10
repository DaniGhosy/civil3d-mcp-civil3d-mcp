using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Base primitives for Civil 3D Grading (Mes 2 scope, new domain): feature lines
/// are real (ModelSpace-scan, same pattern as CorridorCommands/AssemblyCommands
/// via CivilObjectLookup). Grading Groups are entirely stubbed: the compiler
/// confirmed neither `Site.GetGradingGroupIds()` nor a `GradingGroup` type exist
/// under those names in Autodesk.Civil.DatabaseServices, so the real accessor
/// path needs to be confirmed against a live Civil 3D session before writing it
/// for real — see the list/get/delete/create Grading Group stubs below.
///
/// Grading criteria sets/criteria (talud/relleno) are out of scope for this
/// round entirely — see the Mes 2 plan.
/// </summary>
public static class GradingCommands
{
  // ─────────────────────────────────────────────
  // Grading Groups: el intento inicial (Site.GetGradingGroupIds() + tipo
  // GradingGroup) no compiló — ninguno de los dos existe con ese nombre en el
  // API. Se deja todo el sub-dominio como stub explícito en vez de seguir
  // adivinando nombres de miembro/tipo.
  // ─────────────────────────────────────────────
  public static Task<object?> ListGradingGroupsAsync()
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Grading group listing needs its real accessor confirmed against a live Civil 3D drawing " +
             "(Site.GetGradingGroupIds() and a GradingGroup type, the initial guesses, do not exist with those names)."
    });

  public static Task<object?> GetGradingGroupAsync(JsonObject? parameters)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Grading group lookup needs its real accessor confirmed against a live Civil 3D drawing."
    });

  public static Task<object?> DeleteGradingGroupAsync(JsonObject? parameters)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Grading group deletion needs its real accessor confirmed against a live Civil 3D drawing."
    });

  public static Task<object?> CreateGradingGroupAsync(JsonObject? parameters)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Grading group creation needs its exact factory signature verified against a live Civil 3D drawing."
    });

  public static Task<object?> ListFeatureLinesAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var featureLines = new List<object>();

      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var fl = tr.GetObject(id, OpenMode.ForRead) as FeatureLine;
        if (fl == null) continue;

        featureLines.Add(new
        {
          name = fl.Name,
          handle = fl.Handle.ToString(),
          layer = fl.Layer,
        });
      }

      return new { featureLines };
    });
  }

  public static Task<object?> GetFeatureLineAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var fl = CivilObjectLookup.FindEntityByName<FeatureLine>(tr, db, name);

      return new
      {
        name = fl.Name,
        handle = fl.Handle.ToString(),
        layer = fl.Layer,
      };
    });
  }

  public static Task<object?> DeleteFeatureLineAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var fl = CivilObjectLookup.FindEntityByName<FeatureLine>(tr, db, name);
      fl.UpgradeOpen();
      fl.Erase();

      return new { success = true, deleted = name };
    });
  }

  // ─────────────────────────────────────────────
  // Crear una feature line a partir de puntos/alineamiento: FeatureLine es un
  // Entity como los demás objetos Civil3D, pero su factory de creación no está
  // confirmado. Se deja como stub explícito en vez de adivinar la firma.
  // ─────────────────────────────────────────────
  public static Task<object?> CreateFeatureLineAsync(JsonObject? parameters)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Feature line creation needs its exact factory signature verified against a live Civil 3D drawing."
    });
}
