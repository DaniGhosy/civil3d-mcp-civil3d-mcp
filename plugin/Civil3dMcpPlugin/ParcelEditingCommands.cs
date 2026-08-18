using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Parcel editing commands: editParcel, adjustParcelLotLine, reportParcels.
/// Ported from Civil3D-mcp-main. This repo's own ParcelCommands.cs already
/// covers list/get/delete/create; create_parcel stays that file's documented
/// stub (Parcel.CreateByLayout does not exist with the guessed signature) —
/// not revisited here.
/// </summary>
public static class ParcelEditingCommands
{
  // -------------------------------------------------------------------------
  // editParcel
  // -------------------------------------------------------------------------

  public static Task<object?> EditParcelAsync(JsonObject? parameters)
  {
    var siteName = PluginRuntime.GetRequiredString(parameters, "siteName");
    var parcelName = PluginRuntime.GetRequiredString(parameters, "parcelName");
    var newName = PluginRuntime.GetOptionalString(parameters, "newName");
    var style = PluginRuntime.GetOptionalString(parameters, "style");
    var areaLabelStyle = PluginRuntime.GetOptionalString(parameters, "areaLabelStyle");
    var description = PluginRuntime.GetOptionalString(parameters, "description");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var site = FindSiteByName(civilDoc, transaction, siteName);
      var parcel = FindParcelByName(site, transaction, parcelName, OpenMode.ForWrite);

      if (!string.IsNullOrWhiteSpace(newName)) CivilObjectUtils.TrySetName(parcel, newName);
      if (!string.IsNullOrWhiteSpace(style)) ApplyStyleToParcel(parcel, style, civilDoc, transaction);
      if (!string.IsNullOrWhiteSpace(areaLabelStyle)) ApplyLabelStyleToParcel(parcel, areaLabelStyle, civilDoc, transaction);
      if (!string.IsNullOrWhiteSpace(description))
      {
        parcel.Description = description;
      }

      return new Dictionary<string, object?>
      {
        ["siteName"] = siteName,
        ["name"] = parcel.Name,
        ["handle"] = CivilObjectUtils.GetHandle(parcel),
        ["area"] = parcel.Area,
        ["updated"] = true,
      };
    });
  }

  // -------------------------------------------------------------------------
  // adjustParcelLotLine
  // -------------------------------------------------------------------------

  public static Task<object?> AdjustParcelLotLineAsync(JsonObject? parameters)
  {
    var siteName = PluginRuntime.GetRequiredString(parameters, "siteName");
    var parcelName = PluginRuntime.GetRequiredString(parameters, "parcelName");
    var targetAreaSqFt = PluginRuntime.GetRequiredDouble(parameters, "targetAreaSqFt");
    var lotLineHandle = PluginRuntime.GetOptionalString(parameters, "lotLineHandle");
    var tolerance = PluginRuntime.GetOptionalDouble(parameters, "tolerance") ?? 1.0;

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var site = FindSiteByName(civilDoc, transaction, siteName);
      var parcel = FindParcelByName(site, transaction, parcelName, OpenMode.ForRead);

      // Try Civil 3D lot line slide API
      var result = CivilObjectUtils.InvokeMethod(parcel, "AdjustLotLine",
        targetAreaSqFt, tolerance, lotLineHandle);

      if (result == null)
      {
        result = CivilObjectUtils.InvokeMethod(parcel, "SlideAngle",
          targetAreaSqFt, tolerance);
      }

      var actualArea = parcel.Area;
      var converged = Math.Abs(actualArea - targetAreaSqFt) < tolerance;

      return new Dictionary<string, object?>
      {
        ["siteName"] = siteName,
        ["parcelName"] = parcelName,
        ["targetAreaSqFt"] = targetAreaSqFt,
        ["actualArea"] = actualArea,
        ["converged"] = converged,
        ["message"] = converged
          ? $"Lot line adjusted. Parcel area is now {actualArea:F2} drawing units²."
          : "Lot line adjustment attempted. Verify the result in Civil 3D.",
      };
    });
  }

  // -------------------------------------------------------------------------
  // reportParcels
  // -------------------------------------------------------------------------

  public static Task<object?> ReportParcelsAsync(JsonObject? parameters)
  {
    var siteName = PluginRuntime.GetRequiredString(parameters, "siteName");
    var parcelNamesNode = parameters?["parcelNames"] as JsonArray;
    var outputPath = PluginRuntime.GetOptionalString(parameters, "outputPath");
    var includeCoordinates = PluginRuntime.GetOptionalBool(parameters, "includeCoordinates") ?? false;
    var units = PluginRuntime.GetOptionalString(parameters, "units") ?? "sqft";
    var overwrite = PluginRuntime.GetOptionalBool(parameters, "overwrite") ?? false;

    var filterNames = parcelNamesNode?
      .Select(n => n?.GetValue<string>())
      .Where(n => !string.IsNullOrWhiteSpace(n))
      .ToHashSet(StringComparer.OrdinalIgnoreCase);

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var site = FindSiteByName(civilDoc, transaction, siteName);
      var rows = new List<Dictionary<string, object?>>();

      foreach (ObjectId pid in EnumerateParcelIds(site, transaction))
      {
        var parcel = CivilObjectUtils.GetRequiredObject<Parcel>(transaction, pid, OpenMode.ForRead);
        if (filterNames != null && filterNames.Count > 0 && !filterNames.Contains(parcel.Name)) continue;

        var areaInUnits = ConvertArea(parcel.Area, units);

        var row = new Dictionary<string, object?>
        {
          ["name"] = parcel.Name,
          ["handle"] = CivilObjectUtils.GetHandle(parcel),
          ["area"] = areaInUnits,
          ["areaUnits"] = units,
          ["perimeter"] = GetParcelPerimeter(parcel),
          ["style"] = CivilObjectUtils.GetStringProperty(
            parcel.StyleId.IsNull ? null : transaction.GetObject(parcel.StyleId, OpenMode.ForRead), "Name"),
        };

        if (includeCoordinates)
        {
          var vertices = new List<Dictionary<string, object?>>();
          var boundary = CivilObjectUtils.InvokeMethod(parcel, "GetBoundary")
            ?? CivilObjectUtils.InvokeMethod(parcel, "GetVertices");
          if (boundary is System.Collections.IEnumerable pts)
          {
            foreach (var pt in pts)
            {
              double x = CivilObjectUtils.GetDoubleProperty(pt, "X") ?? 0;
              double y = CivilObjectUtils.GetDoubleProperty(pt, "Y") ?? 0;
              vertices.Add(new Dictionary<string, object?> { ["x"] = x, ["y"] = y });
            }
          }
          row["vertices"] = vertices;
        }

        rows.Add(row);
      }

      if (!string.IsNullOrWhiteSpace(outputPath))
      {
        outputPath = WriteCsv(outputPath, rows, includeCoordinates, overwrite);
      }

      return new Dictionary<string, object?>
      {
        ["siteName"] = siteName,
        ["parcelCount"] = rows.Count,
        ["parcels"] = rows,
        ["outputPath"] = outputPath,
      };
    });
  }

  // -------------------------------------------------------------------------
  // Helpers
  // -------------------------------------------------------------------------

  private static Site FindSiteByName(
    Autodesk.Civil.ApplicationServices.CivilDocument civilDoc,
    Transaction transaction, string siteName)
  {
    foreach (ObjectId sid in civilDoc.GetSiteIds())
    {
      if (sid == ObjectId.Null) continue;
      var site = CivilObjectUtils.GetRequiredObject<Site>(transaction, sid, OpenMode.ForRead);
      if (string.Equals(site.Name, siteName, StringComparison.OrdinalIgnoreCase)) return site;
    }
    throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Site '{siteName}' was not found.");
  }

  private static Parcel FindParcelByName(Site site, Transaction transaction, string parcelName, OpenMode mode)
  {
    foreach (ObjectId pid in EnumerateParcelIds(site, transaction))
    {
      var parcel = CivilObjectUtils.GetRequiredObject<Parcel>(transaction, pid, mode);
      if (string.Equals(parcel.Name, parcelName, StringComparison.OrdinalIgnoreCase)) return parcel;
    }
    throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Parcel '{parcelName}' was not found in site '{site.Name}'.");
  }

  private static void ApplyStyleToParcel(Parcel parcel, string styleName,
    Autodesk.Civil.ApplicationServices.CivilDocument civilDoc, Transaction transaction)
  {
    parcel.StyleId = LookupUtils.GetParcelStyleId(civilDoc, transaction, styleName);
  }

  private static void ApplyLabelStyleToParcel(Parcel parcel, string labelStyleName,
    Autodesk.Civil.ApplicationServices.CivilDocument civilDoc, Transaction transaction)
  {
    parcel.AreaSelectionLabelStyleId = LookupUtils.GetParcelAreaLabelStyleId(civilDoc, transaction, labelStyleName);
  }

  private static IEnumerable<ObjectId> EnumerateParcelIds(Site site, Transaction transaction)
  {
    foreach (ObjectId objectId in site.GetParcelIds())
    {
      if (objectId != ObjectId.Null)
        yield return objectId;
    }
  }

  private static double? GetParcelPerimeter(Parcel parcel)
  {
    var perimeter = CivilObjectUtils.GetDoubleProperty(parcel, "Perimeter");
    if (perimeter.HasValue)
      return perimeter.Value;

    var boundary = CivilObjectUtils.InvokeMethod(parcel, "GetBoundary")
      ?? CivilObjectUtils.InvokeMethod(parcel, "GetVertices");
    if (boundary is not System.Collections.IEnumerable vertices)
      return null;

    var points = new List<Point2d>();
    foreach (var vertex in vertices)
    {
      var x = CivilObjectUtils.GetDoubleProperty(vertex, "X");
      var y = CivilObjectUtils.GetDoubleProperty(vertex, "Y");
      if (x.HasValue && y.HasValue)
        points.Add(new Point2d(x.Value, y.Value));
    }

    if (points.Count < 2)
      return null;

    double length = 0;
    for (var index = 1; index < points.Count; index++)
    {
      length += points[index - 1].GetDistanceTo(points[index]);
    }

    if (points[0] != points[^1])
      length += points[^1].GetDistanceTo(points[0]);

    return length;
  }

  private static double ConvertArea(double areaInDrawingUnits, string units)
  {
    return units.ToLowerInvariant() switch
    {
      "acres" => areaInDrawingUnits / 43560.0,
      "sqm" => areaInDrawingUnits * 0.092903,
      "ha" => areaInDrawingUnits * 0.092903 / 10000.0,
      _ => areaInDrawingUnits, // sqft or default
    };
  }

  private static string WriteCsv(
    string outputPath,
    List<Dictionary<string, object?>> rows,
    bool includeCoordinates,
    bool overwrite)
  {
    var csv = new System.Text.StringBuilder();
    csv.AppendLine("Name,Area,AreaUnits,Perimeter,Style");
    foreach (var row in rows)
    {
      csv.AppendLine(string.Join(",",
        row.GetValueOrDefault("name"), row.GetValueOrDefault("area"),
        row.GetValueOrDefault("areaUnits"), row.GetValueOrDefault("perimeter"),
        row.GetValueOrDefault("style")));
    }

    return FileBoundary.WriteAllTextAtomic(
      outputPath, csv.ToString(), System.Text.Encoding.UTF8, overwrite, ".csv");
  }
}
