using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Handles COGO point operations: list, get, create, delete, groups, import.
/// </summary>
public static class PointCommands
{
  public static Task<object?> ListCogoPointsAsync(JsonObject? parameters)
  {
    var groupName = PluginRuntime.GetOptionalString(parameters, "groupName");
    var limit = PluginRuntime.GetOptionalInt(parameters, "limit") ?? 500;

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var cogoPoints = civilDoc.CogoPoints;
      var points = new List<object>();
      var count = 0;

      // Filtrado real por membresía de grupo vía PointGroup.ContainsPoint(uint),
      // confirmado por búsqueda en la documentación/foros oficiales de Autodesk
      // (PointGroup no expone una lista simple de números de punto; la
      // membresía se resuelve por criterio de query, y ContainsPoint es el
      // método puntual para chequear un número específico).
      PointGroup? group = null;
      if (groupName != null)
      {
        group = FindPointGroupByName(civilDoc, tr, groupName);
      }

      foreach (ObjectId id in cogoPoints)
      {
        if (count >= limit) break;

        var point = tr.GetObject(id, OpenMode.ForRead) as CogoPoint;
        if (point == null) continue;
        if (group != null && !group.ContainsPoint(point.PointNumber)) continue;

        points.Add(new
        {
          pointNumber = point.PointNumber,
          easting = point.Easting,
          northing = point.Northing,
          elevation = point.Elevation,
          rawDescription = point.RawDescription,
          fullDescription = point.FullDescription,
        });
        count++;
      }

      return new { points, total = group != null ? group.PointsCount : cogoPoints.Count };
    });
  }

  public static Task<object?> GetCogoPointAsync(JsonObject? parameters)
  {
    var pointNumber = PluginRuntime.GetRequiredInt(parameters, "pointNumber");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var cogoPoints = civilDoc.CogoPoints;

      foreach (ObjectId id in cogoPoints)
      {
        var point = tr.GetObject(id, OpenMode.ForRead) as CogoPoint;
        if (point != null && point.PointNumber == (uint)pointNumber)
        {
          return new
          {
            pointNumber = point.PointNumber,
            easting = point.Easting,
            northing = point.Northing,
            elevation = point.Elevation,
            rawDescription = point.RawDescription,
            fullDescription = point.FullDescription,
            handle = point.Handle.ToString(),
            layer = point.Layer,
          };
        }
      }

      throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"COGO point #{pointNumber} not found.");
    });
  }

  public static Task<object?> CreateCogoPointsAsync(JsonObject? parameters)
  {
    var pointsNode = parameters?["points"] as JsonArray;

    if (pointsNode == null || pointsNode.Count == 0)
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Parameter 'points' is required.");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var cogoPoints = civilDoc.CogoPoints;
      var created = new List<object>();

      foreach (var pt in pointsNode)
      {
        var easting = pt!["easting"]!.GetValue<double>();
        var northing = pt["northing"]!.GetValue<double>();
        var elevation = pt["elevation"]?.GetValue<double>() ?? 0.0;
        var description = pt["rawDescription"]?.GetValue<string>() ?? "";

        var location = new Point3d(easting, northing, elevation);
        var pointId = cogoPoints.Add(location, false);
        var point = tr.GetObject(pointId, OpenMode.ForWrite) as CogoPoint;

        if (point != null && !string.IsNullOrEmpty(description))
        {
          point.RawDescription = description;
        }

        created.Add(new
        {
          pointNumber = point?.PointNumber ?? 0,
          easting,
          northing,
          elevation,
        });
      }

      return new { success = true, created };
    });
  }

  public static Task<object?> DeleteCogoPointsAsync(JsonObject? parameters)
  {
    var pointNumbersNode = parameters?["pointNumbers"] as JsonArray;

    if (pointNumbersNode == null || pointNumbersNode.Count == 0)
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Parameter 'pointNumbers' is required.");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var cogoPoints = civilDoc.CogoPoints;
      var deleted = new List<int>();

      foreach (var pn in pointNumbersNode)
      {
        var pointNumber = pn!.GetValue<int>();

        foreach (ObjectId id in cogoPoints)
        {
          var point = tr.GetObject(id, OpenMode.ForRead) as CogoPoint;
          if (point != null && point.PointNumber == (uint)pointNumber)
          {
            point.UpgradeOpen();
            point.Erase();
            deleted.Add(pointNumber);
            break;
          }
        }
      }

      return new { success = true, deleted };
    });
  }

  public static Task<object?> ListPointGroupsAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var groups = new List<object>();
      var pointGroupIds = civilDoc.PointGroups;

      foreach (ObjectId id in pointGroupIds)
      {
        var group = tr.GetObject(id, OpenMode.ForRead) as PointGroup;
        if (group == null) continue;

        groups.Add(new
        {
          name = group.Name,
          handle = group.Handle.ToString(),
          pointCount = group.PointsCount,
          description = group.Description,
        });
      }

      return new { groups };
    });
  }

  // ─────────────────────────────────────────────
  // import/export de puntos en texto plano (Mes 8, S3): no depende de
  // LandXML ni de ningún API especial — solo I/O de archivo + civilDoc.CogoPoints
  // (ya usado en createCogoPoints). Formato soportado: PNEZD o PENZD,
  // delimitado por coma, un punto por línea.
  // ─────────────────────────────────────────────
  public static Task<object?> ImportCogoPointsAsync(JsonObject? parameters)
  {
    var filePath = PluginRuntime.GetRequiredString(parameters, "filePath");
    var format = (PluginRuntime.GetOptionalString(parameters, "format") ?? "PNEZD").ToUpperInvariant();

    if (format != "PNEZD" && format != "PENZD")
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Unsupported format '{format}'. Expected 'PNEZD' or 'PENZD'.");

    if (!File.Exists(filePath))
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"File not found: '{filePath}'.");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var cogoPoints = civilDoc.CogoPoints;
      var created = new List<object>();

      foreach (var rawLine in File.ReadAllLines(filePath))
      {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith("#")) continue;

        var parts = line.Split(',');
        if (parts.Length < 4) continue;

        double northing, easting;
        if (format == "PNEZD")
        {
          northing = double.Parse(parts[1]);
          easting = double.Parse(parts[2]);
        }
        else
        {
          easting = double.Parse(parts[1]);
          northing = double.Parse(parts[2]);
        }
        var elevation = double.Parse(parts[3]);
        var description = parts.Length > 4 ? string.Join(",", parts, 4, parts.Length - 4).Trim() : "";

        var pointId = cogoPoints.Add(new Point3d(easting, northing, elevation), false);
        var point = tr.GetObject(pointId, OpenMode.ForWrite) as CogoPoint;
        if (point != null && !string.IsNullOrEmpty(description))
          point.RawDescription = description;

        created.Add(new { pointNumber = point?.PointNumber ?? 0, easting, northing, elevation, description });
      }

      return new { success = true, filePath, format, importedCount = created.Count, created };
    });
  }

  public static Task<object?> ExportCogoPointsAsync(JsonObject? parameters)
  {
    var filePath = PluginRuntime.GetRequiredString(parameters, "filePath");
    var format = (PluginRuntime.GetOptionalString(parameters, "format") ?? "PNEZD").ToUpperInvariant();
    var groupName = PluginRuntime.GetOptionalString(parameters, "groupName");

    if (format != "PNEZD" && format != "PENZD")
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Unsupported format '{format}'. Expected 'PNEZD' or 'PENZD'.");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      PointGroup? group = groupName != null ? FindPointGroupByName(civilDoc, tr, groupName) : null;

      using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
      var exportedCount = 0;

      foreach (ObjectId id in civilDoc.CogoPoints)
      {
        var point = tr.GetObject(id, OpenMode.ForRead) as CogoPoint;
        if (point == null) continue;
        if (group != null && !group.ContainsPoint(point.PointNumber)) continue;

        var line = format == "PNEZD"
          ? $"{point.PointNumber},{point.Northing},{point.Easting},{point.Elevation},{point.RawDescription}"
          : $"{point.PointNumber},{point.Easting},{point.Northing},{point.Elevation},{point.RawDescription}";

        writer.WriteLine(line);
        exportedCount++;
      }

      return new { success = true, filePath, format, exportedCount };
    });
  }

  public static Task<object?> CreatePointGroupAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var groupId = civilDoc.PointGroups.Add(name);
      var group = tr.GetObject(groupId, OpenMode.ForRead) as PointGroup;

      return new
      {
        success = true,
        name,
        handle = group?.Handle.ToString(),
      };
    });
  }

  public static Task<object?> DeletePointGroupAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var group = FindPointGroupByName(civilDoc, tr, name);
      group.UpgradeOpen();
      group.Erase();

      return new { success = true, deleted = name };
    });
  }

  public static Task<object?> UpdatePointGroupAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var description = PluginRuntime.GetOptionalString(parameters, "description");
    var includeNumbers = PluginRuntime.GetOptionalString(parameters, "includeNumbers");
    var excludeNumbers = PluginRuntime.GetOptionalString(parameters, "excludeNumbers");
    var includeDescriptions = PluginRuntime.GetOptionalString(parameters, "includeDescriptions");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var group = FindPointGroupByName(civilDoc, tr, name);
      group.UpgradeOpen();

      if (description != null) group.Description = description;

      if (includeNumbers != null || excludeNumbers != null || includeDescriptions != null)
      {
        var existingQuery = group.GetQuery();
        if (existingQuery is not null && existingQuery is not StandardPointGroupQuery)
        {
          throw new JsonRpcDispatchException(
            "CIVIL3D.CONFLICT",
            $"Point group '{name}' uses a custom query. Standard filters were not applied because doing so would discard its existing QueryString criteria.");
        }

        var query = existingQuery as StandardPointGroupQuery ?? new StandardPointGroupQuery();
        if (includeNumbers != null) query.IncludeNumbers = includeNumbers;
        if (excludeNumbers != null) query.ExcludeNumbers = excludeNumbers;
        if (includeDescriptions != null) query.IncludeRawDescriptions = includeDescriptions;
        group.SetQuery(query);
        group.Update();
      }

      return new { success = true, name, updated = true };
    });
  }

  public static Task<object?> TransformCogoPointsAsync(JsonObject? parameters)
  {
    var pointNumbersNode = parameters?["pointNumbers"] as JsonArray;
    var groupName = PluginRuntime.GetOptionalString(parameters, "groupName");
    var translateX = PluginRuntime.GetOptionalDouble(parameters, "translateX") ?? 0;
    var translateY = PluginRuntime.GetOptionalDouble(parameters, "translateY") ?? 0;
    var translateZ = PluginRuntime.GetOptionalDouble(parameters, "translateZ") ?? 0;
    var rotateRadians = PluginRuntime.GetOptionalDouble(parameters, "rotateRadians") ?? 0;
    var scaleFactor = PluginRuntime.GetOptionalDouble(parameters, "scaleFactor") ?? 1.0;

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      HashSet<uint>? targetNumbers = null;

      if (pointNumbersNode != null && pointNumbersNode.Count > 0)
      {
        targetNumbers = new HashSet<uint>(pointNumbersNode.Select(n => (uint)(n?.GetValue<int>() ?? 0)).Where(n => n > 0));
      }
      else if (!string.IsNullOrWhiteSpace(groupName))
      {
        var group = FindPointGroupByName(civilDoc, tr, groupName!);
        targetNumbers = new HashSet<uint>();
        foreach (ObjectId id in civilDoc.CogoPoints)
        {
          var candidate = tr.GetObject(id, OpenMode.ForRead) as CogoPoint;
          if (candidate != null && group.ContainsPoint(candidate.PointNumber))
          {
            targetNumbers.Add(candidate.PointNumber);
          }
        }
      }

      var transformedCount = 0;
      var sinRot = Math.Sin(rotateRadians);
      var cosRot = Math.Cos(rotateRadians);

      foreach (ObjectId id in civilDoc.CogoPoints)
      {
        var point = tr.GetObject(id, OpenMode.ForRead) as CogoPoint;
        if (point == null) continue;
        if (targetNumbers != null && !targetNumbers.Contains(point.PointNumber)) continue;

        var x = point.Easting;
        var y = point.Northing;
        var z = point.Elevation;

        x *= scaleFactor;
        y *= scaleFactor;

        if (rotateRadians != 0)
        {
          var rx = x * cosRot - y * sinRot;
          var ry = x * sinRot + y * cosRot;
          x = rx;
          y = ry;
        }

        x += translateX;
        y += translateY;
        z += translateZ;

        point.UpgradeOpen();
        try
        {
          point.Easting = x;
          point.Northing = y;
          point.Elevation = z;
        }
        catch (Exception exception)
        {
          throw new JsonRpcDispatchException(
            "CIVIL3D.API_ERROR",
            $"COGO point {point.PointNumber} could not be moved: {exception.Message}");
        }
        transformedCount++;
      }

      return new Dictionary<string, object?>
      {
        ["transformedCount"] = transformedCount,
        ["translateX"] = translateX,
        ["translateY"] = translateY,
        ["translateZ"] = translateZ,
        ["rotateRadians"] = rotateRadians,
        ["scaleFactor"] = scaleFactor,
      };
    });
  }

  // ─────────────────────────────────────────────
  // Description Key Sets: el PDF los menciona como parte de "Puntos COGO
  // completos", pero la ruta de acceso real del API (posiblemente vía
  // civilDoc.Styles o un manager dedicado) no se puede confirmar sin una
  // sesión Civil3D real. Se deja como stub explícito en vez de adivinar.
  // ─────────────────────────────────────────────
  public static Task<object?> GetDescriptionKeySetsAsync(JsonObject? parameters)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Description Key Set access needs its real API path confirmed against a live Civil 3D drawing."
    });

  private static PointGroup FindPointGroupByName(dynamic civilDoc, Transaction tr, string name)
  {
    var ids = ((System.Collections.IEnumerable)civilDoc.PointGroups).Cast<ObjectId>();
    return CivilObjectLookup.FindByName<PointGroup>(ids, tr, name);
  }
}
