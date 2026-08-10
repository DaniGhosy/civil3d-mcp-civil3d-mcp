import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const ProfileActionSchema = z.enum([
  "list",
  "get",
  "get_elevation",
  "create_from_surface",
  "create_layout",
  "create_tangent",
  "create_parabola",
  "list_entities",
  "delete",
]);

const canonicalInputShape = {
  action: ProfileActionSchema.describe("The profile operation to perform."),
  alignmentName: z.string().optional().describe("Parent alignment name."),
  name: z.string().optional().describe("Profile name."),
  station: z.number().optional().describe("Station for elevation query."),
  surfaceName: z.string().optional().describe("Surface to sample."),
  style: z.string().optional().describe("Profile style name (default 'Standard')."),
  labelSet: z.string().optional().describe("Profile label set style name (default 'Standard')."),
  layer: z.string().optional().describe("Layer name."),
  startStation: z.number().optional().describe("Start station (create_tangent/create_parabola)."),
  startElevation: z.number().optional().describe("Start elevation (create_tangent/create_parabola)."),
  endStation: z.number().optional().describe("End station (create_tangent/create_parabola)."),
  endElevation: z.number().optional().describe("End elevation (create_tangent/create_parabola)."),
  pviStation: z.number().optional().describe("PVI station, the vertex of the curve (create_parabola)."),
  pviElevation: z.number().optional().describe("PVI elevation, the vertex of the curve (create_parabola)."),
};

export const PROFILE_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "profile",
  actions: {
    list: {
      action: "list",
      inputSchema: z.object({
        action: z.literal("list"),
        alignmentName: z.string(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listProfiles"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listProfiles", { alignmentName: args.alignmentName })
        ),
    },
    get: {
      action: "get",
      inputSchema: z.object({
        action: z.literal("get"),
        alignmentName: z.string(),
        name: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getProfile"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getProfile", {
            alignmentName: args.alignmentName,
            name: args.name,
          })
        ),
    },
    get_elevation: {
      action: "get_elevation",
      inputSchema: z.object({
        action: z.literal("get_elevation"),
        alignmentName: z.string(),
        name: z.string(),
        station: z.number(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getProfileElevation"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getProfileElevation", {
            alignmentName: args.alignmentName,
            name: args.name,
            station: args.station,
          })
        ),
    },
    create_from_surface: {
      action: "create_from_surface",
      inputSchema: z.object({
        action: z.literal("create_from_surface"),
        alignmentName: z.string(),
        surfaceName: z.string(),
        name: z.string(),
        style: z.string().optional(),
        labelSet: z.string().optional(),
        layer: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createProfileFromSurface"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createProfileFromSurface", {
            alignmentName: args.alignmentName,
            surfaceName: args.surfaceName,
            name: args.name,
            style: args.style,
            labelSet: args.labelSet,
            layer: args.layer,
          })
        ),
    },
    create_layout: {
      action: "create_layout",
      inputSchema: z.object({
        action: z.literal("create_layout"),
        alignmentName: z.string(),
        name: z.string(),
        style: z.string().optional(),
        labelSet: z.string().optional(),
        layer: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createLayoutProfile"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createLayoutProfile", {
            alignmentName: args.alignmentName,
            name: args.name,
            style: args.style,
            labelSet: args.labelSet,
            layer: args.layer,
          })
        ),
    },
    create_tangent: {
      action: "create_tangent",
      inputSchema: z.object({
        action: z.literal("create_tangent"),
        alignmentName: z.string(),
        name: z.string(),
        startStation: z.number(),
        startElevation: z.number(),
        endStation: z.number(),
        endElevation: z.number(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addProfileTangent"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addProfileTangent", {
            alignmentName: args.alignmentName,
            name: args.name,
            startStation: args.startStation,
            startElevation: args.startElevation,
            endStation: args.endStation,
            endElevation: args.endElevation,
          })
        ),
    },
    create_parabola: {
      action: "create_parabola",
      inputSchema: z.object({
        action: z.literal("create_parabola"),
        alignmentName: z.string(),
        name: z.string(),
        startStation: z.number(),
        startElevation: z.number(),
        pviStation: z.number(),
        pviElevation: z.number(),
        endStation: z.number(),
        endElevation: z.number(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addProfileParabola"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addProfileParabola", {
            alignmentName: args.alignmentName,
            name: args.name,
            startStation: args.startStation,
            startElevation: args.startElevation,
            pviStation: args.pviStation,
            pviElevation: args.pviElevation,
            endStation: args.endStation,
            endElevation: args.endElevation,
          })
        ),
    },
    list_entities: {
      action: "list_entities",
      inputSchema: z.object({
        action: z.literal("list_entities"),
        alignmentName: z.string(),
        name: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listProfileEntities"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listProfileEntities", {
            alignmentName: args.alignmentName,
            name: args.name,
          })
        ),
    },
    delete: {
      action: "delete",
      inputSchema: z.object({
        action: z.literal("delete"),
        alignmentName: z.string(),
        name: z.string(),
      }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteProfile"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteProfile", {
            alignmentName: args.alignmentName,
            name: args.name,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_profile",
      displayName: "Civil 3D Profile",
      description:
        "Manage Civil 3D profiles (vertical geometry). Actions: list (by alignment), " +
        "get (by alignment + name), get_elevation (at station), create_from_surface, " +
        "create_layout (empty layout profile — created without a label set for now, since " +
        "civilDoc.Styles.ProfileLabelSetStyles does not exist under that name), " +
        "list_entities (inspect current vertical geometry). Note: create_tangent and " +
        "create_parabola are not yet implemented — the underlying API methods exist but take " +
        "a different argument count than guessed, so they return a 'planned' status until the " +
        "real overload is confirmed against a live Civil 3D drawing.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list",
        "get",
        "get_elevation",
        "create_from_surface",
        "create_layout",
        "create_tangent",
        "create_parabola",
        "list_entities",
        "delete",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
