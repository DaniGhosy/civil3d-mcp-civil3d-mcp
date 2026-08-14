using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Módulo C del catálogo de lectura de planos ("simbología y leyenda") — solo la mitad que
/// necesita el dibujo vivo: leer la tabla de leyenda como datos crudos (filas de celdas de
/// texto). Cruzar esa leyenda contra los bloques reales del dibujo (build_symbol_dictionary),
/// compararla (compare_legend_vs_drawing) y persistirla como JSON local
/// (export_symbol_library/import_symbol_library) es post-proceso puro y vive en
/// src/tools/domains/legendDomain.ts, sin volver a llamar al plugin — mismo patrón que
/// civil3d_quantity (ver CLAUDE.md).
///
/// No hay heurística de "cuál tabla es la leyenda" acá a propósito: se devuelven TODAS las
/// tablas del dibujo (o una puntual por handle) como datos crudos, y quien llama decide cuál es
/// la leyenda — es más fácil elegir bien mirando el contenido real que adivinar por posición o
/// tamaño.
/// </summary>
public static class LegendCommands
{
  public static Task<object?> ReadLegendTableAsync(JsonObject? parameters)
  {
    var handleString = PluginRuntime.GetOptionalString(parameters, "handle");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      if (handleString != null)
      {
        var id = GenericObjectCommands.ResolveHandle(db, handleString);
        if (tr.GetObject(id, OpenMode.ForRead) is not Table table)
          throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Object '{handleString}' is not a Table.");

        return new { tables = new[] { SerializeTable(table) } };
      }

      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      var tables = new List<object>();
      foreach (ObjectId entId in ms)
      {
        if (tr.GetObject(entId, OpenMode.ForRead) is Table t)
        {
          tables.Add(SerializeTable(t));
        }
      }

      return new { tables };
    });
  }

  private static object SerializeTable(Table table)
  {
    var rows = new List<List<string?>>();
    for (var row = 0; row < table.Rows.Count; row++)
    {
      var rowCells = new List<string?>();
      for (var col = 0; col < table.Columns.Count; col++)
      {
        rowCells.Add(table.Cells[row, col].TextString);
      }
      rows.Add(rowCells);
    }

    return new
    {
      handle = table.Handle.ToString(),
      layer = table.Layer,
      rowCount = table.Rows.Count,
      columnCount = table.Columns.Count,
      rows,
    };
  }
}
