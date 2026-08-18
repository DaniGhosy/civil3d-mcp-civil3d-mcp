import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const Point3DSchema = z.object({ x: z.number(), y: z.number(), z: z.number() });

const PressurePipeActionSchema = z.enum([
  "list_networks",
  "get_network",
  "list_parts",
  "get_part",
  "create_network",
  "delete_network",
  "assign_parts_list",
  "set_cover",
  "validate",
  "export",
  "connect_networks",
  "add_pipe",
  "get_pipe_properties",
  "resize_pipe",
  "add_fitting",
  "get_fitting_properties",
  "add_appurtenance",
]);

const canonicalInputShape = {
  action: PressurePipeActionSchema.describe("The pressure pipe network operation to perform."),
  networkName: z.string().optional().describe("Pressure pipe network name."),
  partHandle: z.string().optional().describe("Pressure part handle (pipe, fitting, or appurtenance) — list_parts/get_part only."),
  partsList: z.string().optional().describe("Pressure parts list (catalog) name (create_network/assign_parts_list)."),
  layer: z.string().optional().describe("Layer for the new network (create_network)."),
  referenceSurface: z.string().optional().describe("Reference surface name (create_network)."),
  referenceAlignment: z.string().optional().describe("Reference alignment name (create_network)."),
  minCoverDepth: z.number().optional().describe("Minimum cover depth (set_cover)."),
  maxCoverDepth: z.number().optional().describe("Maximum cover depth (set_cover)."),
  includeCoordinates: z.boolean().optional().describe("Include start/end/position coordinates (export)."),
  targetNetwork: z.string().optional().describe("Target network to merge into (connect_networks)."),
  sourceNetwork: z.string().optional().describe("Source network to merge from (connect_networks)."),
  partName: z.string().optional().describe("Catalog part description to place (add_pipe/add_fitting/add_appurtenance)."),
  startPoint: Point3DSchema.optional().describe("Pipe start point (add_pipe)."),
  endPoint: Point3DSchema.optional().describe("Pipe end point (add_pipe)."),
  diameter: z.number().optional().describe("Expected inner diameter, validated against the catalog part (add_pipe)."),
  pipeName: z.string().optional().describe("Pipe name (get_pipe_properties/resize_pipe)."),
  newPartName: z.string().optional().describe("New catalog part description (resize_pipe)."),
  newDiameter: z.number().optional().describe("New inner diameter (resize_pipe)."),
  position: Point3DSchema.optional().describe("Fitting/appurtenance position (add_fitting/add_appurtenance)."),
  rotation: z.number().optional().describe("Rotation — must be 0, not supported by the managed API (add_fitting/add_appurtenance)."),
  fittingName: z.string().optional().describe("Fitting name (get_fitting_properties)."),
  onPipeName: z.string().optional().describe("Snap the appurtenance to this pipe's midpoint (add_appurtenance)."),
};

