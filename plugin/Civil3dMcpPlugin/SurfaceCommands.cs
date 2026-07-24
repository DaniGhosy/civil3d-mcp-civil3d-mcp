using System.Text.Json.Nodes;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

public static class SurfaceCommands
{
  public static Task<object?> ListSurfacesAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surfaces = new List<object>();

      foreach (ObjectId id in civilDoc.GetSurfaceIds())
      {
        var surface = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
        if (surface == null) continue;

        surfaces.Add(new
        {
          name = surface.Name,
          handle = surface.Handle.ToString(),
          type = surface is TinSurface ? "TIN" : "Grid",
          layer = surface.Layer
        });
      }

      return new { surfaces };
    });
  }

  public static Task<object?> GetSurfaceAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = FindSurfaceByName(civilDoc, tr, name);

      return new
      {
        name = surface.Name,
        handle = surface.Handle.ToString(),
        type = surface is TinSurface ? "TIN" : "Grid",
        layer = surface.Layer,
        style = surface.StyleName
      };
    });
  }

  public static Task<object?> GetSurfaceElevationAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var x = PluginRuntime.GetRequiredDouble(parameters, "x");
    var y = PluginRuntime.GetRequiredDouble(parameters, "y");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = FindSurfaceByName(civilDoc, tr, name);
      var elevation = surface.FindElevationAtXY(x, y);

      return new
      {
        surfaceName = name,
        elevation,
        x,
        y
      };
    });
  }

  public static Task<object?> GetSurfaceStatisticsAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = FindSurfaceByName(civilDoc, tr, name);
      var props = surface.GetGeneralProperties();

      return new
      {
        surfaceName = name,
        minimumElevation = props.MinimumElevation,
        maximumElevation = props.MaximumElevation,
        numberOfPoints = props.NumberOfPoints
      };
    });
  }

  public static Task<object?> CreateSurfaceAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var description = PluginRuntime.GetOptionalString(parameters, "description");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surfaceId = TinSurface.Create(db, name);
      var surface = tr.GetObject(surfaceId, OpenMode.ForWrite) as TinSurface;

      if (surface != null && description != null)
        surface.Description = description;

      return new
      {
        success = true,
        name,
        handle = surface?.Handle.ToString()
      };
    });
  }

  public static Task<object?> DeleteSurfaceAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = FindSurfaceByName(civilDoc, tr, name);
      surface.UpgradeOpen();
      surface.Erase();

      return new { success = true, deleted = name };
    });
  }

  public static Task<object?> AddSurfacePointsAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var pointsNode = parameters?["points"] as JsonArray;

    if (pointsNode == null || pointsNode.Count == 0)
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "points required");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = FindSurfaceByName(civilDoc, tr, name) as TinSurface
        ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Not a TIN surface");

      surface.UpgradeOpen();

      var pts = new Point3dCollection();

      foreach (var pt in pointsNode)
      {
        pts.Add(new Point3d(
          pt!["x"]!.GetValue<double>(),
          pt["y"]!.GetValue<double>(),
          pt["z"]!.GetValue<double>()
        ));
      }

      surface.AddVertices(pts);

      return new
      {
        success = true,
        surfaceName = name,
        added = pts.Count
      };
    });
  }

  // ─────────────────────────────────────────────
  // FIX IMPORTANTE: NO existe volume API directo
  // ─────────────────────────────────────────────
  public static Task<object?> ComputeSurfaceVolumeAsync(JsonObject? parameters)
  {
    var baseName = PluginRuntime.GetRequiredString(parameters, "baseSurface");
    var compName = PluginRuntime.GetRequiredString(parameters, "comparisonSurface");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var baseSurface = FindSurfaceByName(civilDoc, tr, baseName) as TinSurface;
      var compSurface = FindSurfaceByName(civilDoc, tr, compName) as TinSurface;

      if (baseSurface == null || compSurface == null)
        throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Both surfaces must be TIN");

      return new
      {
        baseSurface = baseName,
        comparisonSurface = compName,
        cutVolume = 0.0,
        fillVolume = 0.0,
        netVolume = 0.0,
        note = "Civil 3D .NET API does not expose direct volume calculation. Requires Surface Analysis workflow."
      };
    });
  }

  // ─────────────────────────────────────────────
  // Tabla Área vs Elevación (área acumulada bajo cada cota)
  // ─────────────────────────────────────────────
  public static Task<object?> GetSurfaceAreaElevationTableAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var interval = PluginRuntime.GetOptionalDouble(parameters, "interval") ?? 1.0;

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = FindSurfaceByName(civilDoc, tr, name) as TinSurface
        ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Not a TIN surface");

      var props = surface.GetGeneralProperties();
      double minElev = props.MinimumElevation;
      double maxElev = props.MaximumElevation;

      var table = new List<object>();

      // Iteramos cada nivel de elevación y calculamos el área
      // usando los triángulos del TIN que quedan por debajo de esa cota
      var triangles = surface.GetTriangles(false);

      for (double elev = minElev; elev <= maxElev; elev += interval)
      {
        double area = 0.0;

        foreach (var triangle in triangles)
        {
          var v0 = triangle.Vertex1.Location;
          var v1 = triangle.Vertex2.Location;
          var v2 = triangle.Vertex3.Location;

          // Solo incluir triángulos completamente bajo la elevación actual
          if (v0.Z <= elev && v1.Z <= elev && v2.Z <= elev)
          {
            // Área horizontal del triángulo (ignorando Z)
            double ax = v1.X - v0.X;
            double ay = v1.Y - v0.Y;
            double bx = v2.X - v0.X;
            double by = v2.Y - v0.Y;
            area += Math.Abs(ax * by - ay * bx) / 2.0;
          }
        }

        table.Add(new
        {
          elevation = Math.Round(elev, 3),
          areaSqM = Math.Round(area, 3),
          areaHa = Math.Round(area / 10000.0, 6),
        });
      }

      return new
      {
        surfaceName = name,
        interval,
        minElevation = minElev,
        maxElevation = maxElev,
        rowCount = table.Count,
        table
      };
    });
  }

  // ─────────────────────────────────────────────
  // NUEVO: Volumen por curvas de nivel extraídas (polilíneas reales
  // en una capa). Calcula el área encerrada de CADA curva individual
  // (no acumulada), arma la tabla, exporta a CSV y calcula el volumen
  // total tanto por método trapezoidal como por el método simple
  // (suma de áreas × intervalo).
  // ─────────────────────────────────────────────
  public static Task<object?> ComputeContourVolumeAsync(JsonObject? parameters)
  {
    var layerName = PluginRuntime.GetRequiredString(parameters, "layerName");
    var minElevation = PluginRuntime.GetOptionalDouble(parameters, "minElevation");
    var maxElevation = PluginRuntime.GetOptionalDouble(parameters, "maxElevation");
    var interval = PluginRuntime.GetOptionalDouble(parameters, "interval") ?? 0.01;
    var outputCsvPath = PluginRuntime.GetOptionalString(parameters, "outputCsvPath");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      var rows = new List<(double elevation, double area, string handle)>();

      foreach (ObjectId id in btr)
      {
        var ent = tr.GetObject(id, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
        if (ent == null || !string.Equals(ent.Layer, layerName, StringComparison.OrdinalIgnoreCase))
          continue;

        var pts = new List<Point3d>();

        if (ent is Polyline3d p3d)
        {
          foreach (ObjectId vId in p3d)
          {
            var v = tr.GetObject(vId, OpenMode.ForRead) as PolylineVertex3d;
            if (v != null) pts.Add(v.Position);
          }
        }
        else if (ent is Polyline p2d)
        {
          for (int i = 0; i < p2d.NumberOfVertices; i++)
            pts.Add(p2d.GetPoint3dAt(i));
        }
        else
        {
          continue; // otro tipo de entidad, ignorar
        }

        if (pts.Count < 3) continue;

        double elevation = pts[0].Z; // curva de nivel: Z constante
        if (minElevation.HasValue && elevation < minElevation.Value) continue;
        if (maxElevation.HasValue && elevation > maxElevation.Value) continue;

        // Área con Shoelace sobre la proyección XY
        double area = 0.0;
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
          var a = pts[i];
          var b = pts[(i + 1) % n];
          area += (a.X * b.Y) - (b.X * a.Y);
        }
        area = Math.Abs(area) / 2.0;

        rows.Add((Math.Round(elevation, 4), Math.Round(area, 4), ent.Handle.ToString()));
      }

      // Ordenar por elevación ascendente
      rows.Sort((a, b) => a.elevation.CompareTo(b.elevation));

      // Volumen método trapezoidal (usa la diferencia real de elevación
      // entre curvas consecutivas, no asume espaciado uniforme)
      double totalVolume = 0.0;
      for (int i = 0; i < rows.Count - 1; i++)
      {
        double h = rows[i + 1].elevation - rows[i].elevation;
        double avgArea = (rows[i].area + rows[i + 1].area) / 2.0;
        totalVolume += avgArea * h;
      }

      // Volumen método simple (suma directa × intervalo) para comparar
      double simpleVolume = rows.Sum(r => r.area) * interval;

      if (!string.IsNullOrEmpty(outputCsvPath))
      {
        using var writer = new StreamWriter(outputCsvPath, false, System.Text.Encoding.UTF8);
        writer.WriteLine("Elevacion_m;Area_m2;Handle");
        foreach (var r in rows)
          writer.WriteLine($"{r.elevation:F3};{r.area:F4};{r.handle}");
        writer.WriteLine();
        writer.WriteLine($"Volumen_trapezoidal_m3;{totalVolume:F4}");
        writer.WriteLine($"Volumen_metodo_simple_m3;{simpleVolume:F4}");
      }

      return new
      {
        layerName,
        rowCount = rows.Count,
        totalVolumeTrapezoidal = Math.Round(totalVolume, 4),
        totalVolumeSimpleMethod = Math.Round(simpleVolume, 4),
        csvPath = outputCsvPath,
        table = rows.Select(r => new { elevation = r.elevation, areaSqM = r.area, handle = r.handle })
      };
    });
  }

