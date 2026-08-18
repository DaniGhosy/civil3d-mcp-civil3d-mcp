using System.Text.Json.Nodes;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;

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
  // Volumen real vía TinVolumeSurface (confirmado por este mismo repo en
  // CostEstimationCommands.cs, Mes 9 — el stub anterior estaba desactualizado).
  // ─────────────────────────────────────────────
  public static Task<object?> ComputeSurfaceVolumeAsync(JsonObject? parameters)
  {
    var baseName = PluginRuntime.GetRequiredString(parameters, "baseSurface");
    var compName = PluginRuntime.GetRequiredString(parameters, "comparisonSurface");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var baseSurface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, baseName, OpenMode.ForRead);
      var compSurface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, compName, OpenMode.ForRead);
      var volumeProperties = GetVolumeProperties(tr, baseSurface, compSurface);

      return new
      {
        baseSurface = baseName,
        comparisonSurface = compName,
        cutVolume = volumeProperties.UnadjustedCutVolume,
        fillVolume = volumeProperties.UnadjustedFillVolume,
        netVolume = volumeProperties.UnadjustedNetVolume,
      };
    });
  }

  // ─── calculateSurfaceVolume (portado de Civil3D-mcp-main) ──────────────────

  public static Task<object?> CalculateSurfaceVolumeAsync(JsonObject? parameters)
  {
    var baseSurfaceName = PluginRuntime.GetRequiredString(parameters, "baseSurface");
    var comparisonSurfaceName = PluginRuntime.GetRequiredString(parameters, "comparisonSurface");
    var method = PluginRuntime.GetOptionalString(parameters, "method") ?? "tin_volume";

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var baseSurface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, baseSurfaceName, OpenMode.ForRead);
      var comparisonSurface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, comparisonSurfaceName, OpenMode.ForRead);
      var volumeProperties = GetVolumeProperties(tr, baseSurface, comparisonSurface);
      var units = CivilObjectUtils.LinearUnits(db);

      return new Dictionary<string, object?>
      {
        ["baseSurface"] = baseSurfaceName,
        ["comparisonSurface"] = comparisonSurfaceName,
        ["cutVolume"] = volumeProperties.UnadjustedCutVolume,
        ["fillVolume"] = volumeProperties.UnadjustedFillVolume,
        ["netVolume"] = volumeProperties.UnadjustedNetVolume,
        ["method"] = method,
        ["units"] = new Dictionary<string, object?> { ["volume"] = $"{units}^3", ["area"] = $"{units}^2" },
      };
    });
  }

  // ─── getSurfaceVolumeReport (portado de Civil3D-mcp-main) ──────────────────

  public static Task<object?> GetSurfaceVolumeReportAsync(JsonObject? parameters)
  {
    var baseSurfaceName = PluginRuntime.GetRequiredString(parameters, "baseSurface");
    var comparisonSurfaceName = PluginRuntime.GetRequiredString(parameters, "comparisonSurface");
    var format = PluginRuntime.GetOptionalString(parameters, "format") ?? "summary";

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var baseSurface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, baseSurfaceName, OpenMode.ForRead);
      var comparisonSurface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, comparisonSurfaceName, OpenMode.ForRead);
      var volumeProperties = GetVolumeProperties(tr, baseSurface, comparisonSurface);
      var units = CivilObjectUtils.LinearUnits(db);

      var cut = volumeProperties.UnadjustedCutVolume;
      var fill = volumeProperties.UnadjustedFillVolume;
      var net = volumeProperties.UnadjustedNetVolume;

      var lines = new List<string>
      {
        "Surface Volume Report",
        "====================",
        $"Base Surface:       {baseSurfaceName}",
        $"Comparison Surface: {comparisonSurfaceName}",
        "",
        $"Cut Volume:  {cut:F3} {units}^3",
        $"Fill Volume: {fill:F3} {units}^3",
        $"Net Volume:  {net:F3} {units}^3",
      };

      if (format == "detailed")
      {
        lines.Add("");
        lines.Add($"Net Balance: {(net >= 0 ? "Cut exceeds fill" : "Fill exceeds cut")} by {Math.Abs(net):F3} {units}^3");
        lines.Add($"Cut/Fill Ratio: {(fill > 0 ? (cut / fill).ToString("F3") : "N/A")}");
      }

      return new Dictionary<string, object?>
      {
        ["baseSurface"] = baseSurfaceName,
        ["comparisonSurface"] = comparisonSurfaceName,
        ["format"] = format,
        ["report"] = string.Join("\n", lines),
        ["volumes"] = new Dictionary<string, object?> { ["cut"] = cut, ["fill"] = fill, ["net"] = net },
        ["units"] = new Dictionary<string, object?> { ["volume"] = $"{units}^3", ["area"] = $"{units}^2" },
      };
    });
  }

  // ─── calculateSurfaceVolumeByRegion (portado de Civil3D-mcp-main) ──────────

  public static Task<object?> CalculateSurfaceVolumeByRegionAsync(JsonObject? parameters)
  {
    var baseSurfaceName = PluginRuntime.GetRequiredString(parameters, "baseSurface");
    var comparisonSurfaceName = PluginRuntime.GetRequiredString(parameters, "comparisonSurface");
    var boundary = ParsePoint2dArray(parameters, "boundary", "calculateSurfaceVolumeByRegion requires at least 3 boundary points.");
    if (boundary.Count < 3)
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "calculateSurfaceVolumeByRegion requires at least 3 boundary points.");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var baseSurface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, baseSurfaceName, OpenMode.ForRead);
      var comparisonSurface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, comparisonSurfaceName, OpenMode.ForRead);
      var units = CivilObjectUtils.LinearUnits(db);

      var minX = boundary.Min(p => p.X);
      var maxX = boundary.Max(p => p.X);
      var minY = boundary.Min(p => p.Y);
      var maxY = boundary.Max(p => p.Y);
      var gridSpacing = Math.Max(maxX - minX, maxY - minY) / 50.0;
      if (gridSpacing < 0.01) gridSpacing = 0.01;

      double cutVolume = 0, fillVolume = 0, cutArea = 0, fillArea = 0;
      var cellArea = gridSpacing * gridSpacing;

      for (var x = minX + gridSpacing / 2; x < maxX; x += gridSpacing)
      {
        for (var y = minY + gridSpacing / 2; y < maxY; y += gridSpacing)
        {
          if (!IsPointInPolygon(x, y, boundary)) continue;

          double baseZ, compZ;
          try
          {
            baseZ = baseSurface.FindElevationAtXY(x, y);
            compZ = comparisonSurface.FindElevationAtXY(x, y);
          }
          catch { continue; }

          var diff = compZ - baseZ;
          if (diff > 0) { fillVolume += diff * cellArea; fillArea += cellArea; }
          else if (diff < 0) { cutVolume += Math.Abs(diff) * cellArea; cutArea += cellArea; }
        }
      }

      return new Dictionary<string, object?>
      {
        ["baseSurface"] = baseSurfaceName,
        ["comparisonSurface"] = comparisonSurfaceName,
        ["cutVolume"] = cutVolume,
        ["fillVolume"] = fillVolume,
        ["netVolume"] = fillVolume - cutVolume,
        ["cutArea"] = cutArea,
        ["fillArea"] = fillArea,
        ["regionBoundaryPointCount"] = boundary.Count,
        ["units"] = new Dictionary<string, object?> { ["volume"] = $"{units}^3", ["area"] = $"{units}^2" },
      };
    });
  }

  // ─── analyzeSurfaceSlope / Elevation / Directions (portado de Civil3D-mcp-main) ──

  public static Task<object?> AnalyzeSurfaceSlopeAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var requestedRanges = (parameters?["numRanges"] as JsonNode)?.GetValue<int>();
    var rangesNode = parameters?["ranges"] as JsonArray;

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, name, OpenMode.ForRead);
      var analysisData = surface.Analysis.GetSlopeData();
      if (analysisData.Length == 0)
        throw new JsonRpcDispatchException("CIVIL3D.API_ERROR", $"Surface '{name}' has no stored slope analysis. Generate slope ranges on the Surface Properties Analysis tab, then retry.");

      var slopeBands = analysisData.Select((range, index) => new Dictionary<string, object?>
      {
        ["rangeIndex"] = index,
        ["minPercent"] = range.MinimumSlope * 100.0,
        ["maxPercent"] = range.MaximumSlope * 100.0,
        ["color"] = range.Scheme.ColorName,
      }).ToList();

      return new Dictionary<string, object?>
      {
        ["surfaceName"] = name,
        ["analysisType"] = "slope",
        ["numRanges"] = slopeBands.Count,
        ["requestedRanges"] = requestedRanges,
        ["requestedCustomRanges"] = rangesNode?.Count,
        ["slopeBands"] = slopeBands,
        ["units"] = new Dictionary<string, object?> { ["slope"] = "percent" },
        ["note"] = "These are the exact slope ranges stored by Civil 3D. The managed API does not report area or percent-of-surface for each range.",
      };
    });
  }

  public static Task<object?> AnalyzeSurfaceElevationAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var numRanges = (int?)(parameters?["numRanges"] as JsonNode)?.GetValue<int>() ?? 5;
    var rangesNode = parameters?["ranges"] as JsonArray;

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, name, OpenMode.ForRead);
      var generalProperties = surface.GetGeneralProperties();
      var units = CivilObjectUtils.LinearUnits(db);
      var analysisData = surface.Analysis.GetElevationData();
      if (analysisData.Length == 0)
        throw new JsonRpcDispatchException("CIVIL3D.API_ERROR", $"Surface '{name}' has no stored elevation analysis. Generate elevation ranges on the Surface Properties Analysis tab, then retry.");

      var elevBands = analysisData.Select((range, index) => new Dictionary<string, object?>
      {
        ["rangeIndex"] = index,
        ["minElevation"] = range.MinimumElevation,
        ["maxElevation"] = range.MaximumElevation,
        ["color"] = range.Scheme.ColorName,
      }).ToList();

      return new Dictionary<string, object?>
      {
        ["surfaceName"] = name,
        ["analysisType"] = "elevation",
        ["numRanges"] = elevBands.Count,
        ["requestedRanges"] = numRanges,
        ["requestedCustomRanges"] = rangesNode?.Count,
        ["overallMin"] = generalProperties.MinimumElevation,
        ["overallMax"] = generalProperties.MaximumElevation,
        ["elevationBands"] = elevBands,
        ["units"] = new Dictionary<string, object?> { ["elevation"] = units },
        ["note"] = "These are the exact elevation ranges stored by Civil 3D. The managed API does not report area or percent-of-surface for each range.",
      };
    });
  }

  public static Task<object?> AnalyzeSurfaceDirectionsAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var requestedRanges = (int?)(parameters?["numRanges"] as JsonNode)?.GetValue<int>();

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, name, OpenMode.ForRead);
      var analysisData = surface.Analysis.GetDirectionData();
      if (analysisData.Length == 0)
        throw new JsonRpcDispatchException("CIVIL3D.API_ERROR", $"Surface '{name}' has no stored direction analysis. Generate direction ranges on the Surface Properties Analysis tab, then retry.");

      var directionBands = analysisData.Select((band, index) => new Dictionary<string, object?>
      {
        ["sectorIndex"] = index,
        ["startAngle"] = band.MinimumDirection * 180.0 / Math.PI,
        ["endAngle"] = band.MaximumDirection * 180.0 / Math.PI,
        ["color"] = band.Scheme.ColorName,
      }).ToList();

      return new Dictionary<string, object?>
      {
        ["surfaceName"] = name,
        ["analysisType"] = "directions",
        ["numSectors"] = directionBands.Count,
        ["requestedSectors"] = requestedRanges,
        ["directionBands"] = directionBands,
        ["units"] = new Dictionary<string, object?> { ["angle"] = "degrees" },
        ["note"] = "These are the exact direction ranges stored by Civil 3D. The managed API does not report area or percent-of-surface for each range.",
      };
    });
  }

  // ─── addSurfaceWatershed (portado de Civil3D-mcp-main) ─────────────────────

  public static Task<object?> AddSurfaceWatershedsAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var depthThreshold = parameters?["depthThreshold"] is JsonNode dt ? dt.GetValue<double>() : 0.1;
    var mergeAdjacent = parameters?["mergeAdjacentWatersheds"] is JsonNode ma && ma.GetValue<bool>();

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, name, OpenMode.ForWrite);

      var watershedsAdded = false;
      var watershedCount = 0;

      foreach (var methodName in new[] { "AddWatersheds", "CreateWatersheds", "ComputeWatersheds" })
      {
        var result = CivilObjectUtils.InvokeMethod(surface, methodName, depthThreshold);
        if (result != null)
        {
          watershedsAdded = true;
          watershedCount = result is int count ? count : 1;
          break;
        }
      }

      if (!watershedsAdded)
      {
        var watershedsProperty = CivilObjectUtils.GetPropertyValue<object>(surface, "Watersheds");
        if (watershedsProperty != null)
        {
          CivilObjectUtils.InvokeMethod(watershedsProperty, "Add", depthThreshold);
          watershedsAdded = true;
        }
      }

      surface.Rebuild();

      return new Dictionary<string, object?>
      {
        ["surfaceName"] = name,
        ["depthThreshold"] = depthThreshold,
        ["mergeAdjacentWatersheds"] = mergeAdjacent,
        ["watershedsAdded"] = watershedsAdded,
        ["watershedCount"] = watershedCount,
        ["status"] = watershedsAdded ? "Watershed analysis added successfully" : "Watershed analysis may require manual configuration in Civil 3D",
      };
    });
  }

  // ─── setSurfaceContourInterval (portado de Civil3D-mcp-main) ───────────────

  public static Task<object?> SetSurfaceContourIntervalAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var minorInterval = PluginRuntime.GetRequiredDouble(parameters, "minorInterval");
    var majorInterval = PluginRuntime.GetRequiredDouble(parameters, "majorInterval");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, name, OpenMode.ForWrite);

      var styleId = surface.StyleId;
      var style = CivilObjectUtils.GetRequiredObject<SurfaceStyle>(tr, styleId, OpenMode.ForWrite);
      style.ContourStyle.MinorContourInterval = minorInterval;
      style.ContourStyle.MajorContourInterval = majorInterval;

      return new Dictionary<string, object?>
      {
        ["surfaceName"] = name,
        ["minorInterval"] = minorInterval,
        ["majorInterval"] = majorInterval,
        ["applied"] = true,
        ["status"] = $"Contour intervals set: minor={minorInterval}, major={majorInterval}",
      };
    });
  }

  // ─── getSurfaceStatisticsDetailed (portado de Civil3D-mcp-main) ────────────

  public static Task<object?> GetSurfaceStatisticsDetailedAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, name, OpenMode.ForRead);
      var generalProperties = surface.GetGeneralProperties();
      var terrainProperties = GetTerrainProperties(surface);
      var units = CivilObjectUtils.LinearUnits(db);

      return new Dictionary<string, object?>
      {
        ["surfaceName"] = surface.Name,
        ["minimumElevation"] = generalProperties.MinimumElevation,
        ["maximumElevation"] = generalProperties.MaximumElevation,
        ["meanElevation"] = generalProperties.MeanElevation,
        ["area2d"] = terrainProperties?.SurfaceArea2D,
        ["area3d"] = terrainProperties?.SurfaceArea3D,
        ["numberOfPoints"] = generalProperties.NumberOfPoints,
        ["numberOfTriangles"] = surface is TinSurface tinSurface ? tinSurface.GetTinProperties().NumberOfTriangles : (int?)null,
        ["units"] = new Dictionary<string, object?> { ["horizontal"] = units, ["vertical"] = units, ["area"] = $"{units}^2" },
      };
    });
  }

  // ─── sampleSurfaceElevations (portado de Civil3D-mcp-main) ─────────────────

  public static Task<object?> SampleSurfaceElevationsAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var method = PluginRuntime.GetRequiredString(parameters, "method");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var surface = CivilObjectUtils.FindSurfaceByName(civilDoc, tr, name, OpenMode.ForRead);
      var units = CivilObjectUtils.LinearUnits(db);
      var samples = new List<Dictionary<string, object?>>();

      if (method == "grid")
      {
        var gridSpacing = PluginRuntime.GetRequiredDouble(parameters, "gridSpacing");
        var extents = surface.GeometricExtents;

        var minX = extents.MinPoint.X;
        var maxX = extents.MaxPoint.X;
        var minY = extents.MinPoint.Y;
        var maxY = extents.MaxPoint.Y;

        var boundaryNode = parameters?["boundary"] as JsonArray;
        var boundaryPoints = boundaryNode != null
          ? boundaryNode.OfType<JsonObject>().Select(p => new Point2d(p["x"]!.GetValue<double>(), p["y"]!.GetValue<double>())).ToList()
          : (List<Point2d>?)null;

        for (var x = minX; x <= maxX; x += gridSpacing)
        {
          for (var y = minY; y <= maxY; y += gridSpacing)
          {
            if (boundaryPoints != null && !IsPointInPolygon(x, y, boundaryPoints)) continue;

            double elevation;
            try { elevation = surface.FindElevationAtXY(x, y); }
            catch { continue; }

            samples.Add(new Dictionary<string, object?> { ["x"] = x, ["y"] = y, ["elevation"] = elevation });
          }
        }
      }
      else if (method == "points")
      {
        var pointsNode = parameters?["points"] as JsonArray
          ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "sampleSurfaceElevations with method=points requires 'points' array.");

        foreach (var pointNode in pointsNode.OfType<JsonObject>())
        {
          var x = pointNode["x"]!.GetValue<double>();
          var y = pointNode["y"]!.GetValue<double>();
          double elevation;
          try { elevation = surface.FindElevationAtXY(x, y); }
          catch { continue; }

          samples.Add(new Dictionary<string, object?> { ["x"] = x, ["y"] = y, ["elevation"] = elevation });
        }
      }
      else if (method == "transect")
      {
        var startNode = parameters?["startPoint"] as JsonObject
          ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "sampleSurfaceElevations with method=transect requires 'startPoint'.");
        var endNode = parameters?["endPoint"] as JsonObject
          ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "sampleSurfaceElevations with method=transect requires 'endPoint'.");
        var numSamples = (int?)(parameters?["numSamples"] as JsonNode)?.GetValue<int>() ?? 50;
        if (numSamples < 2) numSamples = 2;

        var x0 = startNode["x"]!.GetValue<double>();
        var y0 = startNode["y"]!.GetValue<double>();
        var x1 = endNode["x"]!.GetValue<double>();
        var y1 = endNode["y"]!.GetValue<double>();
        var totalLength = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));

        for (var i = 0; i < numSamples; i++)
        {
          var t = (double)i / (numSamples - 1);
          var x = x0 + t * (x1 - x0);
          var y = y0 + t * (y1 - y0);
          double elevation;
          try { elevation = surface.FindElevationAtXY(x, y); }
          catch { continue; }

          samples.Add(new Dictionary<string, object?> { ["x"] = x, ["y"] = y, ["station"] = t * totalLength, ["elevation"] = elevation });
        }
      }
      else
      {
        throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Unknown sampling method '{method}'. Use 'grid', 'points', or 'transect'.");
      }

      return new Dictionary<string, object?>
      {
        ["surfaceName"] = name,
        ["method"] = method,
        ["sampleCount"] = samples.Count,
        ["samples"] = samples,
        ["units"] = new Dictionary<string, object?> { ["horizontal"] = units, ["vertical"] = units },
      };
    });
  }

  // ─── createSurfaceFromDem (portado de Civil3D-mcp-main) ────────────────────

  public static Task<object?> CreateSurfaceFromDemAsync(JsonObject? parameters)
  {
    var filePath = FileBoundary.ResolveImportPath(
      PluginRuntime.GetRequiredString(parameters, "filePath"),
      ".dem", ".tif", ".tiff", ".asc", ".adf");
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var style = PluginRuntime.GetOptionalString(parameters, "style");
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");
    var description = PluginRuntime.GetOptionalString(parameters, "description");
    var coordinateSystem = PluginRuntime.GetOptionalString(parameters, "coordinateSystem");

    if (!string.IsNullOrWhiteSpace(coordinateSystem))
    {
      throw new JsonRpcDispatchException(
        "CIVIL3D.API_ERROR",
        "createSurfaceFromDem cannot assign a coordinate system through SurfaceDefinitionDEMFiles.AddDEMFile. " +
        "Assign the drawing coordinate system explicitly before importing the DEM.");
    }

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var styleId = LookupUtils.GetSurfaceStyleId(civilDoc, tr, style);

      var surfaceId = TinSurface.Create(name, styleId);
      var surface = CivilObjectUtils.GetRequiredObject<TinSurface>(tr, surfaceId, OpenMode.ForWrite);
      try
      {
        surface.DEMFilesDefinition.AddDEMFile(filePath);
      }
      catch (Exception exception)
      {
        surface.Erase();
        throw new JsonRpcDispatchException("CIVIL3D.TRANSACTION_FAILED", $"Unable to import DEM file '{filePath}': {exception.Message}");
      }
      if (!string.IsNullOrWhiteSpace(layer)) surface.Layer = layer;
      if (!string.IsNullOrWhiteSpace(description)) surface.Description = description;
      surface.Rebuild();

      return new Dictionary<string, object?>
      {
        ["name"] = surface.Name,
        ["handle"] = CivilObjectUtils.GetHandle(surface),
        ["filePath"] = filePath,
        ["created"] = true,
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

        var pts = ReadEntityVertices(ent, tr);
        if (pts.Count < 3) continue; // ni polilínea soportada ni suficientes vértices

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
        WriteCsv(outputCsvPath, "Elevacion_m;Area_m2;Handle",
          rows.Select(r => $"{r.elevation:F3};{r.area:F4};{r.handle}"),
          new[] { $"Volumen_trapezoidal_m3;{totalVolume:F4}", $"Volumen_metodo_simple_m3;{simpleVolume:F4}" });
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
  var forceMinElev = PluginRuntime.GetOptionalDouble(parameters, "forceMinElevation");
  var forceMaxElev = PluginRuntime.GetOptionalDouble(parameters, "forceMaxElevation");
  var forceTramo = PluginRuntime.GetOptionalString(parameters, "forceTramo"); // "tramo_corto" o "tramo_largo"
  var closeMethod = PluginRuntime.GetOptionalString(parameters, "closeMethod") ?? "boundary"; // "boundary", "straight" o "fixedWindow"
  var fixedPoint1X = PluginRuntime.GetOptionalDouble(parameters, "fixedPoint1X");
  var fixedPoint1Y = PluginRuntime.GetOptionalDouble(parameters, "fixedPoint1Y");
  var fixedPoint2X = PluginRuntime.GetOptionalDouble(parameters, "fixedPoint2X");
  var fixedPoint2Y = PluginRuntime.GetOptionalDouble(parameters, "fixedPoint2Y");

  return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
  {
    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

    // ── 0. Asegurar que exista la capa de salida ──
    GenericObjectCommands.EnsureLayerId(db, tr, outputLayerName);

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

        double area;
        string chosen;
        List<Point3d> finalPolygon;

        bool overrideAplica = forceMinElev.HasValue && forceMaxElev.HasValue &&
                               elev >= forceMinElev.Value && elev <= forceMaxElev.Value &&
                               !string.IsNullOrEmpty(forceTramo);

        if (closeMethod == "fixedWindow" && fixedPoint1X.HasValue && fixedPoint1Y.HasValue &&
            fixedPoint2X.HasValue && fixedPoint2Y.HasValue)
        {
          // Usa siempre el mismo tramo fijo del boundary, definido por dos
          // puntos de coordenadas dados por el usuario, en vez de dejar
          // que el algoritmo decida automáticamente (que falla en formas
          // de boundary irregulares tipo horquilla).
          var fixedP1 = new Point3d(fixedPoint1X.Value, fixedPoint1Y.Value, 0);
          var fixedP2 = new Point3d(fixedPoint2X.Value, fixedPoint2Y.Value, 0);

          double sFixed1 = ProjectOntoBoundary(fixedP1, boundaryPts, boundaryArc);
          double sFixed2 = ProjectOntoBoundary(fixedP2, boundaryPts, boundaryArc);

          var fixedSegment = GetBoundaryArcBetween(boundaryPts, boundaryArc, sFixed1, sFixed2);

          finalPolygon = new List<Point3d>(chain);
          finalPolygon.AddRange(fixedSegment);
          area = ShoelaceArea(finalPolygon);
          chosen = "ventana_fija";
        }
        else if (closeMethod == "straight")
        {
          // Cierre directo: conecta los dos extremos abiertos con una
          // línea recta, sin depender del boundary. Útil cuando el
          // boundary tiene una forma irregular (horquilla) que confunde
          // la elección automática de tramo corto/largo.
          finalPolygon = new List<Point3d>(chain);
          area = ShoelaceArea(finalPolygon);
          chosen = "cierre_directo";
        }
        else if (overrideAplica && forceTramo == "tramo_largo")
        {
          area = areaLong;
          chosen = "tramo_largo (forzado)";
          finalPolygon = polyLong;
        }
        else if (overrideAplica && forceTramo == "tramo_corto")
        {
          area = areaShort;
          chosen = "tramo_corto (forzado)";
          finalPolygon = polyShort;
        }
        else
        {
          // Criterio principal: preferir el tramo cuyo polígono cerrado resultante
          // NO se autointerseca (polígono simple) — el área mínima por sí sola falla
          // en boundaries irregulares tipo horquilla (el hairpin de sotavento), donde
          // el tramo geométricamente correcto no siempre es el de menor área.
          bool shortIsSimple = IsSimplePolygon(polyShort);
          bool longIsSimple = IsSimplePolygon(polyLong);

          if (shortIsSimple && !longIsSimple)
          {
            area = areaShort;
            chosen = "tramo_corto (simple)";
            finalPolygon = polyShort;
          }
          else if (longIsSimple && !shortIsSimple)
          {
            area = areaLong;
            chosen = "tramo_largo (simple)";
            finalPolygon = polyLong;
          }
          else
          {
            // Ambos son simples o ambos se autointersecan: desempate por área mínima,
            // igual que antes.
            area = Math.Min(areaShort, areaLong);
            chosen = areaShort <= areaLong ? "tramo_corto (area)" : "tramo_largo (area)";
            finalPolygon = areaShort <= areaLong ? polyShort : polyLong;
          }
        }

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
      WriteCsv(outputCsvPath, "Elevacion_m;Area_m2;Origen",
        resultRows.Select(r => { dynamic d = r; return $"{d.elevation:F3};{d.areaSqM:F4};{d.source}"; }));
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

// ─────────────────────────────────────────────
// Edición de definición (Mes 3.5 — techo de TinSurface, pedido explícito del
// usuario). delete_points y los accesos a triángulos están confirmados por
// investigación real (ver plan). paste_surface, get_operations, delete_boundary
// y build options son intentos de mejor esfuerzo — build-verify único, se
// documenta si el compilador los rechaza.
// ─────────────────────────────────────────────

// delete_points: TinSurface.DeleteVertices(...) confirmado real (código
// funcional citado en foro de Autodesk). Sin "points" borra TODOS los vértices
// (caso 100% confirmado); con "points" filtra por cercanía XY dentro de
// "tolerance" antes de borrar (extensión razonable, no verificada aparte).
public static Task<object?> DeleteSurfacePointsAsync(JsonObject? parameters)
{
  var name = PluginRuntime.GetRequiredString(parameters, "name");
  var pointsNode = parameters?["points"] as JsonArray;
  var tolerance = PluginRuntime.GetOptionalDouble(parameters, "tolerance") ?? 0.01;

  return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
  {
    var surface = FindSurfaceByName(civilDoc, tr, name) as TinSurface
      ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Not a TIN surface");

    surface.UpgradeOpen();

    var toDelete = new List<TinSurfaceVertex>();

    if (pointsNode != null && pointsNode.Count > 0)
    {
      var targets = new List<(double x, double y)>();
      foreach (var pt in pointsNode)
        targets.Add((pt!["x"]!.GetValue<double>(), pt["y"]!.GetValue<double>()));

      foreach (TinSurfaceVertex vertex in surface.Vertices)
      {
        var loc = vertex.Location;
        if (targets.Any(t => Math.Abs(t.x - loc.X) <= tolerance && Math.Abs(t.y - loc.Y) <= tolerance))
          toDelete.Add(vertex);
      }
    }
    else
    {
      foreach (TinSurfaceVertex vertex in surface.Vertices)
        toDelete.Add(vertex);
    }

    // Civil3D exige una lista no vacía ("The vertices can't be empty") — confirmado en pruebas
    // en vivo que el filtro XY puede no encontrar coincidencias (ej. contra un punto recién
    // agregado sin rebuild), así que se evita la llamada en vez de dejarla fallar.
    if (toDelete.Count == 0)
      return new
      {
        success = true,
        surfaceName = name,
        deletedCount = 0,
        note = "No vertices matched the given XY filter (within tolerance)."
      };

    surface.DeleteVertices(toDelete);

    return new { success = true, surfaceName = name, deletedCount = toDelete.Count };
  });
}

// list_triangles: mismo patrón (triangle.VertexN.Location) ya probado y
// funcionando en GetSurfaceAreaElevationTableAsync de este mismo archivo.
public static Task<object?> ListSurfaceTrianglesAsync(JsonObject? parameters)
{
  var name = PluginRuntime.GetRequiredString(parameters, "name");
  var limit = PluginRuntime.GetOptionalInt(parameters, "limit") ?? 500;

  return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
  {
    var surface = FindSurfaceByName(civilDoc, tr, name) as TinSurface
      ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Not a TIN surface");

    var triangles = new List<object>();
    int total = 0;

    foreach (var triangle in surface.GetTriangles(false))
    {
      total++;
      if (triangles.Count >= limit) continue;

      triangles.Add(new
      {
        v1 = new { x = triangle.Vertex1.Location.X, y = triangle.Vertex1.Location.Y, z = triangle.Vertex1.Location.Z },
        v2 = new { x = triangle.Vertex2.Location.X, y = triangle.Vertex2.Location.Y, z = triangle.Vertex2.Location.Z },
        v3 = new { x = triangle.Vertex3.Location.X, y = triangle.Vertex3.Location.Y, z = triangle.Vertex3.Location.Z },
      });
    }

    return new { surfaceName = name, total, truncated = total > triangles.Count, triangles };
  });
}

// get_triangle_at_point: TinSurface.FindTriangleAtXY confirmado real (uso
// real citado en foro de Autodesk).
public static Task<object?> GetSurfaceTriangleAtPointAsync(JsonObject? parameters)
{
  var name = PluginRuntime.GetRequiredString(parameters, "name");
  var x = PluginRuntime.GetRequiredDouble(parameters, "x");
  var y = PluginRuntime.GetRequiredDouble(parameters, "y");

  return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
  {
    var surface = FindSurfaceByName(civilDoc, tr, name) as TinSurface
      ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Not a TIN surface");

    var triangle = surface.FindTriangleAtXY(x, y);
    if (triangle == null)
      throw new JsonRpcDispatchException("CIVIL3D.NOT_FOUND", $"No triangle found at ({x}, {y}) on surface '{name}'.");

    return new
    {
      surfaceName = name,
      x,
      y,
      v1 = new { x = triangle.Vertex1.Location.X, y = triangle.Vertex1.Location.Y, z = triangle.Vertex1.Location.Z },
      v2 = new { x = triangle.Vertex2.Location.X, y = triangle.Vertex2.Location.Y, z = triangle.Vertex2.Location.Z },
      v3 = new { x = triangle.Vertex3.Location.X, y = triangle.Vertex3.Location.Y, z = triangle.Vertex3.Location.Z },
    };
  });
}

