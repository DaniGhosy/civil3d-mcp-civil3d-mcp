using System.Text.Json.Nodes;
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
}
