import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const GradingActionSchema = z.enum([
  "list_groups",
  "get_group",
  "delete_group",
  "create_group",
  "list_feature_lines",
  "get_feature_line",
  "delete_feature_line",
  "create_feature_line",
]);

const canonicalInputShape = {
  action: GradingActionSchema.describe("The grading operation to perform."),
  name: z.string().optional().describe("Grading group or feature line name (for get/delete/create)."),
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
      inputSchema: z.object({ action: z.literal("create_group"), name: z.string().optional() }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createGradingGroup"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createGradingGroup", { name: args.name })
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
      inputSchema: z.object({ action: z.literal("create_feature_line"), name: z.string().optional() }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createFeatureLine"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createFeatureLine", { name: args.name })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_grading",
      displayName: "Civil 3D Grading",
      description:
        "Manage Civil 3D grading. Actions: list_feature_lines, get_feature_line (by name), " +
        "delete_feature_line are real. Note: list_groups, get_group, delete_group, " +
        "create_group, and create_feature_line are not yet implemented — they return a " +
        "'planned' status. Grading Group listing in particular needs its real API accessor " +
        "confirmed against a live Civil 3D drawing (the initial guess, Site.GetGradingGroupIds() " +
        "with a GradingGroup type, does not compile). Grading criteria sets (talud/relleno) " +
        "are not covered by this tool at all.",
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
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