// ─────────────────────────────────────────────
// NUEVO: Cerrar curvas de nivel abiertas contra un boundary específico
// Agregar dentro de la clase SurfaceCommands
// ─────────────────────────────────────────────
//
// Parámetros esperados:
// {
//   "contourLayerName": "C-TOPO-MINR",
//   "boundaryLayerName": "Boundary_Barlovento",   // capa que contiene SOLO el boundary de ese lado
//   "minElevation": 1.45,          // opcional
//   "maxElevation": 2.23,          // opcional
//   "snapTolerance": 0.5,          // distancia máxima (m) para unir extremos de curvas y para
//                                  // considerar que un extremo "toca" el boundary
//   "outputCsvPath": "C:\\...\\tabla_cerrada.csv"  // opcional
// }

public static Task<object?> CloseContoursAgainstBoundaryAsync(JsonObject? parameters)
{
  var contourLayerName = PluginRuntime.GetRequiredString(parameters, "contourLayerName");
  var boundaryLayerName = PluginRuntime.GetRequiredString(parameters, "boundaryLayerName");
  var minElevation = PluginRuntime.GetOptionalDouble(parameters, "minElevation");
  var maxElevation = PluginRuntime.GetOptionalDouble(parameters, "maxElevation");
  var snapTolerance = PluginRuntime.GetOptionalDouble(parameters, "snapTolerance") ?? 0.5;
  var outputCsvPath = PluginRuntime.GetOptionalString(parameters, "outputCsvPath");
  var outputLayerName = PluginRuntime.GetOptionalString(parameters, "outputLayerName") ?? (contourLayerName + "-CERRADAS");

  return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
  {
    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

    // ── 0. Asegurar que exista la capa de salida ──
    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
    if (!lt.Has(outputLayerName))
    {
      lt.UpgradeOpen();
      var newLayer = new LayerTableRecord { Name = outputLayerName };
      lt.Add(newLayer);
      tr.AddNewlyCreatedDBObject(newLayer, true);
    }

    // ── 1. Leer el boundary (una sola polilínea en su propia capa) ──
    List<Point3d>? boundaryPts = null;

    foreach (ObjectId id in btr)
    {
      var ent = tr.GetObject(id, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
      if (ent == null || !string.Equals(ent.Layer, boundaryLayerName, StringComparison.OrdinalIgnoreCase))
        continue;

      boundaryPts = ReadEntityVertices(ent, tr);
      break; // se asume una sola polilínea en esa capa
    }

    if (boundaryPts == null || boundaryPts.Count < 2)
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT",
        $"No se encontró una polilínea de boundary en la capa '{boundaryLayerName}'");

    // Parámetro acumulado de longitud de arco para cada vértice del boundary
    var boundaryArc = new List<double> { 0.0 };
    for (int i = 1; i < boundaryPts.Count; i++)
      boundaryArc.Add(boundaryArc[i - 1] + Distance2D(boundaryPts[i - 1], boundaryPts[i]));
    double boundaryTotalLength = boundaryArc[^1];

    // ── 2. Leer todas las curvas de nivel de la capa indicada ──
    var contours = new List<(double elevation, List<Point3d> pts, bool closed, string handle)>();

    foreach (ObjectId id in btr)
    {
      var ent = tr.GetObject(id, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
      if (ent == null || !string.Equals(ent.Layer, contourLayerName, StringComparison.OrdinalIgnoreCase))
        continue;

      var pts = ReadEntityVertices(ent, tr);
      if (pts == null || pts.Count < 2) continue;

      bool isClosed = Distance2D(pts[0], pts[^1]) < 1e-6;
      double elevation = pts[0].Z;

      if (minElevation.HasValue && elevation < minElevation.Value) continue;
      if (maxElevation.HasValue && elevation > maxElevation.Value) continue;

      contours.Add((Math.Round(elevation, 4), pts, isClosed, ent.Handle.ToString()));
    }

    // ── 3. Procesar por grupo de elevación ──
    var resultRows = new List<object>();

    foreach (var group in contours.GroupBy(c => c.elevation).OrderBy(g => g.Key))
    {
      double elev = group.Key;

      // Separar las que ya están cerradas (se dejan tal cual, pero también
      // se copian a la capa de salida para verlas todas juntas)
      foreach (var closedContour in group.Where(c => c.closed))
      {
        double areaClosed = ShoelaceArea(closedContour.pts);
        DrawClosedPolyline(closedContour.pts, outputLayerName, btr, tr);
        resultRows.Add(new { elevation = elev, areaSqM = Math.Round(areaClosed, 4), source = "ya_cerrada", handle = closedContour.handle });
      }

      // Cadenas abiertas de este nivel
      var openChains = group.Where(c => !c.closed).Select(c => new List<Point3d>(c.pts)).ToList();

      // ── 3a. Unir cadenas cuyos extremos estén muy cerca entre sí ──
      bool merged = true;
      while (merged)
      {
        merged = false;
        for (int i = 0; i < openChains.Count && !merged; i++)
        {
          for (int j = i + 1; j < openChains.Count && !merged; j++)
          {
            var a = openChains[i];
            var b = openChains[j];

            if (Distance2D(a[^1], b[0]) <= snapTolerance)
            {
              a.AddRange(b.Skip(1));
              openChains.RemoveAt(j);
              merged = true;
            }
            else if (Distance2D(a[^1], b[^1]) <= snapTolerance)
            {
              b.Reverse();
              a.AddRange(b.Skip(1));
              openChains.RemoveAt(j);
              merged = true;
            }
            else if (Distance2D(a[0], b[0]) <= snapTolerance)
            {
              a.Reverse();
              a.AddRange(b.Skip(1));
              openChains.RemoveAt(j);
              merged = true;
            }
            else if (Distance2D(a[0], b[^1]) <= snapTolerance)
            {
              b.AddRange(a.Skip(1));
              openChains[i] = b;
              openChains.RemoveAt(j);
              merged = true;
            }
          }
        }
      }

      // ── 3b. Cerrar cada cadena resultante contra el boundary ──
      foreach (var chain in openChains)
      {
        var start = chain[0];
        var end = chain[^1];

        double sStart = ProjectOntoBoundary(start, boundaryPts, boundaryArc);
        double sEnd = ProjectOntoBoundary(end, boundaryPts, boundaryArc);

        // Probamos los dos tramos posibles del boundary (el "corto" y el "largo")
        // y nos quedamos con el que da el área MÁS CHICA, porque el tramo
        // equivocado genera polígonos gigantes que no representan la realidad.
        var segmentShort = GetBoundaryArcBetween(boundaryPts, boundaryArc, sEnd, sStart);
        var segmentLong = GetBoundaryArcBetweenLongWay(boundaryPts, boundaryArc, sEnd, sStart, boundaryTotalLength);

        var polyShort = new List<Point3d>(chain);
        polyShort.AddRange(segmentShort);
        double areaShort = ShoelaceArea(polyShort);

        var polyLong = new List<Point3d>(chain);
        polyLong.AddRange(segmentLong);
        double areaLong = ShoelaceArea(polyLong);

        double area = Math.Min(areaShort, areaLong);
        string chosen = areaShort <= areaLong ? "tramo_corto" : "tramo_largo";
        var finalPolygon = areaShort <= areaLong ? polyShort : polyLong;

        DrawClosedPolyline(finalPolygon, outputLayerName, btr, tr);

        resultRows.Add(new
        {
          elevation = elev,
          areaSqM = Math.Round(area, 4),
          source = "cerrada_con_boundary",
          tramo = chosen,
          handle = (string?)null
        });
      }
    }

    if (!string.IsNullOrEmpty(outputCsvPath))
    {
      using var writer = new StreamWriter(outputCsvPath, false, System.Text.Encoding.UTF8);
      writer.WriteLine("Elevacion_m;Area_m2;Origen");
      foreach (dynamic r in resultRows)
        writer.WriteLine($"{r.elevation:F3};{r.areaSqM:F4};{r.source}");
    }

    return new
    {
      contourLayerName,
      boundaryLayerName,
      outputLayerName,
      snapTolerance,
      rowCount = resultRows.Count,
      csvPath = outputCsvPath,
      table = resultRows
    };
  });
}

