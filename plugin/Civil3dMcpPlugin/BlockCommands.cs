using System.Linq;
using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Módulo A del catálogo de lectura de planos ("inventario de bloques"): consulta directa a la
/// tabla de bloques del dibujo, sin interpretación de geometría — resultado exacto. Usa
/// BlockTableRecord.GetBlockReferenceIds(directOnly, forceOpenOnLockedLayer) para resolver
/// inserciones de un bloque específico, en vez de escanear ModelSpace y comparar nombres a mano
/// (ese idiom, aceptado como deuda técnica en otros 7 archivos, no aplica aquí porque hay un
/// método dedicado más preciso para "todas las inserciones de ESTE bloque").
/// </summary>
public static class BlockCommands
{
  public static Task<object?> ListBlockDefinitionsAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var blocks = new List<object>();

      foreach (ObjectId btrId in bt)
      {
        var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
        if (btr.IsAnonymous || btr.IsLayout) continue;

        blocks.Add(new
        {
          name = btr.Name,
          insertionCount = btr.GetBlockReferenceIds(true, false).Count,
          isDynamicBlock = btr.IsDynamicBlock,
        });
      }

      return new { blocks };
    });
  }

  public static Task<object?> CountBlocksByNameAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var layoutFilter = PluginRuntime.GetOptionalString(parameters, "layout");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var record = ResolveBlockDefinition(tr, db, name);
      var ids = record.GetBlockReferenceIds(true, false);

      var count = 0;
      foreach (ObjectId id in ids)
      {
        if (layoutFilter != null && !MatchesLayout(tr, id, layoutFilter)) continue;
        count++;
      }

      return new { name, layout = layoutFilter, count };
    });
  }

  public static Task<object?> GetBlockAttributesAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var record = ResolveBlockDefinition(tr, db, name);
      var instances = new List<object>();

      foreach (ObjectId id in record.GetBlockReferenceIds(true, false))
      {
        var reference = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
        var attributes = new Dictionary<string, string?>();

        foreach (ObjectId attId in reference.AttributeCollection)
        {
          if (tr.GetObject(attId, OpenMode.ForRead) is not AttributeReference attRef) continue;
          attributes[attRef.Tag] = attRef.TextString;
        }

        instances.Add(new { handle = reference.Handle.ToString(), attributes });
      }

      return new { name, instances };
    });
  }

  public static Task<object?> ListBlocksByLayerAsync(JsonObject? parameters)
  {
    var layer = PluginRuntime.GetRequiredString(parameters, "layer");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

      foreach (ObjectId id in ms)
      {
        if (tr.GetObject(id, OpenMode.ForRead) is not BlockReference reference) continue;
        if (!string.Equals(reference.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;

        var effectiveName = EffectiveBlockName(tr, reference);
        counts[effectiveName] = counts.TryGetValue(effectiveName, out var existing) ? existing + 1 : 1;
      }

      return new
      {
        layer,
        blocks = counts.Select(kv => new { name = kv.Key, insertionCount = kv.Value }).ToList(),
      };
    });
  }

  public static Task<object?> GetBlockInsertionPointsAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var record = ResolveBlockDefinition(tr, db, name);
      var points = new List<object>();

      foreach (ObjectId id in record.GetBlockReferenceIds(true, false))
      {
        var reference = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
        points.Add(new
        {
          handle = reference.Handle.ToString(),
          position = new { x = reference.Position.X, y = reference.Position.Y, z = reference.Position.Z },
          rotation = reference.Rotation,
          layer = reference.Layer,
        });
      }

      return new { name, points };
    });
  }

  // DynamicBlockReferencePropertyCollection / DynamicBlockReferenceProperty.PropertyName / .Value son API
  // documentada de Autodesk.AutoCAD.DatabaseServices (no el caso "incierto" de CLAUDE.md) — se devuelven
  // TODAS las propiedades dinámicas de cada inserción en vez de asumir que la de visibilidad se llama
  // siempre "Visibility1" (el nombre lo define quien creó el bloque).
  public static Task<object?> ListDynamicBlockStatesAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var record = ResolveBlockDefinition(tr, db, name);
      var instances = new List<object>();

      foreach (ObjectId id in record.GetBlockReferenceIds(true, false))
      {
        var reference = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
        if (!reference.IsDynamicBlock) continue;

        var properties = new List<object>();
        foreach (DynamicBlockReferenceProperty prop in reference.DynamicBlockReferencePropertyCollection)
        {
          properties.Add(new { propertyName = prop.PropertyName, value = prop.Value?.ToString() });
        }

        instances.Add(new { handle = reference.Handle.ToString(), properties });
      }

      return new { name, instances };
    });
  }

  // ── Helpers ──

  private static BlockTableRecord ResolveBlockDefinition(Transaction tr, Database db, string name)
  {
    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    if (!bt.Has(name))
      throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"Block definition '{name}' not found.");

    return (BlockTableRecord)tr.GetObject(bt[name], OpenMode.ForRead);
  }

  private static string EffectiveBlockName(Transaction tr, BlockReference reference)
  {
    if (!reference.IsDynamicBlock) return reference.Name;

    var dynamicBtr = (BlockTableRecord)tr.GetObject(reference.DynamicBlockTableRecord, OpenMode.ForRead);
    return dynamicBtr.Name;
  }

  private static bool MatchesLayout(Transaction tr, ObjectId blockReferenceId, string layoutName)
  {
    var reference = (BlockReference)tr.GetObject(blockReferenceId, OpenMode.ForRead);
    var ownerBtr = (BlockTableRecord)tr.GetObject(reference.OwnerId, OpenMode.ForRead);
    if (ownerBtr.LayoutId.IsNull) return false;

    var layout = (Layout)tr.GetObject(ownerBtr.LayoutId, OpenMode.ForRead);
    return string.Equals(layout.LayoutName, layoutName, StringComparison.OrdinalIgnoreCase);
  }
}
