import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const Point3DSchema = z.object({ x: z.number(), y: z.number(), z: z.number() });

const GradingActionSchema = z.enum([
  "list_groups",
  "get_group",
  "delete_group",
  "create_group",
  "list_feature_lines",
  "get_feature_line",
  "delete_feature_line",
  "create_feature_line",
  "group_volume",
  "group_surface_create",
  "list_gradings",
  "get_grading",
  "create_grading",
  "delete_grading",
  "list_grading_criteria",
]);

const canonicalInputShape = {
  action: GradingActionSchema.describe("The grading operation to perform."),
  name: z.string().optional().describe("Grading group or feature line name (for get/delete/create/group_volume/group_surface_create)."),
  description: z.string().optional().describe("Grading group description (create_group)."),
  useProjection: z.boolean().optional().describe("Use projection method for the new grading group (create_group)."),
  layer: z.string().optional().describe("Layer for the new feature line (create_feature_line)."),
  points: z.array(Point3DSchema).min(2).optional().describe("Vertices, at least 2 (create_feature_line)."),
  surfaceName: z.string().optional().describe("Name for the surface created from the grading group (group_surface_create)."),
  groupName: z.string().optional().describe("Grading group name (list_gradings/get_grading/create_grading/delete_grading)."),
  handle: z.string().optional().describe("Grading object handle (get_grading/delete_grading)."),
  featureLineName: z.string().optional().describe("Feature line to grade against (create_grading)."),
  criteriaName: z.string().optional().describe("Grading criteria name (create_grading)."),
  side: z.enum(["left", "right", "both"]).optional().describe("Grading side (create_grading)."),
};

export const GRADING_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "grading",
  actions: {
    list_groups: {
      action: "list_groups",
      inputSchema: z.object({ action: z.literal("list_groups") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listGradingGroups"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listGradingGroups", {})
        ),
    },
    get_group: {
      action: "get_group",
      inputSchema: z.object({ action: z.literal("get_group"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getGradingGroup"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getGradingGroup", { name: args.name })
        ),
    },
    delete_group: {
      action: "delete_group",
      inputSchema: z.object({ action: z.literal("delete_group"), name: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteGradingGroup"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteGradingGroup", { name: args.name })
        ),
    },
    create_group: {
      action: "create_group",
      inputSchema: z.object({
        action: z.literal("create_group"),
        name: z.string(),
        description: z.string().optional(),
        useProjection: z.boolean().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createGradingGroup"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createGradingGroup", {
            name: args.name,
            description: args.description,
            useProjection: args.useProjection ?? false,
          })
        ),
    },
    list_feature_lines: {
      action: "list_feature_lines",
      inputSchema: z.object({ action: z.literal("list_feature_lines") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listFeatureLines"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listFeatureLines", {})
        ),
    },
    get_feature_line: {
      action: "get_feature_line",
      inputSchema: z.object({ action: z.literal("get_feature_line"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getFeatureLine"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getFeatureLine", { name: args.name })
        ),
    },
    delete_feature_line: {
      action: "delete_feature_line",
      inputSchema: z.object({ action: z.literal("delete_feature_line"), name: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteFeatureLine"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteFeatureLine", { name: args.name })
        ),
    },
    create_feature_line: {
      action: "create_feature_line",
      inputSchema: z.object({
        action: z.literal("create_feature_line"),
        name: z.string().optional(),
        layer: z.string().optional(),
        points: z.array(Point3DSchema).min(2),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createFeatureLine"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createFeatureLine", {
            name: args.name,
            layer: args.layer ?? "0",
            points: args.points,
          })
        ),
    },
    group_volume: {
      action: "group_volume",
      inputSchema: z.object({ action: z.literal("group_volume"), name: z.string() }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getGradingGroupVolume"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getGradingGroupVolume", { name: args.name })
        ),
    },
    group_surface_create: {
      action: "group_surface_create",
      inputSchema: z.object({
        action: z.literal("group_surface_create"),
        name: z.string(),
        surfaceName: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createSurfaceFromGradingGroup"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createSurfaceFromGradingGroup", { name: args.name, surfaceName: args.surfaceName })
        ),
    },
    list_gradings: {
      action: "list_gradings",
      inputSchema: z.object({ action: z.literal("list_gradings"), groupName: z.string() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listGradings"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listGradings", { groupName: args.groupName })
        ),
    },
    get_grading: {
      action: "get_grading",
      inputSchema: z.object({ action: z.literal("get_grading"), groupName: z.string(), handle: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getGrading"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getGrading", { groupName: args.groupName, handle: args.handle })
        ),
    },
    create_grading: {
      action: "create_grading",
      inputSchema: z.object({
        action: z.literal("create_grading"),
        groupName: z.string(),
        featureLineName: z.string(),
        criteriaName: z.string().optional(),
        side: z.enum(["left", "right", "both"]).optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createGrading"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createGrading", {
            groupName: args.groupName,
            featureLineName: args.featureLineName,
            criteriaName: args.criteriaName,
            side: args.side ?? "right",
          })
        ),
    },
    delete_grading: {
      action: "delete_grading",
      inputSchema: z.object({ action: z.literal("delete_grading"), groupName: z.string(), handle: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteGrading"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteGrading", { groupName: args.groupName, handle: args.handle })
        ),
    },
    list_grading_criteria: {
      action: "list_grading_criteria",
      inputSchema: z.object({ action: z.literal("list_grading_criteria") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listGradingCriteria"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listGradingCriteria", {})
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_grading",
      displayName: "Civil 3D Grading",
      description:
        "Manage Civil 3D grading. Actions: list_groups/get_group/create_group/delete_group " +
        "(Grading Groups, via Site.GradingGroups), list_feature_lines/get_feature_line/" +
        "delete_feature_line/create_feature_line, group_volume (cut/fill), " +
        "group_surface_create (detach an independent surface from a grading group), " +
        "list_gradings/get_grading/create_grading/delete_grading (individual Grading objects " +
        "within a group, distinct from the group itself), list_grading_criteria (criteria " +
        "sets/talud-relleno definitions available in the drawing).",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list_groups",
        "get_group",
        "delete_group",
        "create_group",
        "list_feature_lines",
        "get_feature_line",
        "delete_feature_line",
        "create_feature_line",
        "group_volume",
        "group_surface_create",
        "list_gradings",
        "get_grading",
        "create_grading",
        "delete_grading",
        "list_grading_criteria",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