// ── Helpers ──

private static List<Point3d> ReadEntityVertices(Autodesk.AutoCAD.DatabaseServices.Entity ent, Transaction tr)
{
  var pts = new List<Point3d>();

  if (ent is Polyline3d p3d)
  {
    foreach (ObjectId vId in p3d)
    {
      var v = tr.GetObject(vId, OpenMode.ForRead) as PolylineVertex3d;
      if (v != null) pts.Add(v.Position);
    }
  }
  else if (ent is Polyline p2d)
  {
    for (int i = 0; i < p2d.NumberOfVertices; i++)
      pts.Add(p2d.GetPoint3dAt(i));
  }

  return pts;
}

private static double Distance2D(Point3d a, Point3d b)
{
  double dx = a.X - b.X;
  double dy = a.Y - b.Y;
  return Math.Sqrt(dx * dx + dy * dy);
}

private static double ShoelaceArea(List<Point3d> pts)
{
  double area = 0.0;
  int n = pts.Count;
  for (int i = 0; i < n; i++)
  {
    var a = pts[i];
    var b = pts[(i + 1) % n];
    area += (a.X * b.Y) - (b.X * a.Y);
  }
  return Math.Abs(area) / 2.0;
}

// Devuelve el parámetro de longitud de arco (a lo largo del boundary) del punto
// más cercano de la polilínea de boundary al punto dado.
private static double ProjectOntoBoundary(Point3d point, List<Point3d> boundaryPts, List<double> boundaryArc)
{
  double bestDist = double.MaxValue;
  double bestParam = 0.0;

  for (int i = 0; i < boundaryPts.Count - 1; i++)
  {
    var a = boundaryPts[i];
    var b = boundaryPts[i + 1];

    double dx = b.X - a.X, dy = b.Y - a.Y;
    double segLenSq = dx * dx + dy * dy;
    double t = segLenSq < 1e-12 ? 0.0 :
      ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / segLenSq;
    t = Math.Max(0.0, Math.Min(1.0, t));

    double px = a.X + t * dx, py = a.Y + t * dy;
    double dist = Math.Sqrt((point.X - px) * (point.X - px) + (point.Y - py) * (point.Y - py));

    if (dist < bestDist)
    {
      bestDist = dist;
      double segLen = Math.Sqrt(segLenSq);
      bestParam = boundaryArc[i] + t * segLen;
    }
  }

  return bestParam;
}

