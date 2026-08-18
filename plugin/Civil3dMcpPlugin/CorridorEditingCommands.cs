using System.Text.Json.Nodes;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using AcDbObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using CivilSurface = Autodesk.Civil.DatabaseServices.Surface;

namespace Civil3DMcpPlugin;

/// <summary>
/// Corridor editing commands: setCorridorTargetMappings, deleteCorridorRegion.
/// Ported from Civil3D-mcp-main. This repo's own CorridorCommands.cs already
/// covers a read-only per-region target listing (getCorridorTargets) and
/// region creation (addBaselineRegion) under a name/name convention; these two
/// commands work by baseline/region index instead, matching how the source
/// project modeled write access to target mappings.
/// </summary>
public static class CorridorEditingCommands
{
  // -------------------------------------------------------------------------
  // setCorridorTargetMappings
  // -------------------------------------------------------------------------

  public static Task<object?> SetCorridorTargetMappingsAsync(JsonObject? parameters)
  {
    var corridorName = PluginRuntime.GetRequiredString(parameters, "corridorName");
    var regionIndex = PluginRuntime.GetOptionalInt(parameters, "regionIndex") ?? 0;
    var baselineIndex = PluginRuntime.GetOptionalInt(parameters, "baselineIndex") ?? 0;
    var targetsNode = parameters?["targets"] as JsonArray
      ?? throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "targets array is required.");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var corridor = CivilObjectLookup.FindEntityByName<Corridor>(transaction, database, corridorName);
      var baseline = GetBaseline(corridor, baselineIndex);

