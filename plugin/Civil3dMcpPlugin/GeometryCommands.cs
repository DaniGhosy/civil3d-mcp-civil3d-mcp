using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Civil3DMcpPlugin;

/// <summary>
/// Handles basic AutoCAD geometry creation: lines, polylines, 3D polylines, text.
/// </summary>
public static class GeometryCommands
{
  public static Task<object?> CreateLineSegmentAsync(JsonObject? parameters)
  {
    var startX = PluginRuntime.GetRequiredDouble(parameters, "startX");
    var startY = PluginRuntime.GetRequiredDouble(parameters, "startY");
    var startZ = PluginRuntime.GetOptionalDouble(parameters, "startZ") ?? 0.0;
    var endX = PluginRuntime.GetRequiredDouble(parameters, "endX");
    var endY = PluginRuntime.GetRequiredDouble(parameters, "endY");
    var endZ = PluginRuntime.GetOptionalDouble(parameters, "endZ") ?? 0.0;
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var line = new Line(
        new Point3d(startX, startY, startZ),
        new Point3d(endX, endY, endZ)
      );

      if (layer != null)
      {
        GenericObjectCommands.EnsureLayerId(db, tr, layer);
        line.Layer = layer;
      }

      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
      btr.AppendEntity(line);
      tr.AddNewlyCreatedDBObject(line, true);

      return new
      {
        success = true,
        handle = line.Handle.ToString(),
        objectType = "Line",
      };
    });
  }

  public static Task<object?> CreatePolylineAsync(JsonObject? parameters)
  {
    var verticesNode = parameters?["vertices"] as JsonArray;
    var closed = PluginRuntime.GetOptionalBool(parameters, "closed") ?? false;
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");

    if (verticesNode == null || verticesNode.Count < 2)
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "At least 2 vertices are required.");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var polyline = new Polyline();

      for (int i = 0; i < verticesNode.Count; i++)
      {
        var v = verticesNode[i]!;
        polyline.AddVertexAt(i,
          new Point2d(v["x"]!.GetValue<double>(), v["y"]!.GetValue<double>()),
          0, 0, 0);
      }

      polyline.Closed = closed;
      if (layer != null)
      {
        GenericObjectCommands.EnsureLayerId(db, tr, layer);
        polyline.Layer = layer;
      }

      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
      btr.AppendEntity(polyline);
      tr.AddNewlyCreatedDBObject(polyline, true);

      return new
      {
        success = true,
        handle = polyline.Handle.ToString(),
        objectType = "Polyline",
        vertexCount = verticesNode.Count,
      };
    });
  }

  public static Task<object?> Create3dPolylineAsync(JsonObject? parameters)
  {
    var verticesNode = parameters?["vertices"] as JsonArray;
    var closed = PluginRuntime.GetOptionalBool(parameters, "closed") ?? false;
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");

    if (verticesNode == null || verticesNode.Count < 2)
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "At least 2 vertices are required.");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var points = new Point3dCollection();
      foreach (var v in verticesNode)
      {
        points.Add(new Point3d(
          v!["x"]!.GetValue<double>(),
          v["y"]!.GetValue<double>(),
          v["z"]!.GetValue<double>()
        ));
      }

      var polyline3d = new Polyline3d(Poly3dType.SimplePoly, points, closed);
      if (layer != null)
      {
        GenericObjectCommands.EnsureLayerId(db, tr, layer);
        polyline3d.Layer = layer;
      }

      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
      btr.AppendEntity(polyline3d);
      tr.AddNewlyCreatedDBObject(polyline3d, true);

      return new
      {
        success = true,
        handle = polyline3d.Handle.ToString(),
        objectType = "Polyline3d",
        vertexCount = verticesNode.Count,
      };
    });
  }

  public static Task<object?> CreateTextAsync(JsonObject? parameters)
  {
    var text = PluginRuntime.GetRequiredString(parameters, "text");
    var x = PluginRuntime.GetRequiredDouble(parameters, "insertionX");
    var y = PluginRuntime.GetRequiredDouble(parameters, "insertionY");
    var height = PluginRuntime.GetOptionalDouble(parameters, "height") ?? 1.0;
    var rotation = PluginRuntime.GetOptionalDouble(parameters, "rotation") ?? 0.0;
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var dbText = new DBText
      {
        TextString = text,
        Position = new Point3d(x, y, 0),
        Height = height,
        Rotation = rotation * Math.PI / 180.0,
      };

      if (layer != null)
      {
        GenericObjectCommands.EnsureLayerId(db, tr, layer);
        dbText.Layer = layer;
      }

      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
      btr.AppendEntity(dbText);
      tr.AddNewlyCreatedDBObject(dbText, true);

      return new
      {
        success = true,
        handle = dbText.Handle.ToString(),
        objectType = "DBText",
      };
    });
  }

  public static Task<object?> CreateMTextAsync(JsonObject? parameters)
  {
    var text = PluginRuntime.GetRequiredString(parameters, "text");
    var x = PluginRuntime.GetRequiredDouble(parameters, "insertionX");
    var y = PluginRuntime.GetRequiredDouble(parameters, "insertionY");
    var height = PluginRuntime.GetOptionalDouble(parameters, "height") ?? 1.0;
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var mtext = new MText
      {
        Contents = text,
        Location = new Point3d(x, y, 0),
        TextHeight = height,
      };

      if (layer != null)
      {
        GenericObjectCommands.EnsureLayerId(db, tr, layer);
        mtext.Layer = layer;
      }

      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
      btr.AppendEntity(mtext);
      tr.AddNewlyCreatedDBObject(mtext, true);

      return new
      {
        success = true,
        handle = mtext.Handle.ToString(),
        objectType = "MText",
      };
    });
  }

  /// <summary>
  /// Copia rectas todas las lineas de "sourceLayer" cada "spacing" metros
  /// en la direccion "directionDegrees", recortando cada copia contra
  /// el poligono cerrado en "boundaryLayer". Se detiene automaticamente
  /// cuando se acumulan "missTolerance" iteraciones SEGUIDAS sin ninguna
  /// interseccion (para tolerar zonas concavas del boundary donde una
  /// iteracion intermedia puede no cruzar el poligono).
  ///
  /// Params esperados (JSON):
  ///   sourceLayer       (string, requerido)  ej: "0"
  ///   boundaryLayer     (string, requerido)  ej: "A-BLDG"
  ///   directionDegrees  (double, requerido)  angulo de avance, 0=+X, 90=+Y
  ///   spacing           (double, opcional, default 1.5)
  ///   maxCopies         (double, opcional, default 200) limite de seguridad
  /// </summary>
  public static Task<object?> OffsetLinesToBoundaryAsync(JsonObject? parameters)
  {
    var sourceLayer = PluginRuntime.GetRequiredString(parameters, "sourceLayer");
    var boundaryLayer = PluginRuntime.GetRequiredString(parameters, "boundaryLayer");
    var directionDegrees = PluginRuntime.GetRequiredDouble(parameters, "directionDegrees");
    var spacing = PluginRuntime.GetOptionalDouble(parameters, "spacing") ?? 1.5;
    var maxCopies = (int)(PluginRuntime.GetOptionalDouble(parameters, "maxCopies") ?? 200);

    if (spacing <= 0.0)
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "spacing debe ser mayor a 0.");

    // Numero de iteraciones vacias consecutivas toleradas antes de detenerse.
    // Cubre huecos/entrantes concavos del boundary de hasta ~missTolerance * spacing metros.
    const int missTolerance = 5;

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

      // --- 1. Recolectar lineas fuente en sourceLayer ---
      var sourceLines = new List<Line>();
      foreach (ObjectId id in btr)
      {
        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
        if (ent is Line ln && ln.Layer == sourceLayer)
          sourceLines.Add(ln);
      }

      if (sourceLines.Count == 0)
        throw new JsonRpcDispatchException(
          "CIVIL3D.INVALID_INPUT",
          $"No se encontraron objetos Line en la capa '{sourceLayer}'.");

      // --- 2. Encontrar el boundary (primera Polyline cerrada en boundaryLayer) ---
      Polyline? boundary = null;
      foreach (ObjectId id in btr)
      {
        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
        if (ent is Polyline pl && pl.Layer == boundaryLayer && pl.Closed)
        {
          boundary = pl;
          break;
        }
      }

      if (boundary == null)
        throw new JsonRpcDispatchException(
          "CIVIL3D.INVALID_INPUT",
          $"No se encontro una Polyline CERRADA en la capa '{boundaryLayer}'.");

      // --- 3. Vector de avance ---
      double rad = directionDegrees * Math.PI / 180.0;
      var dirVec = new Vector3d(Math.Cos(rad), Math.Sin(rad), 0.0);

      var createdHandles = new List<object>();
      int iteration = 1;
      int totalCreated = 0;
      int consecutiveMisses = 0;

      while (iteration <= maxCopies)
      {
        var offsetVec = dirVec * (spacing * iteration);
        bool anyIntersectionThisIteration = false;
        var handlesThisIteration = new List<string>();

        foreach (var srcLine in sourceLines)
        {
          // Trasladar la linea original (recta, sin deformar)
          var p1 = srcLine.StartPoint + offsetVec;
          var p2 = srcLine.EndPoint + offsetVec;

          using (var offsetLine = new Line(p1, p2))
          {
            var pts = new Point3dCollection();
            offsetLine.IntersectWith(
              boundary, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);

            if (pts.Count < 2)
              continue; // esta linea no cruza el poligono en esta iteracion

            anyIntersectionThisIteration = true;

            // Proyectar los puntos de interseccion sobre la direccion de la
            // linea para tomar los dos extremos (min y max parametro).
            var lineDir = (p2 - p1).GetNormal();
            double minT = double.MaxValue, maxT = double.MinValue;
            Point3d ptMin = p1, ptMax = p2;

            foreach (Point3d pt in pts)
            {
              double t = (pt - p1).DotProduct(lineDir);
              if (t < minT) { minT = t; ptMin = pt; }
              if (t > maxT) { maxT = t; ptMax = pt; }
            }

            var trimmedLine = new Line(ptMin, ptMax) { Layer = srcLine.Layer };
            btr.AppendEntity(trimmedLine);
            tr.AddNewlyCreatedDBObject(trimmedLine, true);

            handlesThisIteration.Add(trimmedLine.Handle.ToString());
            totalCreated++;
          }
        }

        if (!anyIntersectionThisIteration)
        {
          consecutiveMisses++;
          if (consecutiveMisses >= missTolerance)
            break; // ya se acumularon suficientes fallos seguidos: fin real
        }
        else
        {
          consecutiveMisses = 0; // se resetea el contador de fallos
          createdHandles.Add(new
          {
            iteration,
            offsetDistance = spacing * iteration,
            handles = handlesThisIteration,
          });
        }

        iteration++;
      }

      return new
      {
        success = true,
        totalLinesCreated = totalCreated,
        iterationsCompleted = iteration - 1,
        spacing,
        directionDegrees,
        details = createdHandles,
        warning = totalCreated == 0
          ? $"No intersections found between '{sourceLayer}' and the boundary on '{boundaryLayer}' " +
            $"across {iteration - 1} iteration(s) — 0 lines created."
          : null,
      };
    });
  }
}