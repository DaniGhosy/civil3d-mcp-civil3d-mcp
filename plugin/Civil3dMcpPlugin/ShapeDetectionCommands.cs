using System.Linq;
using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Civil3DMcpPlugin;

/// <summary>
/// Módulo D del catálogo de lectura de planos ("geometría cruda / heurística"): para cuando el
/// símbolo se dibujó a mano con líneas y círculos sueltos, sin insertar un bloque. Acá ya no hay
/// exactitud garantizada — geometría analítica pura, sin visión por computadora. Complementa a
/// classify_geometry_by_signature (post-proceso puro en TS, ver shapeDetectionDomain.ts).
///
/// get_entity_extended_data usa DBObject.XData/GetXDataForApplication(string) — investigado contra
/// documentación oficial de Autodesk (Assign and Retrieve Extended Data, .NET) y el AutoCAD DevBlog
/// antes de escribir esta implementación (protocolo de CLAUDE.md para API incierta): confianza
/// alta, firma citada explícitamente en fuentes oficiales. No requiere leer RegAppTable — eso solo
/// hace falta para REGISTRAR una app nueva antes de escribir xdata, no para leerla.
/// </summary>
public static class ShapeDetectionCommands
{
  public static Task<object?> DetectParallelLinePairsAsync(JsonObject? parameters)
  {
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");
    var angleToleranceDegrees = PluginRuntime.GetOptionalDouble(parameters, "angleToleranceDegrees") ?? 2.0;
    var maxDistance = PluginRuntime.GetOptionalDouble(parameters, "maxDistance") ?? 1.0;

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var lines = new List<Line>();
      foreach (ObjectId id in ModelSpace(tr, db))
      {
        if (tr.GetObject(id, OpenMode.ForRead) is not Line line) continue;
        if (layer != null && !string.Equals(line.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;
        lines.Add(line);
      }

      var angleToleranceRadians = angleToleranceDegrees * Math.PI / 180.0;
      var pairs = new List<object>();

      for (var i = 0; i < lines.Count; i++)
      {
        for (var j = i + 1; j < lines.Count; j++)
        {
          var a = lines[i];
          var b = lines[j];

          if (!AreAnglesParallel(LineAngle(a), LineAngle(b), angleToleranceRadians)) continue;

          var distanceAtStart = PerpendicularDistance(a, b.StartPoint);
          var distanceAtEnd = PerpendicularDistance(a, b.EndPoint);
          if (distanceAtStart > maxDistance) continue;

          // Distancia consistente en ambos extremos de "b" confirma que son realmente paralelas
          // y no solo cruzadas con un ángulo parecido en un punto.
          var spread = Math.Abs(distanceAtStart - distanceAtEnd);
          if (spread > maxDistance * 0.25) continue;

          pairs.Add(new
          {
            handleA = a.Handle.ToString(),
            handleB = b.Handle.ToString(),
            distance = (distanceAtStart + distanceAtEnd) / 2.0,
            layer = a.Layer,
          });
        }
      }

      return new { layer, angleToleranceDegrees, maxDistance, pairs };
    });
  }