// Dibuja una Polyline3d cerrada en la capa indicada, a partir de una
// lista de puntos (no hace falta repetir el primer punto al final,
// se cierra automáticamente con Closed = true).
private static void DrawClosedPolyline(List<Point3d> pts, string layerName, BlockTableRecord btr, Transaction tr)
{
  if (pts.Count < 3) return;

  var pts3d = new Point3dCollection();
  foreach (var p in pts)
    pts3d.Add(p);

  var poly = new Polyline3d(Poly3dType.SimplePoly, pts3d, false);
  poly.Layer = layerName;

  btr.AppendEntity(poly);
  tr.AddNewlyCreatedDBObject(poly, true);

  poly.UpgradeOpen();
  poly.Closed = true;
}

// Devuelve los puntos del boundary entre los parámetros sFrom y sTo,
// tomando siempre el tramo más corto (para no "irse" hacia el otro extremo).
private static List<Point3d> GetBoundaryArcBetween(List<Point3d> boundaryPts, List<double> boundaryArc, double sFrom, double sTo)
{
  double lo = Math.Min(sFrom, sTo);
  double hi = Math.Max(sFrom, sTo);

  var segment = new List<Point3d>();
  for (int i = 0; i < boundaryPts.Count; i++)
  {
    if (boundaryArc[i] >= lo && boundaryArc[i] <= hi)
      segment.Add(boundaryPts[i]);
  }

  // Orientar el tramo para que vaya de sFrom hacia sTo
  if (sFrom > sTo)
    segment.Reverse();

  return segment;
}

