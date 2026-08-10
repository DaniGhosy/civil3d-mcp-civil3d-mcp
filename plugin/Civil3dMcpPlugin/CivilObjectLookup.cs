using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Shared primitive for finding an object by its Name property. Generalizes the
/// near-identical FindByName helpers that used to be duplicated across
/// CorridorCommands/AssemblyCommands (Mes 2) and later Alignment/Profile/Surface/
/// PipeNetwork/Point (Mes 9 consolidación) — each of those used a different source
/// collection (ModelSpace scan, civilDoc.GetXxxIds(), an alignment's profile ids,
/// etc.), so the generic overload takes any ObjectId source instead of assuming
/// ModelSpace, keeping each caller's original scoping intact.
/// </summary>
public static class CivilObjectLookup
{
  public static T FindByName<T>(IEnumerable<ObjectId> ids, Transaction tr, string name) where T : class
  {
    foreach (var id in ids)
    {
      var candidate = tr.GetObject(id, OpenMode.ForRead) as T;
      if (candidate == null) continue;

      var nameProp = typeof(T).GetProperty("Name");
      var candidateName = nameProp?.GetValue(candidate) as string;

      if (candidateName != null && string.Equals(candidateName, name, StringComparison.OrdinalIgnoreCase))
      {
        return candidate;
      }
    }

    throw new JsonRpcDispatchException(
      "CIVIL3D.NOT_FOUND",
      $"{typeof(T).Name} '{name}' not found."
    );
  }

  public static T FindEntityByName<T>(Transaction tr, Database db, string name) where T : Entity
  {
    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

    return FindByName<T>(ms.Cast<ObjectId>(), tr, name);
  }
}