  public static Task<object?> GroupEntitiesByProximityAsync(JsonObject? parameters)
  {
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");
    var radius = PluginRuntime.GetRequiredDouble(parameters, "radius");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var items = new List<(ObjectId Id, Point3d Center, string ObjectType, string Layer)>();

      foreach (ObjectId id in ModelSpace(tr, db))
      {
        if (tr.GetObject(id, OpenMode.ForRead) is not Autodesk.AutoCAD.DatabaseServices.Entity ent) continue;
        if (layer != null && !string.Equals(ent.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;

        try
        {
          var extents = ent.GeometricExtents;
          var center = new Point3d(
            (extents.MinPoint.X + extents.MaxPoint.X) / 2.0,
            (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0,
            0);
          items.Add((id, center, ent.GetType().Name, ent.Layer));
        }
        catch
        {
          // Sin geometría desplegable (ej. algunos objetos no-gráficos) — se omite, no se cuenta.
        }
      }

      // Union-find simple: agrupa por adyacencia transitiva (A-B y B-C cercanos agrupa A,B,C
      // aunque A-C esté lejos), que es lo que "una sola unidad simbólica" necesita — un símbolo de
      // 3+ piezas sueltas no siempre tiene todos los pares dentro del radio entre sí.
      var parent = Enumerable.Range(0, items.Count).ToArray();
      int Find(int x) => parent[x] == x ? x : (parent[x] = Find(parent[x]));
      void Union(int a, int b) { var ra = Find(a); var rb = Find(b); if (ra != rb) parent[ra] = rb; }

      for (var i = 0; i < items.Count; i++)
      {
        for (var j = i + 1; j < items.Count; j++)
        {
          if (items[i].Center.DistanceTo(items[j].Center) <= radius) Union(i, j);
        }
      }

      var groups = items
        .Select((item, idx) => (item, root: Find(idx)))
        .GroupBy(x => x.root)
        .Select(g => new
        {
          handles = g.Select(x => x.item.Id.Handle.ToString()).ToList(),
          entityTypes = g.Select(x => x.item.ObjectType).Distinct().ToList(),
          layer = g.First().item.Layer,
          center = new
          {
            x = g.Average(x => x.item.Center.X),
            y = g.Average(x => x.item.Center.Y),
          },
        })
        .ToList();

      return new { layer, radius, groups };
    });
  }

  public static Task<object?> GetEntityExtendedDataAsync(JsonObject? parameters)
  {
    var handleString = PluginRuntime.GetRequiredString(parameters, "handle");
    var appName = PluginRuntime.GetOptionalString(parameters, "appName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var obj = tr.GetObject(GenericObjectCommands.ResolveHandle(db, handleString), OpenMode.ForRead);
      var resultBuffer = appName != null ? obj.GetXDataForApplication(appName) : obj.XData;

      if (resultBuffer == null)
      {
        return new { handle = handleString, appName, applications = new List<object>() };
      }

      var applications = new List<object>();
      string? currentApp = null;
      List<object>? currentEntries = null;

      foreach (var typedValue in resultBuffer.AsArray())
      {
        if (typedValue.TypeCode == (int)DxfCode.ExtendedDataRegAppName)
        {
          currentApp = typedValue.Value?.ToString();
          currentEntries = new List<object>();
          applications.Add(new { appName = currentApp, entries = currentEntries });
          continue;
        }

        currentEntries?.Add(new { typeCode = typedValue.TypeCode, value = SerializeXDataValue(typedValue.Value) });
      }

      return new { handle = handleString, appName, applications };
    });
  }

  // ── Helpers ──

  private static BlockTableRecord ModelSpace(Transaction tr, Database db)
  {
    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
  }

  private static double LineAngle(Line line)
  {
    var delta = line.EndPoint - line.StartPoint;
    var angle = Math.Atan2(delta.Y, delta.X);
    // Normaliza a [0, PI) — una línea y su "opuesta" (180°) son la misma dirección para paralelismo.
    if (angle < 0) angle += Math.PI;
    if (angle >= Math.PI) angle -= Math.PI;
    return angle;
  }

  private static bool AreAnglesParallel(double angleA, double angleB, double tolerance)
  {
    var diff = Math.Abs(angleA - angleB);
    diff = Math.Min(diff, Math.PI - diff);
    return diff <= tolerance;
  }

  private static double PerpendicularDistance(Line line, Point3d point)
  {
    var direction = (line.EndPoint - line.StartPoint).GetNormal();
    var toPoint = point - line.StartPoint;
    var projectionLength = toPoint.DotProduct(direction);
    var closestPoint = line.StartPoint + direction * projectionLength;
    return point.DistanceTo(closestPoint);
  }

  private static object? SerializeXDataValue(object? value)
  {
    return value switch
    {
      null => null,
      Point3d p3 => new { x = p3.X, y = p3.Y, z = p3.Z },
      Point2d p2 => new { x = p2.X, y = p2.Y },
      ObjectId oid => oid.IsNull ? null : oid.Handle.ToString(),
      Handle h => h.ToString(),
      _ => value,
    };
  }
}