// Devuelve el tramo "largo" (dando la vuelta completa por el otro lado
// del boundary cerrado) entre sFrom y sTo.
private static List<Point3d> GetBoundaryArcBetweenLongWay(List<Point3d> boundaryPts, List<double> boundaryArc, double sFrom, double sTo, double totalLength)
{
  double lo = Math.Min(sFrom, sTo);
  double hi = Math.Max(sFrom, sTo);

  // Todo lo que NO está en el tramo corto (es decir, arc <= lo o arc >= hi)
  var outerHigh = new List<Point3d>(); // desde hi hasta el final del loop
  var outerLow = new List<Point3d>();  // desde el inicio del loop hasta lo

  for (int i = 0; i < boundaryPts.Count; i++)
  {
    if (boundaryArc[i] >= hi)
      outerHigh.Add(boundaryPts[i]);
    else if (boundaryArc[i] <= lo)
      outerLow.Add(boundaryPts[i]);
  }

  // El tramo largo va: desde "hi" hacia el final del loop, y luego
  // desde el inicio del loop hasta "lo" (dando toda la vuelta).
  var segment = new List<Point3d>();
  segment.AddRange(outerHigh);
  segment.AddRange(outerLow);

  // Orientar para que empiece en sFrom y termine en sTo
  if (sFrom < sTo)
    segment.Reverse();

  return segment;
}

  // ─────────────────────────────────────────────
  // FIX CRÍTICO: wrapper seguro sin CivilDocument real
  // ─────────────────────────────────────────────
  private static Autodesk.Civil.DatabaseServices.Surface FindSurfaceByName(
    dynamic civilDoc,
    Transaction tr,
    string name)
  {
    foreach (ObjectId id in civilDoc.GetSurfaceIds())
    {
      var surface = tr.GetObject(id, OpenMode.ForRead)
        as Autodesk.Civil.DatabaseServices.Surface;

      if (surface != null &&
          string.Equals(surface.Name, name, StringComparison.OrdinalIgnoreCase))
      {
        return surface;
      }
    }

    throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"Surface '{name}' not found");
  }

  // ─────────────────────────────────────────────
  // FIX PARA COMMAND DISPATCHER (OBLIGATORIO)
  // ─────────────────────────────────────────────
  public static Task<object?> AddSurfaceBreaklineAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });

  public static Task<object?> AddSurfaceBoundaryAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });

  public static Task<object?> ExtractSurfaceContoursAsync(JsonObject? p)
    => Task.FromResult<object?>(new { status = "planned" });
}