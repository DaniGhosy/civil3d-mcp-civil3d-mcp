using System.Text.Json.Nodes;

namespace Civil3DMcpPlugin;

/// <summary>
/// Multi-step workflows that compose already-ported QC/grading/surface/project/drawing
/// operations into a single call, backing civil3d_workflow. Each step calls another
/// Commands class's static method in-process (no extra network round trip).
///
/// Two source-repo workflow actions were intentionally NOT ported here:
///   - plan_production_publish: depends on PlanProductionCommands.ExportSheetSetAsync/
///     PublishSheetPdfAsync, both confirmed impossible by source itself (they throw
///     "requires a complete AutoCAD PlotEngine transaction... no publish was started").
///   - pipe_network_design (source's pipeDomain "size_network"): needs a working pipe
///     parts-catalog listing to choose replacement sizes from. Target's own
///     PipeNetworkCommands.ListPartsListsAsync is already a documented "planned" stub
///     (PartsList isn't the right element type name — confirmed by the compiler). Porting
///     this workflow would silently produce zero size recommendations, not a real result.
///
/// grading_surface_volume is NOT here — it is a pure TS-side composition (calls the
/// existing civil3d_surface volume_calculate action directly), same as in source.
///
/// Note on two underlying primitives this file calls: DrawingCommands.SaveDrawingAsync
/// ignores its parameters and always saves the current file in place (no "save as" a new
/// path); DrawingCommands.NewDrawingAsync always returns an "not supported via MCP" object
/// rather than creating a drawing. Both are pre-existing target behavior, not something
/// this file works around — the workflow steps below surface whatever they actually return.
/// </summary>
public static class WorkflowCommands
{
  public static async Task<object?> CorridorQcReportWorkflowAsync(JsonObject? parameters)
  {
    var corridorName = PluginRuntime.GetRequiredString(parameters, "corridorName");
    var outputPath = PluginRuntime.GetOptionalString(parameters, "outputPath");
    var includeAlignments = PluginRuntime.GetOptionalBool(parameters, "includeAlignments") ?? false;
    var includeProfiles = PluginRuntime.GetOptionalBool(parameters, "includeProfiles") ?? false;
    var includePipeNetworks = PluginRuntime.GetOptionalBool(parameters, "includePipeNetworks") ?? false;
    var includeSurfaces = PluginRuntime.GetOptionalBool(parameters, "includeSurfaces") ?? false;
    var includeLabels = PluginRuntime.GetOptionalBool(parameters, "includeLabels") ?? false;
    var overwrite = PluginRuntime.GetOptionalBool(parameters, "overwrite") ?? false;

    var corridorCheck = await RequireResult(
      QcCommands.QcCheckCorridorAsync(new JsonObject { ["name"] = corridorName }),
      "qcCheckCorridor");

    var steps = new List<Dictionary<string, object?>>
    {
      WorkflowStep("Run corridor QC check", "qc.check_corridor", "completed", corridorCheck),
    };
    var warnings = new List<string>();
    object? reportResult = null;

    if (!string.IsNullOrWhiteSpace(outputPath))
    {
      reportResult = await RequireResult(
        QcCommands.QcReportGenerateAsync(new JsonObject
        {
          ["outputPath"] = outputPath,
          ["overwrite"] = overwrite,
          ["includeAlignments"] = includeAlignments,
          ["includeProfiles"] = includeProfiles,
          ["includeCorridors"] = true,
          ["includePipeNetworks"] = includePipeNetworks,
          ["includeSurfaces"] = includeSurfaces,
          ["includeLabels"] = includeLabels,
        }),
        "qcReportGenerate");

      steps.Add(WorkflowStep("Generate consolidated QC report", "qc.generate_report", "completed", reportResult));
    }
    else
    {
      warnings.Add("No outputPath was provided, so consolidated QC report generation was skipped.");
      steps.Add(WorkflowStep("Generate consolidated QC report", "qc.generate_report", "skipped"));
    }

    return WorkflowResult(
      "corridor_qc_report",
      $"Completed corridor QC workflow for '{corridorName}'.",
      steps,
      new Dictionary<string, object?>
      {
        ["corridorCheck"] = corridorCheck,
        ["report"] = reportResult,
      },
      warnings);
  }

