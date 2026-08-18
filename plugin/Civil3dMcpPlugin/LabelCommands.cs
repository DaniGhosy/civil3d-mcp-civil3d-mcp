using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Labels (Módulo B — lectura de planos: texto y anotaciones): extracts DBText/MText,
/// Leader/MLeader, and Dimension entities straight from the drawing.
/// </summary>
public static class LabelCommands
{
  public static Task<object?> ExtractTextEntitiesAsync(JsonObject? parameters)
  {
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var modelSpace = ModelSpace(tr, db);
      var entities = new List<object>();

      foreach (ObjectId id in modelSpace)
      {
        var obj = tr.GetObject(id, OpenMode.ForRead);

        if (obj is DBText text)
        {
          if (layer != null && !string.Equals(text.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;

          entities.Add(new
          {
            handle = text.Handle.ToString(),
            entityType = "DBText",
            text = text.TextString,
            position = new { x = text.Position.X, y = text.Position.Y, z = text.Position.Z },
            height = text.Height,
            layer = text.Layer,
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
      var modelSpace = ModelSpace(tr, db);
      var leaders = new List<object>();

      foreach (ObjectId id in modelSpace)
      {
        var obj = tr.GetObject(id, OpenMode.ForRead);

        if (obj is Leader leader)
        {
          string? text = null;
          if (!leader.Annotation.IsNull)
          {
            if (tr.GetObject(leader.Annotation, OpenMode.ForRead) is MText annotationText)
            {
              text = annotationText.Contents;
            }
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
      var modelSpace = ModelSpace(tr, db);
      var dimensions = new List<object>();

      foreach (ObjectId id in modelSpace)
      {
        var obj = tr.GetObject(id, OpenMode.ForRead);

        if (obj is Dimension dimension)
        {
          if (layer != null && !string.Equals(dimension.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;

          dimensions.Add(new
          {
            handle = dimension.Handle.ToString(),
            dimensionType = dimension.GetType().Name,
            measurement = dimension.Measurement,
            dimensionText = dimension.DimensionText,
            textPosition = new { x = dimension.TextPosition.X, y = dimension.TextPosition.Y, z = dimension.TextPosition.Z },
            layer = dimension.Layer,
          });
        }
      }

      return new { layer, dimensions };
    });
  }

  private static BlockTableRecord ModelSpace(Transaction tr, Database db)
  {
    var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    return (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
  }
}