      if (regionIndex < 0 || regionIndex >= baseline.BaselineRegions.Count)
        throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT",
          $"Region index {regionIndex} is out of range. Corridor '{corridorName}' baseline {baselineIndex} has {baseline.BaselineRegions.Count} region(s).");

      var region = baseline.BaselineRegions[regionIndex];
      var appliedCount = 0;

      foreach (var targetNode in targetsNode)
      {
        if (targetNode is not JsonObject t) continue;
        var paramName = t["parameterName"]?.GetValue<string>();
        var targetType = t["targetType"]?.GetValue<string>();
        var targetName = t["targetName"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(paramName) || string.IsNullOrWhiteSpace(targetType) || string.IsNullOrWhiteSpace(targetName)) continue;

        var targetId = ResolveTargetObjectId(civilDoc, transaction, targetType!, targetName!);
        if (targetId == null)
          throw new JsonRpcDispatchException("CIVIL3D.OBJECT_NOT_FOUND",
            $"Target object '{targetName}' of type '{targetType}' was not found.");

        var applied = ApplyTargetMapping(region, paramName!, targetType!, targetId.Value);
        if (applied) appliedCount++;
      }

      corridor.Rebuild();

      return new Dictionary<string, object?>
      {
        ["corridorName"] = corridorName,
        ["baselineIndex"] = baselineIndex,
        ["regionIndex"] = regionIndex,
        ["targetsApplied"] = appliedCount,
        ["message"] = $"Applied {appliedCount} target mapping(s) and rebuilt corridor '{corridorName}'.",
      };
    });
  }

  // -------------------------------------------------------------------------
  // deleteCorridorRegion
  // -------------------------------------------------------------------------

  public static Task<object?> DeleteCorridorRegionAsync(JsonObject? parameters)
  {
    var corridorName = PluginRuntime.GetRequiredString(parameters, "corridorName");
    var baselineIndex = PluginRuntime.GetOptionalInt(parameters, "baselineIndex") ?? 0;
    var regionIndex = PluginRuntime.GetRequiredInt(parameters, "regionIndex");

    return CivilExecution.WriteAsync<object?>((doc, civilDoc, database, transaction) =>
    {
      var corridor = CivilObjectLookup.FindEntityByName<Corridor>(transaction, database, corridorName);
      var baseline = GetBaseline(corridor, baselineIndex);

      if (regionIndex < 0 || regionIndex >= baseline.BaselineRegions.Count)
        throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT",
          $"Region index {regionIndex} is out of range.");

      var region = baseline.BaselineRegions[regionIndex];
      var regionName = region.Name;
      var startStation = region.StartStation;
      var endStation = region.EndStation;

      var removed = CivilObjectUtils.InvokeMethod(baseline, "RemoveRegion", region);
      if (removed == null)
        removed = CivilObjectUtils.InvokeMethod(baseline, "DeleteRegion", regionIndex);
      if (removed == null)
      {
        var regionId = CivilObjectUtils.GetPropertyValue<ObjectId>(region, "ObjectId");
        if (regionId != ObjectId.Null)
        {
          var regionWrite = CivilObjectUtils.GetRequiredObject<AcDbObject>(transaction, regionId, OpenMode.ForWrite);
          regionWrite.Erase(true);
        }
      }

      corridor.Rebuild();

      return new Dictionary<string, object?>
      {
        ["corridorName"] = corridorName,
        ["baselineIndex"] = baselineIndex,
        ["deletedRegionIndex"] = regionIndex,
        ["deletedRegionName"] = regionName,
        ["deletedStartStation"] = startStation,
        ["deletedEndStation"] = endStation,
        ["message"] = $"Region '{regionName}' deleted from corridor '{corridorName}'.",
      };
    });
  }

  // -------------------------------------------------------------------------
  // Helpers
  // -------------------------------------------------------------------------

  private static Baseline GetBaseline(Corridor corridor, int index)
  {
    if (index < 0 || index >= corridor.Baselines.Count)
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT",
        $"Baseline index {index} is out of range. Corridor '{corridor.Name}' has {corridor.Baselines.Count} baseline(s).");
    return corridor.Baselines[index];
  }

  private static ObjectId? ResolveTargetObjectId(
    Autodesk.Civil.ApplicationServices.CivilDocument civilDoc,
    Transaction transaction, string targetType, string targetName)
  {
    switch (targetType.ToLowerInvariant())
    {
      case "surface":
        foreach (ObjectId id in civilDoc.GetSurfaceIds())
        {
          var s = CivilObjectUtils.GetRequiredObject<CivilSurface>(transaction, id, OpenMode.ForRead);
          if (string.Equals(s.Name, targetName, StringComparison.OrdinalIgnoreCase)) return id;
        }
        break;
      case "alignment":
        foreach (ObjectId id in civilDoc.GetAlignmentIds())
        {
          var a = CivilObjectUtils.GetRequiredObject<Alignment>(transaction, id, OpenMode.ForRead);
          if (string.Equals(a.Name, targetName, StringComparison.OrdinalIgnoreCase)) return id;
        }
        break;
      case "profile":
        foreach (ObjectId aid in civilDoc.GetAlignmentIds())
        {
          var alignment = CivilObjectUtils.GetRequiredObject<Alignment>(transaction, aid, OpenMode.ForRead);
          foreach (ObjectId pid in alignment.GetProfileIds())
          {
            var p = CivilObjectUtils.GetRequiredObject<Profile>(transaction, pid, OpenMode.ForRead);
            if (string.Equals(p.Name, targetName, StringComparison.OrdinalIgnoreCase)) return pid;
          }
        }
        break;
    }
    return null;
  }

  private static bool ApplyTargetMapping(BaselineRegion region, string paramName, string targetType, ObjectId targetId)
  {
    if (Civil3DCompatibility.TryInvokeMethod(region, "SetTarget", out _, paramName, targetId)) return true;
    if (Civil3DCompatibility.TryInvokeMethod(region, "AssignTarget", out _, paramName, targetId)) return true;

    var targets = CivilObjectUtils.GetPropertyValue<object>(region, "Targets");
    if (targets != null)
    {
      foreach (var methodName in new[] { "SetTarget", "Add", "AssignTarget" })
      {
        if (Civil3DCompatibility.TryInvokeMethod(targets, methodName, out _, paramName, targetId))
        {
          return true;
        }
      }
    }

    return false;
  }
}