  public static async Task<object?> SurfaceComparisonReportWorkflowAsync(JsonObject? parameters)
  {
    var baseSurface = PluginRuntime.GetRequiredString(parameters, "baseSurface");
    var comparisonSurface = PluginRuntime.GetRequiredString(parameters, "comparisonSurface");
    var format = PluginRuntime.GetOptionalString(parameters, "format") ?? "summary";

    var baseSurfaceResult = await RequireResult(
      SurfaceCommands.GetSurfaceAsync(new JsonObject { ["name"] = baseSurface }),
      "getSurface.base");
    var comparisonSurfaceResult = await RequireResult(
      SurfaceCommands.GetSurfaceAsync(new JsonObject { ["name"] = comparisonSurface }),
      "getSurface.comparison");
    var volumeResult = await RequireResult(
      SurfaceCommands.ComputeSurfaceVolumeAsync(new JsonObject
      {
        ["baseSurface"] = baseSurface,
        ["comparisonSurface"] = comparisonSurface,
      }),
      "computeSurfaceVolume");
    var reportResult = await RequireResult(
      SurfaceCommands.GetSurfaceVolumeReportAsync(new JsonObject
      {
        ["baseSurface"] = baseSurface,
        ["comparisonSurface"] = comparisonSurface,
        ["format"] = format,
      }),
      "getSurfaceVolumeReport");

    var comparison = new Dictionary<string, object?>
    {
      ["baseSurface"] = baseSurfaceResult,
      ["comparisonSurface"] = comparisonSurfaceResult,
      ["volume"] = volumeResult,
    };

    return WorkflowResult(
      "surface_comparison_report",
      $"Completed surface comparison workflow for '{baseSurface}' vs '{comparisonSurface}'.",
      new List<Dictionary<string, object?>>
      {
        WorkflowStep("Run structured surface comparison", "surface.comparison_workflow", "completed", comparison),
        WorkflowStep("Generate surface volume report", "surface.volume_report", "completed", reportResult),
      },
      new Dictionary<string, object?>
      {
        ["comparison"] = comparison,
        ["report"] = reportResult,
      });
  }

  public static async Task<object?> DataShortcutPublishSyncWorkflowAsync(JsonObject? parameters)
  {
    var objectType = PluginRuntime.GetRequiredString(parameters, "objectType");
    var objectName = PluginRuntime.GetRequiredString(parameters, "objectName");
    var shortcutName = PluginRuntime.GetOptionalString(parameters, "shortcutName") ?? objectName;
    var description = PluginRuntime.GetOptionalString(parameters, "description");
    var projectFolder = PluginRuntime.GetOptionalString(parameters, "projectFolder");
    var dryRun = PluginRuntime.GetOptionalBool(parameters, "dryRun") ?? false;

    var publishResult = await RequireResult(
      DataShortcutCommands.CreateDataShortcutAsync(new JsonObject
      {
        ["objectType"] = objectType,
        ["objectName"] = objectName,
        ["description"] = description,
        ["projectFolder"] = projectFolder,
      }),
      "createDataShortcut");

    var syncResult = await RequireResult(
      DataShortcutCommands.SyncDataShortcutsAsync(new JsonObject
      {
        ["projectFolder"] = projectFolder,
        ["shortcutNames"] = ToJsonArray(new[] { shortcutName }),
        ["dryRun"] = dryRun,
      }),
      "syncDataShortcuts");

    return WorkflowResult(
      "data_shortcut_publish_sync",
      $"Published and synchronized data shortcut '{shortcutName}'.",
      new List<Dictionary<string, object?>>
      {
        WorkflowStep("Publish data shortcut", "data_shortcut.create", "completed", publishResult),
        WorkflowStep("Synchronize published shortcut", "data_shortcut.sync", "completed", syncResult),
      },
      new Dictionary<string, object?>
      {
        ["publish"] = publishResult,
        ["sync"] = syncResult,
        ["shortcutName"] = shortcutName,
      });
  }

