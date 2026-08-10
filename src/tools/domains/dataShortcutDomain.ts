import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const DataShortcutActionSchema = z.enum([
  "get_project_id",
  "associate_project",
  "create_reference",
  "promote_reference",
]);

const canonicalInputShape = {
  action: DataShortcutActionSchema.describe("The data shortcut operation to perform."),
  projectPath: z.string().optional().describe("Data shortcuts project folder path (get_project_id, associate_project)."),
  sourceDrawing: z.string().optional().describe("Source drawing name the reference comes from (create_reference)."),
  entityName: z.string().optional().describe("Name of the surface/alignment/etc. to reference (create_reference)."),
  entityType: z.string().optional().describe("Data shortcut entity type, e.g. 'Surface', 'Alignment' (create_reference)."),
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
        "returns a 'planned' status until confirmed against a live Civil 3D drawing.",
      inputShape: canonicalInputShape,
      supportedActions: ["get_project_id", "associate_project", "create_reference", "promote_reference"],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