export const PRESSURE_PIPE_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "pipe",
  actions: {
    list_networks: {
      action: "list_networks",
      inputSchema: z.object({ action: z.literal("list_networks") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listPressureNetworks"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listPressureNetworks", {})
        ),
    },
    get_network: {
      action: "get_network",
      inputSchema: z.object({ action: z.literal("get_network"), networkName: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getPressureNetwork"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getPressureNetwork", { networkName: args.networkName })
        ),
    },
    list_parts: {
      action: "list_parts",
      inputSchema: z.object({ action: z.literal("list_parts"), networkName: z.string() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listPressureParts"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listPressureParts", { networkName: args.networkName })
        ),
    },
    get_part: {
      action: "get_part",
      inputSchema: z.object({
        action: z.literal("get_part"),
        networkName: z.string(),
        partHandle: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getPressurePart"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getPressurePart", {
            networkName: args.networkName,
            partHandle: args.partHandle,
          })
        ),
    },
    create_network: {
      action: "create_network",
      inputSchema: z.object({
        action: z.literal("create_network"),
        networkName: z.string(),
        partsList: z.string(),
        layer: z.string().optional(),
        referenceSurface: z.string().optional(),
        referenceAlignment: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createPressureNetwork"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createPressureNetwork", {
            networkName: args.networkName,
            partsList: args.partsList,
            layer: args.layer,
            referenceSurface: args.referenceSurface,
            referenceAlignment: args.referenceAlignment,
          })
        ),
    },
    delete_network: {
      action: "delete_network",
      inputSchema: z.object({ action: z.literal("delete_network"), networkName: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deletePressureNetwork"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deletePressureNetwork", { networkName: args.networkName })
        ),
    },
    assign_parts_list: {
      action: "assign_parts_list",
      inputSchema: z.object({
        action: z.literal("assign_parts_list"),
        networkName: z.string(),
        partsList: z.string(),
      }),
      capabilities: ["edit", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["assignPressurePartsList"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("assignPressurePartsList", { networkName: args.networkName, partsList: args.partsList })
        ),
    },
    set_cover: {
      action: "set_cover",
      inputSchema: z.object({
        action: z.literal("set_cover"),
        networkName: z.string(),
        minCoverDepth: z.number(),
        maxCoverDepth: z.number().optional(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["setPressureNetworkCover"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("setPressureNetworkCover", {
            networkName: args.networkName,
            minCoverDepth: args.minCoverDepth,
            maxCoverDepth: args.maxCoverDepth,
          })
        ),
    },
    validate: {
      action: "validate",
      inputSchema: z.object({ action: z.literal("validate"), networkName: z.string() }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["validatePressureNetwork"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("validatePressureNetwork", { networkName: args.networkName })
        ),
    },
    export: {
      action: "export",
      inputSchema: z.object({
        action: z.literal("export"),
        networkName: z.string(),
        includeCoordinates: z.boolean().optional(),
      }),
      capabilities: ["export"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["exportPressureNetwork"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("exportPressureNetwork", {
            networkName: args.networkName,
            includeCoordinates: args.includeCoordinates ?? true,
          })
        ),
    },
    connect_networks: {
      action: "connect_networks",
      inputSchema: z.object({
        action: z.literal("connect_networks"),
        targetNetwork: z.string(),
        sourceNetwork: z.string(),
      }),
      capabilities: ["edit", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["connectPressureNetworks"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("connectPressureNetworks", {
            targetNetwork: args.targetNetwork,
            sourceNetwork: args.sourceNetwork,
          })
        ),
    },
    add_pipe: {
      action: "add_pipe",
      inputSchema: z.object({
        action: z.literal("add_pipe"),
        networkName: z.string(),
        partName: z.string(),
        startPoint: Point3DSchema,
        endPoint: Point3DSchema,
        diameter: z.number().optional(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addPressurePipe"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addPressurePipe", {
            networkName: args.networkName,
            partName: args.partName,
            startPoint: args.startPoint,
            endPoint: args.endPoint,
            diameter: args.diameter,
          })
        ),
    },
    get_pipe_properties: {
      action: "get_pipe_properties",
      inputSchema: z.object({
        action: z.literal("get_pipe_properties"),
        networkName: z.string(),
        pipeName: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getPressurePipeProperties"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getPressurePipeProperties", { networkName: args.networkName, pipeName: args.pipeName })
        ),
    },
    resize_pipe: {
      action: "resize_pipe",
      inputSchema: z.object({
        action: z.literal("resize_pipe"),
        networkName: z.string(),
        pipeName: z.string(),
        newPartName: z.string(),
        newDiameter: z.number().optional(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["resizePressurePipe"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("resizePressurePipe", {
            networkName: args.networkName,
            pipeName: args.pipeName,
            newPartName: args.newPartName,
            newDiameter: args.newDiameter,
          })
        ),
    },
    add_fitting: {
      action: "add_fitting",
      inputSchema: z.object({
        action: z.literal("add_fitting"),
        networkName: z.string(),
        partName: z.string(),
        position: Point3DSchema,
        rotation: z.number().optional(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addPressureFitting"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addPressureFitting", {
            networkName: args.networkName,
            partName: args.partName,
            position: args.position,
            rotation: args.rotation ?? 0,
          })
        ),
    },
    get_fitting_properties: {
      action: "get_fitting_properties",
      inputSchema: z.object({
        action: z.literal("get_fitting_properties"),
        networkName: z.string(),
        fittingName: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getPressureFittingProperties"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getPressureFittingProperties", { networkName: args.networkName, fittingName: args.fittingName })
        ),
    },
    add_appurtenance: {
      action: "add_appurtenance",
      inputSchema: z.object({
        action: z.literal("add_appurtenance"),
        networkName: z.string(),
        partName: z.string(),
        position: Point3DSchema,
        rotation: z.number().optional(),
        onPipeName: z.string().optional(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addPressureAppurtenance"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addPressureAppurtenance", {
            networkName: args.networkName,
            partName: args.partName,
            position: args.position,
            rotation: args.rotation ?? 0,
            onPipeName: args.onPipeName,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_pressure_pipe",
      displayName: "Civil 3D Pressure Pipe Network",
      description:
        "Manage Civil 3D pressure pipe networks (a separate object model from gravity pipe " +
        "networks). list_networks/get_network/create_network/delete_network/" +
        "assign_parts_list/export/add_pipe/get_pipe_properties/add_fitting/" +
        "get_fitting_properties/add_appurtenance are real. Note: list_parts/get_part (this " +
        "server's own part-catalog enumeration by handle) stay 'planned' — use get_network's " +
        "partsList field for the assigned catalog name in the meantime. set_cover, validate, " +
        "connect_networks, and resize_pipe always return a capability error — the managed API " +
        "does not expose network-level cover setters, safe validation criteria, network " +
        "merging, or pipe resize/part-swap.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list_networks",
        "get_network",
        "list_parts",
        "get_part",
        "create_network",
        "delete_network",
        "assign_parts_list",
        "set_cover",
        "validate",
        "export",
        "connect_networks",
        "add_pipe",
        "get_pipe_properties",
        "resize_pipe",
        "add_fitting",
        "get_fitting_properties",
        "add_appurtenance",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