  public static async Task<object?> DataShortcutReferenceSyncWorkflowAsync(JsonObject? parameters)
  {
    var projectFolder = PluginRuntime.GetRequiredString(parameters, "projectFolder");
    var shortcutName = PluginRuntime.GetRequiredString(parameters, "shortcutName");
    var shortcutType = PluginRuntime.GetRequiredString(parameters, "shortcutType");
    var layer = PluginRuntime.GetOptionalString(parameters, "layer");
    var dryRun = PluginRuntime.GetOptionalBool(parameters, "dryRun") ?? false;

    var referenceResult = await RequireResult(
      DataShortcutCommands.ReferenceDataShortcutAsync(new JsonObject
      {
        ["projectFolder"] = projectFolder,
        ["shortcutName"] = shortcutName,
        ["shortcutType"] = shortcutType,
        ["layer"] = layer,
      }),
      "referenceDataShortcut");
    var syncResult = await RequireResult(
      DataShortcutCommands.SyncDataShortcutsAsync(new JsonObject
      {
        ["projectFolder"] = projectFolder,
        ["shortcutNames"] = ToJsonArray(new[] { shortcutName }),
        ["dryRun"] = dryRun,
      }),
      "syncDataShortcuts");

    return WorkflowResult(
      "data_shortcut_reference_sync",
      $"Referenced and synchronized data shortcut '{shortcutName}'.",
      new List<Dictionary<string, object?>>
      {
        WorkflowStep("Reference project data shortcut", "data_shortcut.reference", "completed", referenceResult),
        WorkflowStep("Synchronize referenced shortcut", "data_shortcut.sync", "completed", syncResult),
      },
      new Dictionary<string, object?>
      {
        ["reference"] = referenceResult,
        ["sync"] = syncResult,
      });
  }

  public static async Task<object?> ProjectStartupWorkflowAsync(JsonObject? parameters)
  {
    var templatePath = PluginRuntime.GetOptionalString(parameters, "templatePath");
    var save = PluginRuntime.GetOptionalBool(parameters, "save") ?? false;

    var healthResult = await RequireResult(DrawingCommands.GetCivil3DHealthAsync(), "getCivil3DHealth");
    var steps = new List<Dictionary<string, object?>>
    {
      WorkflowStep("Check Civil 3D health", "plugin.health", "completed", healthResult),
    };
    var warnings = new List<string>();
    object? newDrawingResult = null;

    if (!string.IsNullOrWhiteSpace(templatePath))
    {
      newDrawingResult = await RequireResult(
        DrawingCommands.NewDrawingAsync(new JsonObject { ["templatePath"] = templatePath }),
        "newDrawing");
      steps.Add(WorkflowStep("Create or open startup drawing from template", "drawing.new", "completed", newDrawingResult));
      warnings.Add("newDrawing is not supported via MCP in this plugin build (requires user interaction) — see the step result for the exact limitation reported.");
    }
    else
    {
      steps.Add(WorkflowStep("Create or open startup drawing from template", "drawing.new", "skipped"));
    }

    var drawingInfoResult = await RequireResult(DrawingCommands.GetDrawingInfoAsync(), "getDrawingInfo");
    var drawingSettingsResult = await RequireResult(DrawingCommands.GetDrawingSettingsAsync(), "getDrawingSettings");
    var objectTypesResult = await DrawingCommands.ListCivilObjectTypesAsync();
    var dataShortcutsResult = await RequireResult(DataShortcutCommands.ListDataShortcutsAsync(), "listDataShortcuts");

    steps.Add(WorkflowStep("Inspect drawing info", "drawing.info", "completed", drawingInfoResult));
    steps.Add(WorkflowStep("Inspect drawing settings", "drawing.settings", "completed", drawingSettingsResult));
    steps.Add(WorkflowStep("List Civil 3D object types", "drawing.list_object_types", "completed", objectTypesResult));
    steps.Add(WorkflowStep("List project data shortcuts", "data_shortcut.list", "completed", dataShortcutsResult));

    object? saveResult = null;
    if (save)
    {
      saveResult = await RequireResult(DrawingCommands.SaveDrawingAsync(null), "saveDrawing");
      steps.Add(WorkflowStep("Save startup drawing (in place)", "drawing.save", "completed", saveResult));
    }
    else
    {
      steps.Add(WorkflowStep("Save startup drawing (in place)", "drawing.save", "skipped"));
    }

    return WorkflowResult(
      "project_startup",
      "Completed project startup and drawing readiness workflow.",
      steps,
      new Dictionary<string, object?>
      {
        ["health"] = healthResult,
        ["newDrawing"] = newDrawingResult,
        ["drawingInfo"] = drawingInfoResult,
        ["drawingSettings"] = drawingSettingsResult,
        ["objectTypes"] = objectTypesResult,
        ["dataShortcuts"] = dataShortcutsResult,
        ["save"] = saveResult,
      },
      warnings);
  }

