using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Producción de planos (Mes 7): crear ViewFrame/ViewFrameGroup/Sheets/MatchLine
/// vía API .NET está confirmado como NO soportado por Autodesk (foro oficial:
/// "The ability to create ViewFrames and Sheets has not been exposed in the
/// .NET API"; hay un pedido de feature abierto y sin resolver pidiendo
/// exactamente esto). Solo se ofrece lectura de objetos ya creados vía UI,
/// mismo patrón de escaneo de ModelSpace que FeatureLine/ProfileView.
/// </summary>
public static class SheetProductionCommands
{
  public static Task<object?> ListViewFramesAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var viewFrames = new List<object>();
      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var vf = tr.GetObject(id, OpenMode.ForRead) as ViewFrame;
        if (vf == null) continue;

        viewFrames.Add(new
        {
          name = vf.Name,
          handle = vf.Handle.ToString(),
          properties = GenericObjectCommands.SerializeSimpleProperties(vf),
        });
      }

      return new { viewFrames };
    });
  }

  public static Task<object?> ListMatchLinesAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var matchLines = new List<object>();
      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var ml = tr.GetObject(id, OpenMode.ForRead) as MatchLine;
        if (ml == null) continue;

        matchLines.Add(new
        {
          handle = ml.Handle.ToString(),
          properties = GenericObjectCommands.SerializeSimpleProperties(ml),
        });
      }

      return new { matchLines };
    });
  }
}