// paste_surface: TinSurface.PasteSurface confirmado que existe (página
// oficial de Autodesk), firma exacta no verificable (503 en todos los
// intentos de lectura). Intento de mejor esfuerzo con un solo ObjectId.
public static Task<object?> PasteSurfaceAsync(JsonObject? parameters)
{
  var name = PluginRuntime.GetRequiredString(parameters, "name");
  var pasteSurfaceName = PluginRuntime.GetRequiredString(parameters, "pasteSurfaceName");

  return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
  {
    var surface = FindSurfaceByName(civilDoc, tr, name) as TinSurface
      ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Not a TIN surface");
    var pasteSurface = FindSurfaceByName(civilDoc, tr, pasteSurfaceName);

    surface.UpgradeOpen();
    surface.PasteSurface(pasteSurface.ObjectId);

    return new { success = true, surfaceName = name, pastedSurfaceName = pasteSurfaceName };
  });
}

// get_operations: TinSurface no contiene un método "GetOperations" —
// confirmado por el compilador. Se deja como stub en vez de seguir
// adivinando el nombre real del historial de build.
public static Task<object?> GetSurfaceOperationsAsync(JsonObject? parameters)
  => Task.FromResult<object?>(new
  {
    status = "planned",
    note = "TinSurface.GetOperations() does not exist — confirmed by the compiler. The real build-history accessor needs to be confirmed against a live Civil 3D drawing."
  });