  public static async Task<object?> ProjectReferenceSetupWorkflowAsync(JsonObject? parameters)
  {
    if (parameters?["references"] is not JsonArray referencesArray || referencesArray.Count == 0)
    {
      throw new JsonRpcDispatchException("CIVIL3D.INVALID_INPUT", "projectReferenceSetupWorkflow requires at least one reference.");
    }

    var dryRun = PluginRuntime.GetOptionalBool(parameters, "dryRun") ?? false;
    var save = PluginRuntime.GetOptionalBool(parameters, "save") ?? false;
    var referenceResults = new List<object?>();
    var steps = new List<Dictionary<string, object?>>();
    var shortcutNames = new List<string>();
    string? syncProjectFolder = null;

    foreach (var referenceNode in referencesArray.OfType<JsonObject>())
    {
      var projectFolder = PluginRuntime.GetRequiredString(referenceNode, "projectFolder");
      var shortcutName = PluginRuntime.GetRequiredString(referenceNode, "shortcutName");
      var shortcutType = PluginRuntime.GetRequiredString(referenceNode, "shortcutType");
      var layer = PluginRuntime.GetOptionalString(referenceNode, "layer");

      syncProjectFolder ??= projectFolder;
      shortcutNames.Add(shortcutName);

      var result = await RequireResult(
        DataShortcutCommands.ReferenceDataShortcutAsync(new JsonObject
        {
          ["projectFolder"] = projectFolder,
          ["shortcutName"] = shortcutName,
          ["shortcutType"] = shortcutType,
          ["layer"] = layer,
        }),
        $"referenceDataShortcut.{shortcutName}");
      referenceResults.Add(result);
      steps.Add(WorkflowStep($"Reference data shortcut '{shortcutName}'", "data_shortcut.reference", "completed", result));
    }

    var syncResult = await RequireResult(
      DataShortcutCommands.SyncDataShortcutsAsync(new JsonObject
      {
        ["projectFolder"] = syncProjectFolder,
        ["shortcutNames"] = ToJsonArray(shortcutNames),
        ["dryRun"] = dryRun,
      }),
      "syncDataShortcuts");
    steps.Add(WorkflowStep("Synchronize referenced shortcuts", "data_shortcut.sync", "completed", syncResult));

    var dataShortcutsResult = await RequireResult(DataShortcutCommands.ListDataShortcutsAsync(), "listDataShortcuts");
    steps.Add(WorkflowStep("List data shortcuts after setup", "data_shortcut.list", "completed", dataShortcutsResult));

    object? saveResult = null;
    if (save)
    {
      saveResult = await RequireResult(DrawingCommands.SaveDrawingAsync(null), "saveDrawing");
      steps.Add(WorkflowStep("Save drawing after reference setup (in place)", "drawing.save", "completed", saveResult));
    }
    else
    {
      steps.Add(WorkflowStep("Save drawing after reference setup (in place)", "drawing.save", "skipped"));
    }

    return WorkflowResult(
      "project_reference_setup",
      $"Completed project reference setup for {shortcutNames.Count} data shortcut(s).",
      steps,
      new Dictionary<string, object?>
      {
        ["references"] = referenceResults,
        ["sync"] = syncResult,
        ["dataShortcuts"] = dataShortcutsResult,
        ["save"] = saveResult,
      });
  }

