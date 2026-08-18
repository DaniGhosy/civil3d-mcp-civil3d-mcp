using System.Collections;
using System.Linq;
using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using AcDbObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

namespace Civil3DMcpPlugin;

/// <summary>
/// Grading Groups, Gradings, feature lines. list/get/delete Feature Line stayed
/// on this repo's own typed CivilObjectLookup implementation; Grading
/// Group CRUD, CreateFeatureLine, and everything Grading-object-level
/// (list/get/create/delete Grading, volume, criteria) were ported from
/// Civil3D-mcp-main, which found the real reflection-based accessor
/// (Site.GradingGroups) this repo's own attempt had left as an explicit stub.
/// </summary>
public static class GradingCommands
{
  // ─────────────────────────────────────────────
  // Grading Groups (ported from Civil3D-mcp-main — real Site.GradingGroups
  // reflection accessor, replacing this repo's earlier "planned" stubs)
  // ─────────────────────────────────────────────

  public static Task<object?> ListGradingGroupsAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var groups = EnumerateGradingGroups(civilDoc, transaction)
        .Select(g => ToGradingGroupSummary(g, transaction))
        .ToList();

      return new Dictionary<string, object?> { ["groups"] = groups };
    });
  }

  public static Task<object?> GetGradingGroupAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var group = FindGradingGroupByName(civilDoc, transaction, name, OpenMode.ForRead);
      return ToGradingGroupDetail(group, transaction);
    });
  }

  public static Task<object?> CreateGradingGroupAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var description = PluginRuntime.GetOptionalString(parameters, "description") ?? string.Empty;
    var useProjection = PluginRuntime.GetOptionalBool(parameters, "useProjection") ?? false;

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var siteIds = civilDoc.GetSiteIds().Cast<ObjectId>();
      var firstSiteId = siteIds.FirstOrDefault();

      if (firstSiteId == ObjectId.Null)
      {
        throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", "No site found in the drawing. Create a site first before adding a grading group.");
      }

      var siteObj = transaction.GetObject(firstSiteId, OpenMode.ForRead);
      var gradingGroups = Civil3DCompatibility.GetPropertyValue(siteObj, "GradingGroups");

      // Try Add(name, useProjection) or Add(name)
      object? newGroupId = null;
      if (!Civil3DCompatibility.TryInvokeMethod(gradingGroups, "Add", out newGroupId, name, useProjection))
        Civil3DCompatibility.TryInvokeMethod(gradingGroups, "Add", out newGroupId, name);

      if (newGroupId == null)
      {
        throw new JsonRpcDispatchException("CIVIL3D.API_ERROR", "Failed to create grading group — Add method not found.");
      }

      var newGroupObjectId = (ObjectId)newGroupId;
      var newGroup = transaction.GetObject(newGroupObjectId, OpenMode.ForWrite);

      Civil3DCompatibility.TrySetProperty(newGroup, "Description", description);

      return new Dictionary<string, object?>
      {
        ["name"] = name,
        ["handle"] = CivilObjectUtils.GetHandle(newGroup),
        ["description"] = description,
        ["created"] = true,
      };
    });
  }

  public static Task<object?> DeleteGradingGroupAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var group = FindGradingGroupByName(civilDoc, transaction, name, OpenMode.ForWrite);
      group.Erase();

      return new Dictionary<string, object?>
      {
        ["name"] = name,
        ["deleted"] = true,
      };
    });
  }

  // ─────────────────────────────────────────────
  // Ported from Civil3D-mcp-main — net new
  // ─────────────────────────────────────────────

  public static Task<object?> GetGradingGroupVolumeAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var group = FindGradingGroupByName(civilDoc, transaction, name, OpenMode.ForRead);

      var cutVolume = CivilObjectUtils.GetDoubleProperty(group, "CutVolume");
      var fillVolume = CivilObjectUtils.GetDoubleProperty(group, "FillVolume");
      if (!cutVolume.HasValue || !fillVolume.HasValue)
        throw new JsonRpcDispatchException("CIVIL3D.API_ERROR", $"Grading group '{name}' does not expose readable cut/fill volumes. Zero volumes were not substituted.");
      var netVolume = cutVolume.Value - fillVolume.Value;

      return new Dictionary<string, object?>
      {
        ["groupName"] = name,
        ["cutVolume"] = cutVolume.Value,
        ["fillVolume"] = fillVolume.Value,
        ["netVolume"] = netVolume,
        ["units"] = new Dictionary<string, string> { ["volume"] = CivilObjectUtils.VolumeUnits(database) },
      };
    });
  }

  public static Task<object?> CreateSurfaceFromGradingGroupAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");
    var surfaceName = PluginRuntime.GetOptionalString(parameters, "surfaceName");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var group = FindGradingGroupByName(civilDoc, transaction, name, OpenMode.ForWrite);

      // CreateSurface() or CreateSurface(surfaceName) depending on API version
      object? resultId;
      var invoked = surfaceName != null
        ? Civil3DCompatibility.TryInvokeMethod(group, "CreateSurface", out resultId, surfaceName)
        : Civil3DCompatibility.TryInvokeMethod(group, "CreateSurface", out resultId);
      if (!invoked)
        throw new JsonRpcDispatchException("CIVIL3D.API_ERROR", "CreateSurface method not found on grading group.");

      return new Dictionary<string, object?>
      {
        ["groupName"] = name,
        ["surfaceCreated"] = true,
        ["surfaceObjectId"] = resultId?.ToString(),
      };
    });
  }

  public static Task<object?> ListGradingsAsync(JsonObject? parameters)
  {
    var groupName = PluginRuntime.GetRequiredString(parameters, "groupName");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var group = FindGradingGroupByName(civilDoc, transaction, groupName, OpenMode.ForRead);
      var gradings = GetGradingsFromGroup(group, transaction)
        .Select(g => ToGradingSummary(g))
        .ToList();

      return new Dictionary<string, object?>
      {
        ["groupName"] = groupName,
        ["gradings"] = gradings,
      };
    });
  }

  public static Task<object?> GetGradingAsync(JsonObject? parameters)
  {
    var groupName = PluginRuntime.GetRequiredString(parameters, "groupName");
    var handle = PluginRuntime.GetRequiredString(parameters, "handle");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var group = FindGradingGroupByName(civilDoc, transaction, groupName, OpenMode.ForRead);
      var grading = GetGradingsFromGroup(group, transaction)
        .FirstOrDefault(g => CivilObjectUtils.GetHandle(g) == handle)
        ?? throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Grading with handle '{handle}' not found in group '{groupName}'.");

      return ToGradingDetail(grading);
    });
  }

  public static Task<object?> CreateGradingAsync(JsonObject? parameters)
  {
    var groupName = PluginRuntime.GetRequiredString(parameters, "groupName");
    var featureLineName = PluginRuntime.GetRequiredString(parameters, "featureLineName");
    var criteriaName = PluginRuntime.GetOptionalString(parameters, "criteriaName");
    var side = PluginRuntime.GetOptionalString(parameters, "side") ?? "right"; // left | right | both

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var group = FindGradingGroupByName(civilDoc, transaction, groupName, OpenMode.ForWrite);
      var featureLine = CivilObjectLookup.FindEntityByName<FeatureLine>(transaction, database, featureLineName);

      var featureLineId = featureLine.ObjectId;
      object? gradingId;
      var invoked = criteriaName != null
        ? Civil3DCompatibility.TryInvokeMethod(group, "AddGrading", out gradingId, featureLineId, FindGradingCriteriaId(civilDoc, transaction, criteriaName), side)
          || Civil3DCompatibility.TryInvokeMethod(group, "CreateGrading", out gradingId, featureLineId, FindGradingCriteriaId(civilDoc, transaction, criteriaName), side)
        : Civil3DCompatibility.TryInvokeMethod(group, "AddGrading", out gradingId, featureLineId, side)
          || Civil3DCompatibility.TryInvokeMethod(group, "CreateGrading", out gradingId, featureLineId, side);
      if (!invoked)
        throw new JsonRpcDispatchException("CIVIL3D.API_ERROR", "AddGrading/CreateGrading method not found on grading group.");

      return new Dictionary<string, object?>
      {
        ["groupName"] = groupName,
        ["featureLineName"] = featureLineName,
        ["gradingHandle"] = gradingId?.ToString(),
        ["created"] = true,
      };
    });
  }

  public static Task<object?> DeleteGradingAsync(JsonObject? parameters)
  {
    var groupName = PluginRuntime.GetRequiredString(parameters, "groupName");
    var handle = PluginRuntime.GetRequiredString(parameters, "handle");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var group = FindGradingGroupByName(civilDoc, transaction, groupName, OpenMode.ForRead);
      var grading = GetGradingsFromGroup(group, transaction)
        .FirstOrDefault(g => CivilObjectUtils.GetHandle(g) == handle)
        ?? throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Grading with handle '{handle}' not found in group '{groupName}'.");

      var writableGrading = transaction.GetObject(grading.ObjectId, OpenMode.ForWrite);
      writableGrading.Erase();

      return new Dictionary<string, object?>
      {
        ["handle"] = handle,
        ["deleted"] = true,
      };
    });
  }

  public static Task<object?> ListGradingCriteriaAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var criteriaList = new List<Dictionary<string, object?>>();

      var criteriaSets = Civil3DCompatibility.GetPropertyValue(civilDoc, "GradingCriteriaSets");

      foreach (var setId in CivilObjectUtils.ToObjectIds(criteriaSets))
      {
        var setObj = transaction.GetObject(setId, OpenMode.ForRead);
        var setName = CivilObjectUtils.GetName(setObj) ?? string.Empty;

        var criteriaIds = Civil3DCompatibility.GetPropertyValue(setObj, "CriteriaIds")
          ?? Civil3DCompatibility.GetPropertyValue(setObj, "Criteria");

        foreach (var criteriaId in CivilObjectUtils.ToObjectIds(criteriaIds))
        {
          var criteriaObj = transaction.GetObject(criteriaId, OpenMode.ForRead);
          criteriaList.Add(new Dictionary<string, object?>
          {
            ["setName"] = setName,
            ["name"] = CivilObjectUtils.GetName(criteriaObj),
            ["handle"] = CivilObjectUtils.GetHandle(criteriaObj),
            ["description"] = CivilObjectUtils.GetStringProperty(criteriaObj, "Description"),
          });
        }
      }

      return new Dictionary<string, object?> { ["criteriaList"] = criteriaList };
    });
  }

  // ─────────────────────────────────────────────
  // Feature lines: list/get/delete already existed here and are unchanged.
  // Create is upgraded from a "planned" stub to a real FeatureLine.Create call
  // (ported from Civil3D-mcp-main).
  // ─────────────────────────────────────────────

  public static Task<object?> ListFeatureLinesAsync()
  {
    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var featureLines = new List<object>();

      var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
      var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

      foreach (ObjectId id in ms)
      {
        var fl = tr.GetObject(id, OpenMode.ForRead) as FeatureLine;
        if (fl == null) continue;

        featureLines.Add(new
        {
          name = fl.Name,
          handle = fl.Handle.ToString(),
          layer = fl.Layer,
        });
      }

      return new { featureLines };
    });
  }

  public static Task<object?> GetFeatureLineAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.ReadAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var fl = CivilObjectLookup.FindEntityByName<FeatureLine>(tr, db, name);

      return new
      {
        name = fl.Name,
        handle = fl.Handle.ToString(),
        layer = fl.Layer,
      };
    });
  }

  public static Task<object?> DeleteFeatureLineAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetRequiredString(parameters, "name");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, db, tr) =>
    {
      var fl = CivilObjectLookup.FindEntityByName<FeatureLine>(tr, db, name);
      fl.UpgradeOpen();
      fl.Erase();

      return new { success = true, deleted = name };
    });
  }

  public static Task<object?> CreateFeatureLineAsync(JsonObject? parameters)
  {
    var name = PluginRuntime.GetOptionalString(parameters, "name");
    var layer = PluginRuntime.GetOptionalString(parameters, "layer") ?? "0";
    var pointsNode = parameters?["points"] as JsonArray;

    if (pointsNode == null || pointsNode.Count < 2)
    {
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "createFeatureLine requires at least 2 points.");
    }

    var points = pointsNode.Select(node =>
    {
      if (node is not JsonObject pt)
      {
        throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Each point must be a JSON object with x, y, z.");
      }
      var x = pt["x"]?.GetValue<double>() ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Point missing x.");
      var y = pt["y"]?.GetValue<double>() ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Point missing y.");
      var z = pt["z"]?.GetValue<double>() ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "Feature-line point missing z; elevation 0 will not be assumed.");
      return new Point3d(x, y, z);
    }).ToList();

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var siteIds = civilDoc.GetSiteIds().Cast<ObjectId>();
      var firstSiteId = siteIds.FirstOrDefault();

      var pointCollection = new Point3dCollection();
      foreach (var pt in points)
      {
        pointCollection.Add(pt);
      }

      var modelSpace = CivilObjectUtils.GetRequiredObject<BlockTableRecord>(transaction, database.CurrentSpaceId, OpenMode.ForWrite);
      using var sourcePolyline = new Polyline3d(Poly3dType.SimplePoly, pointCollection, false);
      var sourceId = modelSpace.AppendEntity(sourcePolyline);
      transaction.AddNewlyCreatedDBObject(sourcePolyline, true);
      var newObjectId = firstSiteId.IsNull
        ? FeatureLine.Create(name, sourceId)
        : FeatureLine.Create(name, sourceId, firstSiteId);
      sourcePolyline.Erase();
      var fl = transaction.GetObject(newObjectId, OpenMode.ForWrite);

      if (!string.IsNullOrWhiteSpace(name))
      {
        CivilObjectUtils.TrySetName(fl, name);
      }
      CivilObjectUtils.TrySetLayer(fl, layer, database, transaction);

      return new Dictionary<string, object?>
      {
        ["handle"] = CivilObjectUtils.GetHandle(fl),
        ["name"] = CivilObjectUtils.GetName(fl) ?? name,
        ["layer"] = layer,
        ["pointCount"] = points.Count,
        ["created"] = true,
      };
    });
  }

  // -------------------------------------------------------------------------
  // Private helpers
  // -------------------------------------------------------------------------

  private static IEnumerable<AcDbObject> EnumerateGradingGroups(
    Autodesk.Civil.ApplicationServices.CivilDocument civilDoc,
    Transaction transaction)
  {
    foreach (ObjectId siteId in civilDoc.GetSiteIds())
    {
      var siteObj = transaction.GetObject(siteId, OpenMode.ForRead);
      var gradingGroups = Civil3DCompatibility.GetPropertyValue(siteObj, "GradingGroups");

      foreach (var groupId in CivilObjectUtils.ToObjectIds(gradingGroups))
      {
        yield return transaction.GetObject(groupId, OpenMode.ForRead);
      }
    }
  }

  private static AcDbObject FindGradingGroupByName(
    Autodesk.Civil.ApplicationServices.CivilDocument civilDoc,
    Transaction transaction,
    string name,
    OpenMode mode)
  {
    foreach (ObjectId siteId in civilDoc.GetSiteIds())
    {
      var siteObj = transaction.GetObject(siteId, OpenMode.ForRead);
      var gradingGroups = Civil3DCompatibility.GetPropertyValue(siteObj, "GradingGroups");

      foreach (var groupId in CivilObjectUtils.ToObjectIds(gradingGroups))
      {
        var groupObj = transaction.GetObject(groupId, OpenMode.ForRead);
        if (string.Equals(CivilObjectUtils.GetName(groupObj), name, StringComparison.OrdinalIgnoreCase))
        {
          return mode == OpenMode.ForWrite
            ? transaction.GetObject(groupId, OpenMode.ForWrite)
            : groupObj;
        }
      }
    }

    throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Grading group '{name}' was not found.");
  }

  private static IEnumerable<AcDbObject> GetGradingsFromGroup(AcDbObject group, Transaction transaction)
  {
    var gradingIds = Civil3DCompatibility.GetPropertyValue(group, "GradingIds")
      ?? Civil3DCompatibility.GetPropertyValue(group, "Gradings");

    foreach (var id in CivilObjectUtils.ToObjectIds(gradingIds))
    {
      yield return transaction.GetObject(id, OpenMode.ForRead);
    }
  }

  private static ObjectId FindGradingCriteriaId(
    Autodesk.Civil.ApplicationServices.CivilDocument civilDoc,
    Transaction transaction,
    string criteriaName)
  {
    var criteriaSets = Civil3DCompatibility.GetPropertyValue(civilDoc, "GradingCriteriaSets");

    foreach (var setId in CivilObjectUtils.ToObjectIds(criteriaSets))
    {
      var setObj = transaction.GetObject(setId, OpenMode.ForRead);
      var criteriaIds = Civil3DCompatibility.GetPropertyValue(setObj, "CriteriaIds")
        ?? Civil3DCompatibility.GetPropertyValue(setObj, "Criteria");

      foreach (var criteriaId in CivilObjectUtils.ToObjectIds(criteriaIds))
      {
        var criteriaObj = transaction.GetObject(criteriaId, OpenMode.ForRead);
        if (string.Equals(CivilObjectUtils.GetName(criteriaObj), criteriaName, StringComparison.OrdinalIgnoreCase))
        {
          return criteriaId;
        }
      }
    }

    throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND", $"Grading criteria '{criteriaName}' not found.");
  }

  private static Dictionary<string, object?> ToGradingGroupSummary(AcDbObject group, Transaction transaction)
  {
    var ids = Civil3DCompatibility.GetPropertyValue(group, "GradingIds")
      ?? Civil3DCompatibility.GetPropertyValue(group, "Gradings");
    var count = CivilObjectUtils.ToObjectIds(ids).Count();

    return new Dictionary<string, object?>
    {
      ["name"] = CivilObjectUtils.GetName(group),
      ["handle"] = CivilObjectUtils.GetHandle(group),
      ["description"] = CivilObjectUtils.GetStringProperty(group, "Description"),
      ["gradingCount"] = count,
      ["isValid"] = CivilObjectUtils.GetBoolProperty(group, "IsValid"),
    };
  }

  private static Dictionary<string, object?> ToGradingGroupDetail(AcDbObject group, Transaction transaction)
  {
    var gradings = GetGradingsFromGroup(group, transaction)
      .Select(g => ToGradingSummary(g))
      .ToList();

    var cutVolume = CivilObjectUtils.GetDoubleProperty(group, "CutVolume");
    var fillVolume = CivilObjectUtils.GetDoubleProperty(group, "FillVolume");

    return new Dictionary<string, object?>
    {
      ["name"] = CivilObjectUtils.GetName(group),
      ["handle"] = CivilObjectUtils.GetHandle(group),
      ["description"] = CivilObjectUtils.GetStringProperty(group, "Description"),
      ["gradingCount"] = gradings.Count,
      ["cutVolume"] = cutVolume,
      ["fillVolume"] = fillVolume,
      ["netVolume"] = cutVolume.HasValue && fillVolume.HasValue ? cutVolume.Value - fillVolume.Value : null,
      ["isValid"] = CivilObjectUtils.GetBoolProperty(group, "IsValid"),
      ["gradings"] = gradings,
    };
  }

  private static Dictionary<string, object?> ToGradingSummary(AcDbObject grading)
  {
    return new Dictionary<string, object?>
    {
      ["handle"] = CivilObjectUtils.GetHandle(grading),
      ["name"] = CivilObjectUtils.GetName(grading),
      ["criteriaName"] = CivilObjectUtils.GetStringProperty(grading, "CriteriaName"),
      ["isValid"] = CivilObjectUtils.GetBoolProperty(grading, "IsValid"),
    };
  }

  private static Dictionary<string, object?> ToGradingDetail(AcDbObject grading)
  {
    return new Dictionary<string, object?>
    {
      ["handle"] = CivilObjectUtils.GetHandle(grading),
      ["name"] = CivilObjectUtils.GetName(grading),
      ["criteriaName"] = CivilObjectUtils.GetStringProperty(grading, "CriteriaName"),
      ["side"] = CivilObjectUtils.GetStringProperty(grading, "Side"),
      ["isValid"] = CivilObjectUtils.GetBoolProperty(grading, "IsValid"),
      ["cutVolume"] = CivilObjectUtils.GetDoubleProperty(grading, "CutVolume"),
      ["fillVolume"] = CivilObjectUtils.GetDoubleProperty(grading, "FillVolume"),
    };
  }
}
