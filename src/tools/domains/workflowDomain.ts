import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";
import { SURFACE_DOMAIN_DEFINITION } from "./surfaceDomain.js";

const DataShortcutObjectTypeSchema = z.enum([
  "surface",
  "alignment",
  "profile",
  "pipe_network",
  "pressure_network",
  "corridor",
]);

const GradingSideSchema = z.enum(["left", "right", "both"]);

const ShortcutReferenceSchema = z.object({
  projectFolder: z.string(),
  shortcutName: z.string(),
  shortcutType: DataShortcutObjectTypeSchema,
  layer: z.string().optional(),
});

const WorkflowActionSchema = z.enum([
  "corridor_qc_report",
  "grading_surface_volume",
  "surface_comparison_report",
  "data_shortcut_publish_sync",
  "data_shortcut_reference_sync",
  "project_startup",
  "project_reference_setup",
  "drawing_readiness_audit",
  "feature_line_to_grading",
  "qc_fix_and_verify",
]);

const WorkflowStepSchema = z.object({
  name: z.string(),
  action: z.string(),
  status: z.enum(["completed", "skipped"]),
  result: z.unknown().optional(),
});

function buildWorkflowResult(
  workflow: string,
  summary: string,
  steps: Array<z.infer<typeof WorkflowStepSchema>>,
  outputs: Record<string, unknown>,
  warnings: string[] = []
) {
  return {
    workflow,
    status: warnings.length > 0 ? ("completed_with_warnings" as const) : ("completed" as const),
    summary,
    steps,
    outputs,
    warnings,
  };
}

const canonicalInputShape = {
  action: WorkflowActionSchema.describe("The multi-step workflow to run."),
  corridorName: z.string().optional().describe("Corridor name (corridor_qc_report)."),
  outputPath: z.string().optional().describe("Consolidated QC report output path (corridor_qc_report)."),
  overwrite: z.boolean().optional().describe("Overwrite an existing report file (corridor_qc_report)."),
  includeAlignments: z.boolean().optional().describe("Include alignments in the QC report (corridor_qc_report)."),
  includeProfiles: z.boolean().optional().describe("Include profiles in the QC report (corridor_qc_report)."),
  includePipeNetworks: z.boolean().optional().describe("Include pipe networks in the QC report (corridor_qc_report)."),
  includeSurfaces: z.boolean().optional().describe("Include surfaces in the QC report (corridor_qc_report)."),
  includeLabels: z.boolean().optional().describe("Include labels in the QC report (corridor_qc_report)."),
  baseSurface: z.string().optional().describe("Existing/base surface name (grading_surface_volume, surface_comparison_report)."),
  comparisonSurface: z.string().optional().describe("Comparison/proposed surface name (grading_surface_volume, surface_comparison_report)."),
  method: z.string().optional().describe("Volume method label, informational only (grading_surface_volume)."),
  format: z.enum(["summary", "detailed"]).optional().describe("Report detail level (surface_comparison_report)."),
  objectType: DataShortcutObjectTypeSchema.optional().describe("Object type to publish (data_shortcut_publish_sync)."),
  objectName: z.string().optional().describe("Object name to publish (data_shortcut_publish_sync)."),
  shortcutName: z.string().optional().describe("Shortcut name; defaults to objectName (data_shortcut_publish_sync/data_shortcut_reference_sync)."),
  shortcutType: DataShortcutObjectTypeSchema.optional().describe("Shortcut type (data_shortcut_reference_sync)."),
  references: z.array(ShortcutReferenceSchema).optional().describe("Data shortcuts to reference and sync in one pass (project_reference_setup)."),
  projectFolder: z.string().optional().describe("Data Shortcuts project folder (data_shortcut_publish_sync/data_shortcut_reference_sync)."),
  description: z.string().optional().describe("Shortcut description (data_shortcut_publish_sync)."),
  layer: z.string().optional().describe("Target layer for the new reference (data_shortcut_reference_sync)."),
  templatePath: z.string().optional().describe("Requests a new startup drawing from this template — target's NewDrawingAsync always reports this as unsupported via MCP, the step surfaces that limitation rather than failing silently (project_startup)."),
  save: z.boolean().optional().describe("Save the drawing in place after the workflow — the underlying primitive always saves the current file, there is no 'save as' a new path (project_startup/project_reference_setup)."),
  limit: z.number().optional().describe("Max selected-object entries to inspect (drawing_readiness_audit)."),
  featureLineName: z.string().optional().describe("Source feature line name (feature_line_to_grading)."),
  groupName: z.string().optional().describe("Grading group name (feature_line_to_grading)."),
  groupDescription: z.string().optional().describe("New grading group description (feature_line_to_grading)."),
  createGroup: z.boolean().optional().describe("Create the grading group before adding grading (feature_line_to_grading)."),
  useProjection: z.boolean().optional().describe("Use projection grading group mode (feature_line_to_grading)."),
  criteriaName: z.string().optional().describe("Grading criteria set name (feature_line_to_grading)."),
  side: GradingSideSchema.optional().describe("Grading side relative to the feature line (feature_line_to_grading)."),
  surfaceName: z.string().optional().describe("Surface to create from the grading group, skipped if omitted (feature_line_to_grading)."),
  layerPrefix: z.string().optional().describe("Layer name prefix filter (drawing_readiness_audit/qc_fix_and_verify)."),
  checkLineweights: z.boolean().optional().describe("Check layer lineweights (drawing_readiness_audit/qc_fix_and_verify)."),
  checkColors: z.boolean().optional().describe("Check layer colors (drawing_readiness_audit/qc_fix_and_verify)."),
  fixSpaces: z.boolean().optional().describe("Strip spaces from non-conforming layer names (qc_fix_and_verify)."),
  maxNameLength: z.number().int().positive().optional().describe("Max layer name length before truncation (qc_fix_and_verify)."),
  colorIndex: z.number().int().min(1).max(255).optional().describe("Color index to apply to non-conforming layers (qc_fix_and_verify)."),
  lineweight: z.number().int().optional().describe("Lineweight to apply to non-conforming layers (qc_fix_and_verify)."),
  dryRun: z.boolean().optional().describe("Report intended changes without applying them (data_shortcut_publish_sync/data_shortcut_reference_sync/project_reference_setup/qc_fix_and_verify)."),
};