// delete_boundary: TinSurface no contiene "DeleteBoundaries" — confirmado
// por el compilador. Stub en vez de seguir adivinando.
public static Task<object?> DeleteSurfaceBoundaryAsync(JsonObject? parameters)
  => Task.FromResult<object?>(new
  {
    status = "planned",
    note = "TinSurface.DeleteBoundaries() does not exist — confirmed by the compiler. The real boundary-removal member needs to be confirmed against a live Civil 3D drawing."
  });

// get_build_options: surface.BuildOptions SÍ existe (confirmado por el
// compilador) — se lee genéricamente por reflexión para exponer sus
// propiedades reales sin tener que adivinarlas una por una.
public static Task<object?> GetSurfaceBuildOptionsAsync(JsonObject? parameters)
{
  var name = PluginRuntime.GetRequiredString(parameters, "name");

  return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
  {
    var surface = FindSurfaceByName(civilDoc, tr, name) as TinSurface
      ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Not a TIN surface");

    return new
    {
      surfaceName = name,
      buildOptions = GenericObjectCommands.SerializeSimpleProperties(surface.BuildOptions!),
    };
  });
}

// set_build_options: "MinimumTriangleArea" no es el nombre real de la
// propiedad en SurfaceBuildOptions — confirmado por el compilador. Usa
// get_build_options (arriba) para ver los nombres reales de propiedad vía
// reflexión antes de intentar escribir uno específico.
public static Task<object?> SetSurfaceBuildOptionsAsync(JsonObject? parameters)
  => Task.FromResult<object?>(new
  {
    status = "planned",
    note = "SurfaceBuildOptions.MinimumTriangleArea does not exist under that name — confirmed by the compiler. Use get_build_options to see the real property names via reflection, then this can be implemented for the confirmed name."
  });

