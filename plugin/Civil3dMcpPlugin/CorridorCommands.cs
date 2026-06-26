using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

public static class CorridorCommands
{
  public static Task<object?> ListCorridorsAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var corridors = new List<object>();

      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var c = tr.GetObject(id, OpenMode.ForRead) as Corridor;
        if (c == null) continue;

        corridors.Add(new
        {
          name = c.Name,
          handle = c.Handle.ToString(),
          baselineCount = c.Baselines?.Count ?? 0,
          layer = c.Layer
        });
      }

      return new { corridors };
    });
  }

  public static Task<object?> GetCorridorAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var c = FindByName(tr, db, name);

      return new
      {
        name = c.Name,
        handle = c.Handle.ToString(),
        layer = c.Layer,
        baselineCount = c.Baselines?.Count ?? 0
      };
    });
  }

  public static Task<object?> RebuildCorridorAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var c = FindByName(tr, db, name);

      c.UpgradeOpen();
      c.Rebuild();

      return new { success = true, rebuilt = name };
    });
  }

  // ---------------- FIX PRINCIPAL ----------------

  private static Corridor FindByName(Transaction tr, Database db, string name)
  {
    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

    foreach (ObjectId id in ms)
    {
      var c = tr.GetObject(id, OpenMode.ForRead) as Corridor;

      if (c != null &&
          string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
      {
        return c;
      }
    }

    throw new JsonRpcDispatchException(
      "CIVIL3D.NOT_FOUND",
      $"Corridor '{name}' not found."
    );
  }

  public static Task<object?> GetCorridorSurfacesAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });

  public static Task<object?> GetCorridorFeatureLinesAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });

  public static Task<object?> ComputeCorridorVolumesAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });
}