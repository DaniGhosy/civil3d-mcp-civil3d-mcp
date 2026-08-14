using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Módulo B del catálogo de lectura de planos ("texto y anotaciones"): complementa a
/// BlockCommands.cs leyendo etiquetas, leaders y cotas del plano. Mismo idiom de escaneo de
/// ModelSpace que BlockCommands.ListBlocksByLayerAsync (no hay un GetXxxIds() equivalente para
/// texto/leaders/cotas sueltos, a diferencia de los bloques).
///
/// MLeader.MText (contenido de texto de un multileader) es API incierta — no confirmada contra
/// una sesión de Civil3D en vivo. Sigue el protocolo de CLAUDE.md: mejor intento, una sola pasada
/// de dotnet build, stub documentado si el compilador la rechaza.
/// </summary>
public static class LabelCommands
{
  public static Task<object?> ExtractTextEntitiesAsync(JsonObject? parameters)
  {
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var ms = ModelSpace(tr, db);
      var entities = new List<object>();

      foreach (ObjectId id in ms)
      {
        var obj = tr.GetObject(id, OpenMode.ForRead);

        if (obj is DBText dbText)
        {
          if (layer != null && !string.Equals(dbText.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;
          entities.Add(new
          {
            handle = dbText.Handle.ToString(),
            entityType = "DBText",
            text = dbText.TextString,
            position = new { x = dbText.Position.X, y = dbText.Position.Y, z = dbText.Position.Z },
            height = dbText.Height,
            layer = dbText.Layer,
          });
        }
        else if (obj is MText mtext)
        {
          if (layer != null && !string.Equals(mtext.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;
          entities.Add(new
          {
            handle = mtext.Handle.ToString(),
            entityType = "MText",
            text = mtext.Contents,
            position = new { x = mtext.Location.X, y = mtext.Location.Y, z = mtext.Location.Z },
            height = mtext.TextHeight,
            layer = mtext.Layer,
          });
        }
      }

      return new { layer, entities };
    });
  }

  public static Task<object?> ExtractLeaderAnnotationsAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var ms = ModelSpace(tr, db);
      var leaders = new List<object>();

      foreach (ObjectId id in ms)
      {
        var obj = tr.GetObject(id, OpenMode.ForRead);

        if (obj is Leader leader)
        {
          string? text = null;
          if (!leader.Annotation.IsNull && tr.GetObject(leader.Annotation, OpenMode.ForRead) is MText annotationText)
          {
            text = annotationText.Contents;
          }

          leaders.Add(new
          {
            handle = leader.Handle.ToString(),
            entityType = "Leader",
            text,
            layer = leader.Layer,
          });
        }
        else if (obj is MLeader mleader)
        {
          leaders.Add(new
          {
            handle = mleader.Handle.ToString(),
            entityType = "MLeader",
            text = mleader.MText?.Contents,
            layer = mleader.Layer,
          });
        }
      }

      return new { leaders };
    });
  }

  public static Task<object?> ExtractDimensionsAsync(JsonObject? parameters)
  {
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var ms = ModelSpace(tr, db);
      var dimensions = new List<object>();

      foreach (ObjectId id in ms)
      {
        if (tr.GetObject(id, OpenMode.ForRead) is not Dimension dim) continue;
        if (layer != null && !string.Equals(dim.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;

        dimensions.Add(new
        {
          handle = dim.Handle.ToString(),
          dimensionType = dim.GetType().Name,
          measurement = dim.Measurement,
          dimensionText = dim.DimensionText,
          textPosition = new { x = dim.TextPosition.X, y = dim.TextPosition.Y, z = dim.TextPosition.Z },
          layer = dim.Layer,
        });
      }

      return new { layer, dimensions };
    });
  }

  // ── Helpers ──

  private static BlockTableRecord ModelSpace(Transaction tr, Database db)
  {
    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
  }
}