// ─────────────────────────────────────────────
// Stubs documentados: sin evidencia encontrada en la investigación de esta
// ronda (ver tabla del plan). No se adivina una firma a ciegas.
// ─────────────────────────────────────────────
public static Task<object?> AddSurfaceContourDataAsync(JsonObject? p)
  => Task.FromResult<object?>(new
  {
    status = "planned",
    note = "No real API member found for adding contour data as a definition source. Needs confirmation against a live Civil 3D drawing."
  });

public static Task<object?> SwapSurfaceEdgeAsync(JsonObject? p)
  => Task.FromResult<object?>(new
  {
    status = "planned",
    note = "No real API member found for swapping a TIN edge. Needs confirmation against a live Civil 3D drawing."
  });

public static Task<object?> MinimizeFlatTrianglesAsync(JsonObject? p)
  => Task.FromResult<object?>(new
  {
    status = "planned",
    note = "No real API member found for the 'minimize flat triangles by swapping edges' build option. Needs confirmation against a live Civil 3D drawing."
  });

public static Task<object?> MinimizeConvexTrianglesAsync(JsonObject? p)
  => Task.FromResult<object?>(new
  {
    status = "planned",
    note = "No real API member found for minimizing convex triangles. Needs confirmation against a live Civil 3D drawing."
  });