  public static async Task<object?> DrawingReadinessAuditWorkflowAsync(JsonObject? parameters)
  {
    var layerPrefix = PluginRuntime.GetOptionalString(parameters, "layerPrefix");
    var checkLineweights = PluginRuntime.GetOptionalBool(parameters, "checkLineweights");
    var checkColors = PluginRuntime.GetOptionalBool(parameters, "checkColors");
    var limit = PluginRuntime.GetOptionalInt(parameters, "limit");

    var healthResult = await RequireResult(DrawingCommands.GetCivil3DHealthAsync(), "getCivil3DHealth");
    var drawingInfoResult = await RequireResult(DrawingCommands.GetDrawingInfoAsync(), "getDrawingInfo");
    var drawingSettingsResult = await RequireResult(DrawingCommands.GetDrawingSettingsAsync(), "getDrawingSettings");
    var objectTypesResult = await DrawingCommands.ListCivilObjectTypesAsync();
    var selectedObjectsResult = await DrawingCommands.GetSelectedCivilObjectsInfoAsync(new JsonObject { ["limit"] = limit });
    var standardsAuditResult = await RequireResult(
      QcCommands.QcCheckDrawingStandardsAsync(new JsonObject
      {
        ["layerPrefix"] = layerPrefix,
        ["checkLineweights"] = checkLineweights,
        ["checkColors"] = checkColors,
      }),
      "qcCheckDrawingStandards");

    return WorkflowResult(
      "drawing_readiness_audit",
      "Completed drawing readiness audit workflow.",
      new List<Dictionary<string, object?>>
      {
        WorkflowStep("Check Civil 3D health", "plugin.health", "completed", healthResult),
        WorkflowStep("Inspect drawing info", "drawing.info", "completed", drawingInfoResult),
        WorkflowStep("Inspect drawing settings", "drawing.settings", "completed", drawingSettingsResult),
        WorkflowStep("List Civil 3D object types", "drawing.list_object_types", "completed", objectTypesResult),
        WorkflowStep("Inspect selected Civil 3D objects", "drawing.selected_objects_info", "completed", selectedObjectsResult),
        WorkflowStep("Audit drawing standards", "qc.check_drawing_standards", "completed", standardsAuditResult),
      },
      new Dictionary<string, object?>
      {
        ["health"] = healthResult,
        ["drawingInfo"] = drawingInfoResult,
        ["drawingSettings"] = drawingSettingsResult,
        ["objectTypes"] = objectTypesResult,
        ["selectedObjects"] = selectedObjectsResult,
        ["standardsAudit"] = standardsAuditResult,
      });
  }

  public static async Task<object?> FeatureLineToGradingWorkflowAsync(JsonObject? parameters)
  {
    var featureLineName = PluginRuntime.GetRequiredString(parameters, "featureLineName");
    var groupName = PluginRuntime.GetRequiredString(parameters, "groupName");
    var groupDescription = PluginRuntime.GetOptionalString(parameters, "groupDescription");
    var createGroup = PluginRuntime.GetOptionalBool(parameters, "createGroup") ?? false;
    var useProjection = PluginRuntime.GetOptionalBool(parameters, "useProjection");
    var criteriaName = PluginRuntime.GetOptionalString(parameters, "criteriaName");
    var side = PluginRuntime.GetOptionalString(parameters, "side");
    var surfaceName = PluginRuntime.GetOptionalString(parameters, "surfaceName");

    var featureLine = await RequireResult(
      GradingCommands.GetFeatureLineAsync(new JsonObject { ["name"] = featureLineName }),
      "getFeatureLine");

    var steps = new List<Dictionary<string, object?>>
    {
      WorkflowStep("Inspect source feature line", "grading.feature_line_get", "completed", featureLine),
    };
    var warnings = new List<string>();
    object? groupResult = null;

    if (createGroup)
    {
      groupResult = await RequireResult(
        GradingCommands.CreateGradingGroupAsync(new JsonObject
        {
          ["name"] = groupName,
          ["description"] = groupDescription,
          ["useProjection"] = useProjection,
        }),
        "createGradingGroup");
      steps.Add(WorkflowStep("Create grading group", "grading.group_create", "completed", groupResult));
    }
    else
    {
      steps.Add(WorkflowStep("Create grading group", "grading.group_create", "skipped"));
    }

    var gradingResult = await RequireResult(
      GradingCommands.CreateGradingAsync(new JsonObject
      {
        ["groupName"] = groupName,
        ["featureLineName"] = featureLineName,
        ["criteriaName"] = criteriaName,
        ["side"] = side,
      }),
      "createGrading");
    steps.Add(WorkflowStep("Create grading from feature line", "grading.create", "completed", gradingResult));

    object? surfaceResult = null;
    if (!string.IsNullOrWhiteSpace(surfaceName))
    {
      surfaceResult = await RequireResult(
        GradingCommands.CreateSurfaceFromGradingGroupAsync(new JsonObject
        {
          ["name"] = groupName,
          ["surfaceName"] = surfaceName,
        }),
        "createSurfaceFromGradingGroup");
      steps.Add(WorkflowStep("Create grading surface", "grading.group_surface_create", "completed", surfaceResult));
    }
    else
    {
      warnings.Add("No surfaceName was provided, so grading-surface creation was skipped.");
      steps.Add(WorkflowStep("Create grading surface", "grading.group_surface_create", "skipped"));
    }

    return WorkflowResult(
      "feature_line_to_grading",
      $"Converted feature line '{featureLineName}' into grading in group '{groupName}'.",
      steps,
      new Dictionary<string, object?>
      {
        ["featureLine"] = featureLine,
        ["group"] = groupResult,
        ["grading"] = gradingResult,
        ["surface"] = surfaceResult,
      },
      warnings);
  }

