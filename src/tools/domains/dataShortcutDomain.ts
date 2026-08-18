import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const DataShortcutObjectTypeSchema = z.enum([
  "surface",
  "alignment",
  "profile",
  "pipe_network",
  "pressure_network",
  "corridor",
]);

const DataShortcutActionSchema = z.enum([
  "get_project_id",
  "associate_project",
  "create_reference",
  "promote_reference",
  "list",
  "create",
  "reference",
  "sync",
  "promote",
]);

const canonicalInputShape = {
  action: DataShortcutActionSchema.describe("The data shortcut operation to perform."),
  projectPath: z.string().optional().describe("Data shortcuts project folder path (get_project_id, associate_project)."),
  sourceDrawing: z.string().optional().describe("Source drawing name the reference comes from (create_reference)."),
  entityName: z.string().optional().describe("Name of the surface/alignment/etc. to reference (create_reference)."),
  entityType: z.string().optional().describe("Data shortcut entity type, e.g. 'Surface', 'Alignment' (create_reference)."),
  objectType: DataShortcutObjectTypeSchema.optional().describe("Data shortcut object type (create)."),
  objectName: z.string().optional().describe("Object name to publish as a shortcut (create)."),
  description: z.string().optional().describe("Shortcut description (create)."),
  projectFolder: z.string().optional().describe("Data Shortcuts project folder (create/reference/sync)."),
  shortcutName: z.string().optional().describe("Shortcut name (reference/promote)."),
  shortcutType: DataShortcutObjectTypeSchema.optional().describe("Shortcut type (reference/promote)."),
  layer: z.string().optional().describe("Target layer for the new reference (reference)."),
  newName: z.string().optional().describe("New local name after promotion (promote)."),
  shortcutNames: z.array(z.string()).optional().describe("Filter to specific shortcut names (sync)."),
  dryRun: z.boolean().optional().describe("Report what would sync without launching the command (sync)."),
};

export const DATA_SHORTCUT_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "project",
  actions: {
    get_project_id: {
      action: "get_project_id",
      inputSchema: z.object({ action: z.literal("get_project_id"), projectPath: z.string() }),
      capabilities: ["query"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      pluginMethods: ["getDataShortcutProjectId"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getDataShortcutProjectId", { projectPath: args.projectPath })
        ),
    },
    associate_project: {
      action: "associate_project",
      inputSchema: z.object({ action: z.literal("associate_project"), projectPath: z.string() }),
      capabilities: ["manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["associateDataShortcutProject"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("associateDataShortcutProject", { projectPath: args.projectPath })
        ),
    },
    create_reference: {
      action: "create_reference",
      inputSchema: z.object({
        action: z.literal("create_reference"),
        sourceDrawing: z.string(),
        entityName: z.string(),
        entityType: z.string(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createDataReference"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createDataReference", {
            sourceDrawing: args.sourceDrawing,
            entityName: args.entityName,
            entityType: args.entityType,
          })
        ),
    },
    promote_reference: {
      action: "promote_reference",
      inputSchema: z.object({ action: z.literal("promote_reference") }),
      capabilities: ["manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["promoteDataReference"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("promoteDataReference", {})
        ),
    },
    list: {
      action: "list",
      inputSchema: z.object({ action: z.literal("list") }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listDataShortcuts"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listDataShortcuts", {})
        ),
    },
    create: {
      action: "create",
      inputSchema: z.object({
        action: z.literal("create"),
        objectType: DataShortcutObjectTypeSchema,
        objectName: z.string(),
        description: z.string().optional(),
        projectFolder: z.string().optional(),
      }),
      capabilities: ["create", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createDataShortcut"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createDataShortcut", {
            objectType: args.objectType,
            objectName: args.objectName,
            description: args.description,
            projectFolder: args.projectFolder,
          })
        ),
    },
    reference: {
      action: "reference",
      inputSchema: z.object({
        action: z.literal("reference"),
        projectFolder: z.string(),
        shortcutName: z.string(),
        shortcutType: DataShortcutObjectTypeSchema,
        layer: z.string().optional(),
      }),
      capabilities: ["create", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["referenceDataShortcut"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("referenceDataShortcut", {
            projectFolder: args.projectFolder,
            shortcutName: args.shortcutName,
            shortcutType: args.shortcutType,
            layer: args.layer,
          })
        ),
    },
    sync: {
      action: "sync",
      inputSchema: z.object({
        action: z.literal("sync"),
        projectFolder: z.string().optional(),
        shortcutNames: z.array(z.string()).optional(),
        dryRun: z.boolean().optional(),
      }),
      capabilities: ["manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["syncDataShortcuts"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("syncDataShortcuts", {
            projectFolder: args.projectFolder,
            shortcutNames: args.shortcutNames,
            dryRun: args.dryRun ?? false,
          })
        ),
    },
    promote: {
      action: "promote",
      inputSchema: z.object({
        action: z.literal("promote"),
        shortcutName: z.string(),
        shortcutType: DataShortcutObjectTypeSchema,
        newName: z.string().optional(),
      }),
      capabilities: ["create", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["promoteDataShortcut"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("promoteDataShortcut", {
            shortcutName: args.shortcutName,
            shortcutType: args.shortcutType,
            newName: args.newName,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_data_shortcut",
      displayName: "Civil 3D Data Shortcuts",
      description:
        "Manage Civil 3D data shortcuts (shared references to surfaces/alignments/etc. " +
        "between drawings). Actions: get_project_id, associate_project (both real), " +
        "promote_reference (runs the AutoCAD _PROMOTEREFERENCE command on the current " +
        "selection). Note: create_reference is not yet implemented — the compiler confirmed " +
        "'DataShortcutManager' isn't the real class name for creating a data reference, so it " +
        "returns a 'planned' status until confirmed against a live Civil 3D drawing. Separately: " +
        "list (real, reflection-based inventory of incoming references and exportable objects), " +
        "create/reference/sync/promote — these launch the real Civil 3D dialog commands " +
        "(CreateDataShortcuts/CreateXxxReference/SynchronizeReferences) via SendStringToExecute " +
        "and report status 'initiated' or 'manual_step_required'; Civil 3D still requires user " +
        "confirmation in the dialog, this is not fully hands-off automation.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "get_project_id",
        "associate_project",
        "create_reference",
        "promote_reference",
        "list",
        "create",
        "reference",
        "sync",
        "promote",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