public static Task<object?> DeleteSurfaceBreaklineAsync(JsonObject? p)
  => Task.FromResult<object?>(new
  {
    status = "planned",
    note = "Autodesk's own documentation indicates breaklines that still define the surface cannot be cleanly removed via the API — this isn't a naming guess, the operation itself appears unsupported. Needs confirmation against a live Civil 3D drawing before attempting a workaround."
  });

public static Task<object?> DeleteSurfaceOperationAsync(JsonObject? p)
  => Task.FromResult<object?>(new
  {
    status = "planned",
    note = "No real API member found for removing a specific build operation from a surface's history. Needs confirmation against a live Civil 3D drawing."
  });

// ── Helpers ──

private static VolumeSurfaceProperties GetVolumeProperties(Transaction tr, CivilSurface baseSurface, CivilSurface comparisonSurface)
{
  var name = $"{baseSurface.Name}_{comparisonSurface.Name}_Volume_{Guid.NewGuid():N}";
  var surfaceId = TinVolumeSurface.Create(name, baseSurface.ObjectId, comparisonSurface.ObjectId);
  var volumeSurface = CivilObjectUtils.GetRequiredObject<TinVolumeSurface>(tr, surfaceId, OpenMode.ForRead);
  return volumeSurface.GetVolumeProperties();
}