export const WORKFLOW_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "workflow",
  actions: {
    corridor_qc_report: {
      action: "corridor_qc_report",
      inputSchema: z.object({
        action: z.literal("corridor_qc_report"),
        corridorName: z.string(),
        outputPath: z.string().optional(),
        overwrite: z.boolean().optional(),
        includeAlignments: z.boolean().optional(),
        includeProfiles: z.boolean().optional(),
        includePipeNetworks: z.boolean().optional(),
        includeSurfaces: z.boolean().optional(),
        includeLabels: z.boolean().optional(),
      }),
      capabilities: ["query", "analyze", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["corridorQcReportWorkflow"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("corridorQcReportWorkflow", {
            corridorName: args.corridorName,
            outputPath: args.outputPath,
            overwrite: args.overwrite ?? false,
            includeAlignments: args.includeAlignments,
            includeProfiles: args.includeProfiles,
            includePipeNetworks: args.includePipeNetworks,
            includeSurfaces: args.includeSurfaces,
            includeLabels: args.includeLabels,
          })
        ),
    },
    grading_surface_volume: {
      action: "grading_surface_volume",
      inputSchema: z.object({
        action: z.literal("grading_surface_volume"),
        baseSurface: z.string(),
        comparisonSurface: z.string(),
        method: z.string().optional(),
      }),
      capabilities: ["query", "analyze", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["computeSurfaceVolume"],
      execute: async (args: any) => {
        const volumeResult = await SURFACE_DOMAIN_DEFINITION.actions.volume_calculate.execute({
          action: "volume_calculate",
          baseSurface: args.baseSurface,
          comparisonSurface: args.comparisonSurface,
          method: args.method,
        });

        return buildWorkflowResult(
          "grading_surface_volume",
          `Calculated grading/earthwork volume between '${args.baseSurface}' and '${args.comparisonSurface}'.`,
          [
            {
              name: "Calculate surface-to-surface volume",
              action: "surface.volume_calculate",
              status: "completed",
              result: volumeResult,
            },
          ],
          { volume: volumeResult }
        );
      },
    },
    surface_comparison_report: {
      action: "surface_comparison_report",
      inputSchema: z.object({
        action: z.literal("surface_comparison_report"),
        baseSurface: z.string(),
        comparisonSurface: z.string(),
        format: z.enum(["summary", "detailed"]).optional(),
      }),
      capabilities: ["query", "analyze", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["surfaceComparisonReportWorkflow"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("surfaceComparisonReportWorkflow", {
            baseSurface: args.baseSurface,
            comparisonSurface: args.comparisonSurface,
            format: args.format,
          })
        ),
    },
    data_shortcut_publish_sync: {
      action: "data_shortcut_publish_sync",
      inputSchema: z.object({
        action: z.literal("data_shortcut_publish_sync"),
        objectType: DataShortcutObjectTypeSchema,
        objectName: z.string(),
        shortcutName: z.string().optional(),
        description: z.string().optional(),
        projectFolder: z.string().optional(),
        dryRun: z.boolean().optional(),
      }),
      capabilities: ["create", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["dataShortcutPublishSyncWorkflow"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("dataShortcutPublishSyncWorkflow", {
            objectType: args.objectType,
            objectName: args.objectName,
            shortcutName: args.shortcutName,
            description: args.description,
            projectFolder: args.projectFolder,
            dryRun: args.dryRun,
          })
        ),
    },
    data_shortcut_reference_sync: {
      action: "data_shortcut_reference_sync",
      inputSchema: z.object({
        action: z.literal("data_shortcut_reference_sync"),
        projectFolder: z.string(),
        shortcutName: z.string(),
        shortcutType: DataShortcutObjectTypeSchema,
        layer: z.string().optional(),
        dryRun: z.boolean().optional(),
      }),
      capabilities: ["create", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["dataShortcutReferenceSyncWorkflow"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("dataShortcutReferenceSyncWorkflow", {
            projectFolder: args.projectFolder,
            shortcutName: args.shortcutName,
            shortcutType: args.shortcutType,
            layer: args.layer,
            dryRun: args.dryRun,
          })
        ),
    },
    project_startup: {
      action: "project_startup",
      inputSchema: z.object({
        action: z.literal("project_startup"),
        templatePath: z.string().optional(),
        save: z.boolean().optional(),
      }),
      capabilities: ["query", "inspect", "manage"],
      requiresActiveDrawing: false,
      safeForRetry: false,
      pluginMethods: ["projectStartupWorkflow"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("projectStartupWorkflow", {
            templatePath: args.templatePath,
            save: args.save ?? false,
          })
        ),
    },
    project_reference_setup: {
      action: "project_reference_setup",
      inputSchema: z.object({
        action: z.literal("project_reference_setup"),
        references: z.array(ShortcutReferenceSchema).min(1),
        dryRun: z.boolean().optional(),
        save: z.boolean().optional(),
      }),
      capabilities: ["create", "manage", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["projectReferenceSetupWorkflow"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("projectReferenceSetupWorkflow", {
            references: args.references,
            dryRun: args.dryRun,
            save: args.save ?? false,
          })
        ),
    },
    drawing_readiness_audit: {
      action: "drawing_readiness_audit",
      inputSchema: z.object({
        action: z.literal("drawing_readiness_audit"),
        layerPrefix: z.string().optional(),
        checkLineweights: z.boolean().optional(),
        checkColors: z.boolean().optional(),
        limit: z.number().int().positive().optional(),
      }),
      capabilities: ["query", "inspect", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["drawingReadinessAuditWorkflow"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("drawingReadinessAuditWorkflow", {
            layerPrefix: args.layerPrefix,
            checkLineweights: args.checkLineweights,
            checkColors: args.checkColors,
            limit: args.limit,
          })
        ),
    },
    feature_line_to_grading: {
      action: "feature_line_to_grading",
      inputSchema: z.object({
        action: z.literal("feature_line_to_grading"),
        featureLineName: z.string(),
        groupName: z.string(),
        groupDescription: z.string().optional(),
        createGroup: z.boolean().optional(),
        useProjection: z.boolean().optional(),
        criteriaName: z.string().optional(),
        side: GradingSideSchema.optional(),
        surfaceName: z.string().optional(),
      }),
      capabilities: ["query", "create", "edit", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["featureLineToGradingWorkflow"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("featureLineToGradingWorkflow", {
            featureLineName: args.featureLineName,
            groupName: args.groupName,
            groupDescription: args.groupDescription,
            createGroup: args.createGroup,
            useProjection: args.useProjection,
            criteriaName: args.criteriaName,
            side: args.side,
            surfaceName: args.surfaceName,
          })
        ),
    },
    qc_fix_and_verify: {
      action: "qc_fix_and_verify",
      inputSchema: z.object({
        action: z.literal("qc_fix_and_verify"),
        layerPrefix: z.string().optional(),
        checkLineweights: z.boolean().optional(),
        checkColors: z.boolean().optional(),
        fixSpaces: z.boolean().optional(),
        maxNameLength: z.number().int().positive().optional(),
        colorIndex: z.number().int().min(1).max(255).optional(),
        lineweight: z.number().int().optional(),
        dryRun: z.boolean().optional(),
      }),
      capabilities: ["query", "analyze", "edit", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["qcFixAndVerifyWorkflow"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("qcFixAndVerifyWorkflow", {
            layerPrefix: args.layerPrefix,
            checkLineweights: args.checkLineweights,
            checkColors: args.checkColors,
            fixSpaces: args.fixSpaces,
            maxNameLength: args.maxNameLength,
            colorIndex: args.colorIndex,
            lineweight: args.lineweight,
            dryRun: args.dryRun,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_workflow",
      displayName: "Civil 3D Workflow",
      description:
        "Runs multi-step Civil 3D workflows that compose existing QC, grading, surface, data " +
        "shortcut, and drawing operations in one call. Actions: corridor_qc_report (corridor QC " +
        "check + optional consolidated report file), grading_surface_volume (thin wrapper over " +
        "civil3d_surface volume_calculate), surface_comparison_report (surface lookups + volume " +
        "+ formatted report), data_shortcut_publish_sync / data_shortcut_reference_sync (publish " +
        "or reference a shortcut then immediately sync it), project_startup (health check + " +
        "optional new drawing + drawing/settings/object-type/data-shortcut inspection + optional " +
        "save), project_reference_setup (reference one or more shortcuts, sync, list, optional " +
        "save), drawing_readiness_audit (health + info + settings + object types + selection + " +
        "standards audit), feature_line_to_grading (inspect feature line, optional grading-group " +
        "create, create grading, optional grading surface), qc_fix_and_verify (audit, fix, " +
        "re-audit drawing standards). Note: templatePath (project_startup) and save " +
        "(project_startup/project_reference_setup) surface real limitations of the underlying " +
        "primitives — creating a new drawing via MCP is not supported (requires user " +
        "interaction), and saving always writes the current file in place rather than to a new " +
        "path — the workflow reports this rather than failing silently. Two source workflows are " +
        "NOT available: plan_production_publish (PDF/sheet-set export requires a complete " +
        "AutoCAD PlotEngine transaction — confirmed unavailable, not attempted here) and " +
        "pipe_network_design (needs a working pipe parts-catalog listing, which is itself an " +
        "unresolved gap in civil3d_pipe's own list_parts_lists action).",
      inputShape: canonicalInputShape,
      supportedActions: [
        "corridor_qc_report",
        "grading_surface_volume",
        "surface_comparison_report",
        "data_shortcut_publish_sync",
        "data_shortcut_reference_sync",
        "project_startup",
        "project_reference_setup",
        "drawing_readiness_audit",
        "feature_line_to_grading",
        "qc_fix_and_verify",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