  public static async Task<object?> QcFixAndVerifyWorkflowAsync(JsonObject? parameters)
  {
    var layerPrefix = PluginRuntime.GetOptionalString(parameters, "layerPrefix");
    var checkLineweights = PluginRuntime.GetOptionalBool(parameters, "checkLineweights");
    var checkColors = PluginRuntime.GetOptionalBool(parameters, "checkColors");
    var fixSpaces = PluginRuntime.GetOptionalBool(parameters, "fixSpaces");
    var maxNameLength = PluginRuntime.GetOptionalInt(parameters, "maxNameLength");
    var colorIndex = PluginRuntime.GetOptionalInt(parameters, "colorIndex");
    var lineweight = PluginRuntime.GetOptionalInt(parameters, "lineweight");
    var dryRun = PluginRuntime.GetOptionalBool(parameters, "dryRun");

    var initialCheck = await RequireResult(
      QcCommands.QcCheckDrawingStandardsAsync(new JsonObject
      {
        ["layerPrefix"] = layerPrefix,
        ["checkLineweights"] = checkLineweights,
        ["checkColors"] = checkColors,
      }),
      "qcCheckDrawingStandards.initial");
    var fixResult = await RequireResult(
      QcCommands.QcFixDrawingStandardsAsync(new JsonObject
      {
        ["layerPrefix"] = layerPrefix,
        ["fixSpaces"] = fixSpaces,
        ["maxNameLength"] = maxNameLength,
        ["colorIndex"] = colorIndex,
        ["lineweight"] = lineweight,
        ["dryRun"] = dryRun,
      }),
      "qcFixDrawingStandards");
    var verificationCheck = await RequireResult(
      QcCommands.QcCheckDrawingStandardsAsync(new JsonObject
      {
        ["layerPrefix"] = layerPrefix,
        ["checkLineweights"] = checkLineweights,
        ["checkColors"] = checkColors,
      }),
      "qcCheckDrawingStandards.verification");

    return WorkflowResult(
      "qc_fix_and_verify",
      "Completed drawing-standards fix-and-verify workflow.",
      new List<Dictionary<string, object?>>
      {
        WorkflowStep("Run baseline standards audit", "qc.check_drawing_standards", "completed", initialCheck),
        WorkflowStep("Apply drawing standards fixes", "qc.fix_drawing_standards", "completed", fixResult),
        WorkflowStep("Re-run standards audit", "qc.check_drawing_standards", "completed", verificationCheck),
      },
      new Dictionary<string, object?>
      {
        ["initialCheck"] = initialCheck,
        ["fixes"] = fixResult,
        ["verificationCheck"] = verificationCheck,
      });
  }

  // -------------------------------------------------------------------------
  // Private helpers
  // -------------------------------------------------------------------------

  private static async Task<object> RequireResult(Task<object?> task, string context)
  {
    var value = await task;
    if (value == null)
    {
      throw new JsonRpcDispatchException("CIVIL3D.TRANSACTION_FAILED", $"Expected '{context}' to return a result.");
    }
    return value;
  }

  private static Dictionary<string, object?> WorkflowResult(
    string workflow,
    string summary,
    List<Dictionary<string, object?>> steps,
    Dictionary<string, object?> outputs,
    List<string>? warnings = null)
  {
    warnings ??= new List<string>();
    return new Dictionary<string, object?>
    {
      ["workflow"] = workflow,
      ["status"] = warnings.Count > 0 ? "completed_with_warnings" : "completed",
      ["summary"] = summary,
      ["steps"] = steps,
      ["outputs"] = outputs,
      ["warnings"] = warnings,
    };
  }

  private static Dictionary<string, object?> WorkflowStep(string name, string action, string status, object? result = null)
  {
    var step = new Dictionary<string, object?>
    {
      ["name"] = name,
      ["action"] = action,
      ["status"] = status,
    };

    if (result != null)
    {
      step["result"] = result;
    }

    return step;
  }

  private static JsonArray ToJsonArray(IEnumerable<string> values)
  {
    var array = new JsonArray();
    foreach (var value in values)
    {
      array.Add(value);
    }

    return array;
  }
}
