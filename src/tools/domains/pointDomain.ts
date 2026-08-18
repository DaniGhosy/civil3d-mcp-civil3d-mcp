import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const PointActionSchema = z.enum([
  "list",
  "get",
  "create",
  "delete",
  "list_groups",
  "create_group",
  "delete_group",
  "update_group",
  "import",
  "export",
  "description_keys",
  "transform",
]);

const canonicalInputShape = {
  action: PointActionSchema.describe("The point operation to perform."),
  pointNumber: z.number().optional().describe("Point number (for get/delete)."),
  pointNumbers: z.array(z.number().int().positive()).optional().describe("Point numbers to target (transform)."),
  description: z.string().optional().describe("Point group description (update_group)."),
  includeNumbers: z.string().optional().describe("PointGroup StandardQuery IncludeNumbers filter, e.g. '1-50' (update_group)."),
  excludeNumbers: z.string().optional().describe("PointGroup StandardQuery ExcludeNumbers filter (update_group)."),
  includeDescriptions: z.string().optional().describe("PointGroup StandardQuery IncludeRawDescriptions filter, e.g. 'TREE*' (update_group)."),
  translateX: z.number().optional().describe("Translation along X (transform)."),
  translateY: z.number().optional().describe("Translation along Y (transform)."),
  translateZ: z.number().optional().describe("Translation along Z (transform)."),
  rotateRadians: z.number().optional().describe("Rotation about the origin, radians (transform)."),
  scaleFactor: z.number().optional().describe("Scale factor about the origin, default 1.0 (transform)."),
  easting: z.number().optional().describe("Easting (X) coordinate."),
  northing: z.number().optional().describe("Northing (Y) coordinate."),
  elevation: z.number().optional().describe("Elevation (Z) coordinate."),
  rawDescription: z.string().optional().describe("Raw description for the point."),
  points: z
    .array(
      z.object({
        easting: z.number(),
        northing: z.number(),
        elevation: z.number().optional(),
        rawDescription: z.string().optional(),
      })
    )
    .optional()
    .describe("Array of points (for batch create)."),
  groupName: z.string().optional().describe("Point group name."),
  filePath: z.string().optional().describe("File path for import."),
  format: z.string().optional().describe("Import format (e.g. PNEZD)."),
  limit: z.number().optional().describe("Max points to return."),
};

export const POINT_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "point",
  actions: {
    list: {
      action: "list",
      inputSchema: z.object({
        action: z.literal("list"),
        groupName: z.string().optional(),
        limit: z.number().optional(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listCogoPoints"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listCogoPoints", {
            groupName: args.groupName,
            limit: args.limit ?? 500,
          })
        ),
    },
    get: {
      action: "get",
      inputSchema: z.object({
        action: z.literal("get"),
        pointNumber: z.number(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getCogoPoint"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getCogoPoint", { pointNumber: args.pointNumber })
        ),
    },
    create: {
      action: "create",
      inputSchema: z.object({
        action: z.literal("create"),
        points: z.array(
          z.object({
            easting: z.number(),
            northing: z.number(),
            elevation: z.number().optional(),
            rawDescription: z.string().optional(),
          })
        ),
        groupName: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createCogoPoints"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createCogoPoints", {
            points: args.points,
            groupName: args.groupName,
          })
        ),
    },
    delete: {
      action: "delete",
      inputSchema: z.object({
        action: z.literal("delete"),
        pointNumber: z.number(),
      }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteCogoPoints"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteCogoPoints", {
            pointNumbers: [args.pointNumber],
          })
        ),
    },
    list_groups: {
      action: "list_groups",
      inputSchema: z.object({ action: z.literal("list_groups") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listPointGroups"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listPointGroups", {})
        ),
    },
    create_group: {
      action: "create_group",
      inputSchema: z.object({
        action: z.literal("create_group"),
        groupName: z.string(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createPointGroup"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createPointGroup", { name: args.groupName })
        ),
    },
    delete_group: {
      action: "delete_group",
      inputSchema: z.object({
        action: z.literal("delete_group"),
        groupName: z.string(),
      }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deletePointGroup"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deletePointGroup", { name: args.groupName })
        ),
    },
    update_group: {
      action: "update_group",
      inputSchema: z.object({
        action: z.literal("update_group"),
        groupName: z.string(),
        description: z.string().optional(),
        includeNumbers: z.string().optional(),
        excludeNumbers: z.string().optional(),
        includeDescriptions: z.string().optional(),
      }),
      capabilities: ["edit", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["updatePointGroup"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("updatePointGroup", {
            name: args.groupName,
            description: args.description,
            includeNumbers: args.includeNumbers,
            excludeNumbers: args.excludeNumbers,
            includeDescriptions: args.includeDescriptions,
          })
        ),
    },
    transform: {
      action: "transform",
      inputSchema: z.object({
        action: z.literal("transform"),
        pointNumbers: z.array(z.number().int().positive()).optional(),
        groupName: z.string().optional(),
        translateX: z.number().optional(),
        translateY: z.number().optional(),
        translateZ: z.number().optional(),
        rotateRadians: z.number().optional(),
        scaleFactor: z.number().optional(),
      }),
      capabilities: ["edit", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["transformCogoPoints"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("transformCogoPoints", {
            pointNumbers: args.pointNumbers,
            groupName: args.groupName,
            translateX: args.translateX ?? 0,
            translateY: args.translateY ?? 0,
            translateZ: args.translateZ ?? 0,
            rotateRadians: args.rotateRadians ?? 0,
            scaleFactor: args.scaleFactor ?? 1.0,
          })
        ),
    },
    description_keys: {
      action: "description_keys",
      inputSchema: z.object({ action: z.literal("description_keys") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getDescriptionKeySets"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getDescriptionKeySets", {})
        ),
    },
    import: {
      action: "import",
      inputSchema: z.object({
        action: z.literal("import"),
        filePath: z.string(),
        format: z.string().optional(),
      }),
      capabilities: ["import", "create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["importCogoPoints"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("importCogoPoints", {
            filePath: args.filePath,
            format: args.format ?? "PNEZD",
          })
        ),
    },
    export: {
      action: "export",
      inputSchema: z.object({
        action: z.literal("export"),
        filePath: z.string(),
        format: z.string().optional(),
        groupName: z.string().optional(),
      }),
      capabilities: ["export"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["exportCogoPoints"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("exportCogoPoints", {
            filePath: args.filePath,
            format: args.format ?? "PNEZD",
            groupName: args.groupName,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_point",
      displayName: "Civil 3D COGO Points",
      description:
        "Manage COGO (Coordinate Geometry) points. Actions: list (optionally filtered by " +
        "groupName, which only returns points actually included in that point group), " +
        "get (by point number), create (single or batch), delete, list_groups, create_group, " +
        "update_group (edit description and/or StandardPointGroupQuery filters — fails if the " +
        "group uses a custom query, to avoid discarding its QueryString), delete_group, " +
        "transform (translate/rotate/scale a set of points by pointNumbers or groupName), " +
        "import (plain-text PNEZD/PENZD file — does not assign points to a group, group " +
        "membership assignment isn't confirmed against the API), export (plain-text " +
        "PNEZD/PENZD file, optionally filtered by groupName). Note: description_keys is not " +
        "yet implemented — it returns a 'planned' status until Description Key Set access is " +
        "confirmed against a live Civil 3D drawing.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list",
        "get",
        "create",
        "delete",
        "list_groups",
        "create_group",
        "update_group",
        "delete_group",
        "transform",
        "import",
        "export",
        "description_keys",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