private static bool IsPointInPolygon(double x, double y, List<Point2d> polygon)
{
  var count = polygon.Count;
  var inside = false;
  for (int i = 0, j = count - 1; i < count; j = i++)
  {
    var xi = polygon[i].X;
    var yi = polygon[i].Y;
    var xj = polygon[j].X;
    var yj = polygon[j].Y;
    if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
    {
      inside = !inside;
    }
  }
  return inside;
}

private static TerrainSurfaceProperties? GetTerrainProperties(CivilSurface surface) => surface switch
{
  TinSurface tinSurface => tinSurface.GetTerrainProperties(),
  GridSurface gridSurface => gridSurface.GetTerrainProperties(),
  _ => null,
};

private static List<Point2d> ParsePoint2dArray(JsonObject? parameters, string name, string errorMessage)
{
  var pointsNode = parameters?[name] as JsonArray;
  if (pointsNode == null || pointsNode.Count == 0)
    throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", errorMessage);

  var points = new List<Point2d>();
  foreach (var pointNode in pointsNode)
  {
    if (pointNode is not JsonObject point) continue;
    var x = point["x"]?.GetValue<double>() ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Point is missing x.");
    var y = point["y"]?.GetValue<double>() ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Point is missing y.");
    points.Add(new Point2d(x, y));
  }

  if (points.Count == 0)
    throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", errorMessage);

  return points;
}

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

