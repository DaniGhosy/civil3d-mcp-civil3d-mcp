using System.Text.Json.Nodes;
using System.Linq;
using System.Globalization;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

namespace Civil3DMcpPlugin;

/// <summary>
/// Mes 6: sample lines, section views, mass haul, quantity takeoff.
/// SampleLineGroup.Create / SampleLine.Create (both overloads) / the
/// SectionViewGroups.Add parameter list, are confirmed against real
/// Autodesk DevBlog samples and official reference pages. MassHaulLine
/// creation and material list enumeration have no confirmed API surface —
/// left as documented stubs rather than guessed.
/// </summary>
public static class SampleLineCommands
{
  // ─────────────────────────────────────────────
  // Líneas de muestreo (S1)
  // ─────────────────────────────────────────────
  public static Task<object?> CreateSampleLineGroupAsync(JsonObject? p)
  {
    var alignmentName = PluginRuntime.GetRequiredString(p, "alignmentName");
    var name = PluginRuntime.GetRequiredString(p, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, tr, alignmentName);
      var groupId = SampleLineGroup.Create(name, alignment.ObjectId);
      var group = tr.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;

      return new { success = true, name, alignmentName, handle = group?.Handle.ToString() };
    });
  }

  public static Task<object?> ListSampleLineGroupsAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var groups = new List<object>();
      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var group = tr.GetObject(id, OpenMode.ForRead) as SampleLineGroup;
        if (group == null) continue;

        groups.Add(new { name = group.Name, handle = group.Handle.ToString() });
      }

      return new { groups };
    });
  }

  public static Task<object?> CreateSampleLineAsync(JsonObject? p)
  {
    var groupName = PluginRuntime.GetRequiredString(p, "groupName");
    var name = PluginRuntime.GetRequiredString(p, "name");
    var station = PluginRuntime.GetOptionalDouble(p, "station");
    var pointsNode = p?["points"] as JsonArray;

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var group = CivilObjectLookup.FindEntityByName<SampleLineGroup>(tr, db, groupName);

      ObjectId sampleLineId;
      if (station.HasValue)
      {
        sampleLineId = SampleLine.Create(name, group.ObjectId, station.Value);
      }
      else if (pointsNode != null && pointsNode.Count > 0)
      {
        var pts = new Point2dCollection();
        foreach (var pt in pointsNode)
          pts.Add(new Point2d(pt!["x"]!.GetValue<double>(), pt["y"]!.GetValue<double>()));
        sampleLineId = SampleLine.Create(name, group.ObjectId, pts);
      }
      else
      {
        throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Either 'station' or 'points' is required.");
      }

      var sampleLine = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;

      return new { success = true, groupName, name, handle = sampleLine?.Handle.ToString() };
    });
  }

  public static Task<object?> ListSampleLinesAsync(JsonObject? p)
  {
    var groupName = PluginRuntime.GetRequiredString(p, "groupName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var group = CivilObjectLookup.FindEntityByName<SampleLineGroup>(tr, db, groupName);
      var sampleLines = new List<object>();

      foreach (ObjectId id in group.GetSampleLineIds())
      {
        var sl = tr.GetObject(id, OpenMode.ForRead) as SampleLine;
        if (sl == null) continue;

        sampleLines.Add(GenericObjectCommands.SerializeSimpleProperties(sl));
      }

      return new { groupName, sampleLines };
    });
  }

  public static Task<object?> DeleteSampleLineGroupAsync(JsonObject? p)
  {
    var groupName = PluginRuntime.GetRequiredString(p, "groupName");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var group = CivilObjectLookup.FindEntityByName<SampleLineGroup>(tr, db, groupName);
      group.UpgradeOpen();
      group.Erase();

      return new { success = true, deleted = groupName };
    });
  }

  // ─────────────────────────────────────────────
  // Vistas de sección (S2)
  // ─────────────────────────────────────────────
  public static Task<object?> CreateSectionViewGroupAsync(JsonObject? p)
  {
    var groupName = PluginRuntime.GetRequiredString(p, "groupName");
    var x = PluginRuntime.GetRequiredDouble(p, "x");
    var y = PluginRuntime.GetRequiredDouble(p, "y");
    var z = PluginRuntime.GetOptionalDouble(p, "z") ?? 0.0;

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var group = CivilObjectLookup.FindEntityByName<SampleLineGroup>(tr, db, groupName);
      group.UpgradeOpen();

      var insertPosition = new Point3d(x, y, z);
      var rangeOptions = new SectionViewGroupCreationRangeOptions(group.ObjectId);
      var placementOptions = new SectionViewGroupCreationPlacementOptions();
      var displayOptions = new SectionDisplayOptionCollection(group.ObjectId);

      var sectionViewGroup = group.SectionViewGroups.Add(
        insertPosition, 0.0, 0.0, rangeOptions, placementOptions, displayOptions, ObjectId.Null, ObjectId.Null);

      return new
      {
        success = true,
        groupName,
        sectionViewGroupName = sectionViewGroup.Name,
      };
    });
  }

  public static Task<object?> ListSectionViewsAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var views = new List<object>();
      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var sv = tr.GetObject(id, OpenMode.ForRead) as SectionView;
        if (sv == null) continue;

        views.Add(new { name = sv.Name, handle = sv.Handle.ToString() });
      }

      return new { views };
    });
  }

  public static Task<object?> DeleteSectionViewAsync(JsonObject? p)
  {
    var name = PluginRuntime.GetRequiredString(p, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var sv = CivilObjectLookup.FindEntityByName<SectionView>(tr, db, name);
      sv.UpgradeOpen();
      sv.Erase();

      return new { success = true, deleted = name };
    });
  }

  // ─────────────────────────────────────────────
  // Mass haul (S3) — solo se confirmó que la clase MassHaulLine existe, sin
  // vía de creación encontrada. Listado por escaneo de ModelSpace (mismo
  // patrón que feature lines/profile views); creación queda stub.
  // ─────────────────────────────────────────────
  public static Task<object?> ListMassHaulLinesAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var lines = new List<object>();
      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var mhl = tr.GetObject(id, OpenMode.ForRead) as MassHaulLine;
        if (mhl == null) continue;

        lines.Add(new
        {
          handle = mhl.Handle.ToString(),
          properties = GenericObjectCommands.SerializeSimpleProperties(mhl),
        });
      }

      return new { massHaulLines = lines };
    });
  }

  public static Task<object?> CreateMassHaulLineAsync(JsonObject? p)
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "No confirmed API member found for creating a mass haul line/diagram. Needs confirmation against a live Civil 3D drawing."
    });

  // ─────────────────────────────────────────────
  // Quantity Takeoff (S4)
  // ─────────────────────────────────────────────
  public static Task<object?> ReportQuantitiesAsync(JsonObject? p)
  {
    var groupName = PluginRuntime.GetRequiredString(p, "groupName");
    var materialListName = PluginRuntime.GetRequiredString(p, "materialListName");
    var reportFileName = PluginRuntime.GetRequiredString(p, "reportFileName");
    var styleSheetFileName = PluginRuntime.GetOptionalString(p, "styleSheetFileName") ?? "";

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var group = CivilObjectLookup.FindEntityByName<SampleLineGroup>(tr, db, groupName);

      SampleLineGroup.ReportQuantities(group.ObjectId, materialListName, reportFileName, styleSheetFileName);

      return new { success = true, groupName, materialListName, reportFileName };
    });
  }

  public static Task<object?> ListMaterialListsAsync()
    => Task.FromResult<object?>(new
    {
      status = "planned",
      note = "No confirmed access chain from CivilDocument to available material lists. Needs confirmation against a live Civil 3D drawing."
    });

  // ─────────────────────────────────────────────
  // getSectionData / createSectionViews / updateSectionViewStyles /
  // exportSectionData (portado de Civil3D-mcp-main, adaptado a
  // AlignmentCommands.FindAlignmentByName de este repo).
  // ─────────────────────────────────────────────

  public static Task<object?> GetSectionDataAsync(JsonObject? parameters)
  {
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");
    var sampleLineGroupName = PluginRuntime.GetRequiredString(parameters, "sampleLineGroupName");
    var station = PluginRuntime.GetRequiredDouble(parameters, "station");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, transaction, alignmentName);
      var group = FindSampleLineGroup(alignment, transaction, sampleLineGroupName);

      var sampleLine = group.GetSampleLineIds()
        .Cast<ObjectId>()
        .Select(id => CivilObjectUtils.GetRequiredObject<SampleLine>(transaction, id, OpenMode.ForRead))
        .FirstOrDefault(line => Math.Abs(line.Station - station) < 0.0001)
        ?? throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Sample line at station {station} was not found.");

      return new Dictionary<string, object?>
      {
        ["station"] = sampleLine.Station,
        ["surfaces"] = new List<object>(),
        ["units"] = new Dictionary<string, object?>
        {
          ["horizontal"] = CivilObjectUtils.LinearUnits(database),
          ["vertical"] = CivilObjectUtils.LinearUnits(database),
        },
      };
    });
  }

  public static Task<object?> CreateSectionViewsAsync(JsonObject? parameters)
  {
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");
    var sampleLineGroupName = PluginRuntime.GetRequiredString(parameters, "sampleLineGroupName");
    var insertionX = PluginRuntime.GetRequiredDouble(parameters, "insertionX");
    var insertionY = PluginRuntime.GetRequiredDouble(parameters, "insertionY");
    var style = PluginRuntime.GetOptionalString(parameters, "style");
    var bandSetStyle = PluginRuntime.GetOptionalString(parameters, "bandSetStyle");
    var leftOffset = PluginRuntime.GetOptionalDouble(parameters, "leftOffset");
    var rightOffset = PluginRuntime.GetOptionalDouble(parameters, "rightOffset");
    var stationStart = PluginRuntime.GetOptionalDouble(parameters, "stationStart");
    var stationEnd = PluginRuntime.GetOptionalDouble(parameters, "stationEnd");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, transaction, alignmentName);
      var group = FindSampleLineGroup(alignment, transaction, sampleLineGroupName);
      var styleId = LookupUtils.GetSectionViewStyleId(civilDoc, transaction, style);
      var bandSetId = LookupUtils.GetSectionViewBandSetId(civilDoc, transaction, bandSetStyle);
      var insertionPoint = new Point3d(insertionX, insertionY, 0);

      var createdGroup = CreateSectionViewGroupRange(
        alignment, group, insertionPoint, leftOffset, rightOffset, stationStart, stationEnd);
      var createdViews = OpenSectionViews(createdGroup, transaction, OpenMode.ForWrite).ToList();
      ApplySectionViewStyles(createdViews, styleId, bandSetId, applyToAll: true);

      return new Dictionary<string, object?>
      {
        ["alignmentName"] = alignment.Name,
        ["sampleLineGroupName"] = group.Name,
        ["created"] = createdViews.Count,
        ["insertionPoint"] = new Dictionary<string, object?> { ["x"] = insertionPoint.X, ["y"] = insertionPoint.Y },
      };
    });
  }

  public static Task<object?> UpdateSectionViewStylesAsync(JsonObject? parameters)
  {
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");
    var sampleLineGroupName = PluginRuntime.GetRequiredString(parameters, "sampleLineGroupName");
    var style = PluginRuntime.GetOptionalString(parameters, "style");
    var bandSetStyle = PluginRuntime.GetOptionalString(parameters, "bandSetStyle");
    var applyToAll = PluginRuntime.GetOptionalBool(parameters, "applyToAll") ?? true;

    if (style == null && bandSetStyle == null)
    {
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "updateSectionViewStyles requires 'style' or 'bandSetStyle'.");
    }

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, transaction, alignmentName);
      var group = FindSampleLineGroup(alignment, transaction, sampleLineGroupName);
      var styleId = string.IsNullOrWhiteSpace(style) ? ObjectId.Null : LookupUtils.GetSectionViewStyleId(civilDoc, transaction, style);
      var bandSetId = LookupUtils.GetSectionViewBandSetId(civilDoc, transaction, bandSetStyle);

      var sectionViews = EnumerateSectionViewsForGroup(group, transaction).ToList();
      if (sectionViews.Count == 0)
      {
        throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"No section views exist for sample line group '{sampleLineGroupName}'.");
      }

      var styleUpdated = ApplySectionViewStyles(sectionViews, styleId, bandSetId, applyToAll);

      return new Dictionary<string, object?>
      {
        ["alignmentName"] = alignment.Name,
        ["sampleLineGroupName"] = group.Name,
        ["updated"] = styleUpdated,
      };
    });
  }

  public static Task<object?> ExportSectionDataAsync(JsonObject? parameters)
  {
    var alignmentName = PluginRuntime.GetRequiredString(parameters, "alignmentName");
    var sampleLineGroupName = PluginRuntime.GetRequiredString(parameters, "sampleLineGroupName");
    var outputPath = PluginRuntime.GetRequiredString(parameters, "outputPath");
    var includeElevations = PluginRuntime.GetOptionalBool(parameters, "includeElevations") ?? true;
    var includeMaterials = PluginRuntime.GetOptionalBool(parameters, "includeMaterials") ?? false;
    var stationStart = PluginRuntime.GetOptionalDouble(parameters, "stationStart");
    var stationEnd = PluginRuntime.GetOptionalDouble(parameters, "stationEnd");
    var overwrite = PluginRuntime.GetOptionalBool(parameters, "overwrite") ?? false;

    if (includeMaterials)
    {
      throw new JsonRpcDispatchException(
        "CIVIL3D.INVALID_INPUT",
        "Section material quantities are not exposed by this export path. Set includeMaterials=false; no file was written.");
    }

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var alignment = AlignmentCommands.FindAlignmentByName(civilDoc, transaction, alignmentName);
      var group = FindSampleLineGroup(alignment, transaction, sampleLineGroupName);
      var csv = new StringBuilder("Station,Source,Offset");
      if (includeElevations) csv.Append(",Elevation");
      csv.AppendLine();
      var rowsWritten = 0;

      foreach (ObjectId sampleLineId in group.GetSampleLineIds())
      {
        var sampleLine = CivilObjectUtils.GetRequiredObject<SampleLine>(transaction, sampleLineId, OpenMode.ForRead);
        if (stationStart.HasValue && sampleLine.Station < stationStart.Value) continue;
        if (stationEnd.HasValue && sampleLine.Station > stationEnd.Value) continue;

        foreach (ObjectId sectionId in sampleLine.GetSectionIds())
        {
          var section = CivilObjectUtils.GetRequiredObject<Autodesk.Civil.DatabaseServices.Section>(transaction, sectionId, OpenMode.ForRead);
          foreach (SectionPoint point in section.SectionPoints)
          {
            csv.Append(sampleLine.Station.ToString("G17", CultureInfo.InvariantCulture))
              .Append(',').Append(EscapeCsv(section.SourceName))
              .Append(',').Append(point.Location.X.ToString("G17", CultureInfo.InvariantCulture));
            if (includeElevations)
              csv.Append(',').Append(point.Location.Y.ToString("G17", CultureInfo.InvariantCulture));
            csv.AppendLine();
            rowsWritten++;
          }
        }
      }

      var canonicalPath = FileBoundary.WriteAllTextAtomic(outputPath, csv.ToString(), Encoding.UTF8, overwrite, ".csv");
      return new Dictionary<string, object?>
      {
        ["alignmentName"] = alignment.Name,
        ["sampleLineGroupName"] = group.Name,
        ["outputPath"] = canonicalPath,
        ["rowsWritten"] = rowsWritten,
        ["includeElevations"] = includeElevations,
        ["units"] = CivilObjectUtils.LinearUnits(database),
      };
    });
  }

  private static string EscapeCsv(string value)
  {
    if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
      return $"\"{value.Replace("\"", "\"\"")}\"";
    return value;
  }

  private static SampleLineGroup FindSampleLineGroup(Alignment alignment, Transaction transaction, string sampleLineGroupName)
  {
    var groupIds = alignment.GetSampleLineGroupIds();
    if (groupIds.Count == 0)
    {
      throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"No sample line groups exist for alignment '{alignment.Name}'.");
    }

    foreach (ObjectId groupId in groupIds)
    {
      var group = CivilObjectUtils.GetRequiredObject<SampleLineGroup>(transaction, groupId, OpenMode.ForRead);
      if (string.Equals(group.Name, sampleLineGroupName, StringComparison.OrdinalIgnoreCase))
      {
        return group;
      }
    }

    throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Sample line group '{sampleLineGroupName}' was not found.");
  }

  private static IEnumerable<SectionView> EnumerateSectionViewsForGroup(SampleLineGroup group, Transaction transaction)
  {
    foreach (SectionViewGroup sectionViewGroup in group.SectionViewGroups)
    {
      foreach (var sectionView in OpenSectionViews(sectionViewGroup, transaction, OpenMode.ForRead))
      {
        yield return sectionView;
      }
    }
  }

  private static IEnumerable<SectionView> OpenSectionViews(SectionViewGroup group, Transaction transaction, OpenMode openMode)
  {
    foreach (ObjectId viewId in group.GetSectionViewIds())
    {
      if (viewId != ObjectId.Null)
      {
        yield return CivilObjectUtils.GetRequiredObject<SectionView>(transaction, viewId, openMode);
      }
    }
  }

  private static SectionViewGroup CreateSectionViewGroupRange(
    Alignment alignment,
    SampleLineGroup group,
    Point3d insertionPoint,
    double? leftOffset,
    double? rightOffset,
    double? stationStart,
    double? stationEnd)
  {
    if (leftOffset.HasValue != rightOffset.HasValue)
    {
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "leftOffset and rightOffset must be supplied together.");
    }
    if (leftOffset.HasValue && leftOffset.Value >= rightOffset!.Value)
    {
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "leftOffset must be less than rightOffset for Civil 3D section-view ranges.");
    }

    using var rangeOptions = new SectionViewGroupCreationRangeOptions(group.ObjectId);
    if (leftOffset.HasValue)
    {
      rangeOptions.SetOffsetRange(leftOffset.Value, rightOffset!.Value);
    }
    var placementOptions = new SectionViewGroupCreationPlacementOptions();
    placementOptions.UseDraftPlacement();
    var start = stationStart ?? alignment.StartingStation;
    var end = stationEnd ?? alignment.EndingStation;
    return group.SectionViewGroups.Add(insertionPoint, start, end, rangeOptions, placementOptions);
  }

  private static int ApplySectionViewStyles(
    IReadOnlyList<SectionView> sectionViews,
    ObjectId styleId,
    ObjectId bandSetStyleId,
    bool applyToAll)
  {
    int updated = 0;
    var stylesToProcess = applyToAll ? sectionViews : sectionViews.Take(1);
    foreach (var view in stylesToProcess)
    {
      if (!view.IsWriteEnabled)
      {
        view.UpgradeOpen();
      }

      var changed = false;
      if (styleId != ObjectId.Null)
      {
        view.StyleId = styleId;
        changed = true;
      }

      if (bandSetStyleId != ObjectId.Null)
      {
        view.Bands.ImportBandSetStyle(bandSetStyleId);
        changed = true;
      }

      if (changed)
      {
        updated++;
      }
    }

    return updated;
  }
}