private static void WriteCsv(string path, string header, IEnumerable<string> rows, IEnumerable<string>? trailer = null)
{
  using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
  writer.WriteLine(header);
  foreach (var row in rows) writer.WriteLine(row);

  if (trailer != null)
  {
    writer.WriteLine();
    foreach (var line in trailer) writer.WriteLine(line);
  }
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

// Orientación de la terna ordenada (a, b, c) en 2D: >0 antihorario, <0 horario, 0 colineal.
private static double Orientation2D(Point3d a, Point3d b, Point3d c)
  => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

private static bool SegmentsIntersect(Point3d p1, Point3d p2, Point3d p3, Point3d p4)
{
  double d1 = Orientation2D(p3, p4, p1);
  double d2 = Orientation2D(p3, p4, p2);
  double d3 = Orientation2D(p1, p2, p3);
  double d4 = Orientation2D(p1, p2, p4);

  return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
         ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
}

// Detecta si el polígono cerrado (implícito: último punto se une al primero) se
// autointerseca — usado para elegir el tramo de boundary correcto en boundaries
// irregulares tipo horquilla, donde el criterio de "área mínima" por sí solo elige
// mal (el hairpin de sotavento). Compara cada arista contra las no adyacentes.
private static bool IsSimplePolygon(List<Point3d> pts)
{
  int n = pts.Count;
  if (n < 4) return true;

  for (int i = 0; i < n; i++)
  {
    var a1 = pts[i];
    var a2 = pts[(i + 1) % n];

    for (int j = i + 1; j < n; j++)
    {
      // Aristas adyacentes (comparten un vértice) no cuentan como autointersección.
      if (j == i || (j + 1) % n == i) continue;

      var b1 = pts[j];
      var b2 = pts[(j + 1) % n];

      if (SegmentsIntersect(a1, a2, b1, b2)) return false;
    }
  }

  return true;
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
    var ids = (ObjectIdCollection)civilDoc.GetSurfaceIds();
    return CivilObjectLookup.FindByName<Autodesk.Civil.DatabaseServices.Surface>(ids.Cast<ObjectId>(), tr, name);
  }

  // ─────────────────────────────────────────────
  // Mes 9 — implementación real (antes eran stubs mudos sin ningún intento).
  // Investigación: Autodesk Civil3D .NET Developer Guide, "Adding A Breakline to
  // a TIN Surface" / "Adding a Boundary" / "Extracting Contours" — cada TinSurface
  // expone BreaklinesDefinition/BoundariesDefinition con overloads que aceptan
  // Point3dCollection directamente (no requieren una polilínea ya dibujada), y
  // ExtractMinorContours/ExtractMajorContours vía la interfaz ITerrainSurface.
  // Build-verify único: lo que el compilador rechace vuelve a stub con la firma
  // exacta intentada, no un stub mudo.
  // ─────────────────────────────────────────────
  public static Task<object?> AddSurfaceBreaklineAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var breaklineType = (PluginRuntime.GetOptionalString(parameters, "breaklineType") ?? "standard").ToLowerInvariant();
    var pointsNode = parameters?["points"] as JsonArray;

    if (pointsNode == null || pointsNode.Count < 2)
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "At least 2 points required for a breakline.");

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

      // Solo "standard" está confirmado: TinSurface.BreaklinesDefinition.AddStandardBreaklines(
      // Point3dCollection, double, double, double, int) compila tal cual. "wall"/"proximity"
      // se intentaron con la misma forma de 4 argumentos (points + 3 doubles) y el compilador
      // rechazó ambas por sobrecarga inexistente (CS1501) — sus firmas reales necesitan
      // confirmarse contra una sesión de Civil3D en vivo antes de reintentar.
      if (breaklineType != "standard")
      {
        return new
        {
          status = "planned",
          note = $"breaklineType '{breaklineType}' not implemented — AddWallBreaklines/AddProximityBreaklines " +
                 "don't accept the (Point3dCollection, double, double, double) overload (CS1501: no overload " +
                 "takes 4 arguments). Only 'standard' (AddStandardBreaklines) is confirmed working. Needs the " +
                 "real signature confirmed against a live Civil 3D session."
        };
      }

      // Pruebas en vivo confirmaron el error "midOrdinateDistance should be greater than zero"
      // con los 3 doubles en 0.0 — no está confirmado cuál de los 3 (weedDistance/weedAngle/
      // supplementDistance según el ejemplo de Autodesk) es el que Civil3D valida como
      // "midOrdinateDistance" internamente, así que se usa 0.1 en los tres (mismo valor del
      // ejemplo real) en vez de adivinar la posición exacta — un weeding mínimo no cambia el
      // comportamiento de forma significativa para los que no sean el parámetro validado.
      surface.BreaklinesDefinition.AddStandardBreaklines(pts, 0.1, 0.1, 0.1, 0);

      return new
      {
        success = true,
        surfaceName = name,
        breaklineType,
        pointCount = pts.Count
      };
    });
  }

  public static Task<object?> AddSurfaceBoundaryAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var boundaryTypeRaw = PluginRuntime.GetRequiredString(parameters, "boundaryType");
    var pointsNode = parameters?["points"] as JsonArray;

    if (pointsNode == null || pointsNode.Count < 3)
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "At least 3 boundary points required.");

    var boundaryType = boundaryTypeRaw switch
    {
      "show" => Autodesk.Civil.SurfaceBoundaryType.Show,
      "hide" => Autodesk.Civil.SurfaceBoundaryType.Hide,
      "outer" => Autodesk.Civil.SurfaceBoundaryType.Outer,
      "data_clip" => Autodesk.Civil.SurfaceBoundaryType.DataClip,
      _ => throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", $"Unknown boundaryType '{boundaryTypeRaw}'."),
    };

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
          0.0
        ));
      }

      // midOrdinateDistance debe ser > 0 (confirmado en pruebas en vivo: "midOrdinateDistance
      // should be greater than zero") — determina la tesela de arcos/curvas al convertir a línea.
      surface.BoundariesDefinition.AddBoundaries(pts, 0.1, boundaryType, false);

      return new
      {
        success = true,
        surfaceName = name,
        boundaryType = boundaryTypeRaw,
        pointCount = pts.Count
      };
    });
  }

  public static Task<object?> ExtractSurfaceContoursAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "Real signatures found by the compiler are TinSurface.ExtractMinorContours(SurfaceExtractionSettingsType, " +
             "ContourSmoothingType, int smoothFactor) and ExtractMajorContours(same shape) — neither takes a raw " +
             "interval/layerId pair as guessed (CS7036: missing required parameter 'smoothFactor'). The real API " +
             "extracts contours using the surface's own extraction settings object, not an ad-hoc interval — needs " +
             "research into SurfaceExtractionSettingsType against a live Civil 3D session before attempting again. " +
             "For contour-derived areas/volumes without extraction, civil3d_surface's compute_contour_volume and " +
             "close_contours_against_boundary already work against manually-drawn contour polylines."
    });
}