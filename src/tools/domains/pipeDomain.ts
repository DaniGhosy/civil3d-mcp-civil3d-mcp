import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const PipeActionSchema = z.enum([
  "list_networks",
  "get_network",
  "create_network",
  "list_pipes",
  "get_pipe",
  "list_structures",
  "get_structure",
  "add_pipe",
  "add_structure",
  "list_parts_lists",
  "get_rule_set",
  "get_overridden_rules",
  "check_interference",
  "resize_pipe",
  "hgl_calculate",
  "hydraulic_analysis",
  "structure_properties",
]);

const canonicalInputShape = {
  action: PipeActionSchema.describe("The pipe network operation to perform."),
  networkName: z.string().optional().describe("Pipe network name."),
  pipeHandle: z.string().optional().describe("Pipe handle (hex string)."),
  structureHandle: z.string().optional().describe("Structure handle (hex string)."),
  name: z.string().optional().describe("Name for a new network."),
  partFamilyName: z.string().optional().describe("Part family name (from a parts list) for add_pipe/add_structure."),
  partSizeName: z.string().optional().describe("Part size name for add_pipe/add_structure."),
  startStructureHandle: z.string().optional().describe("Start structure handle for a new pipe."),
  endStructureHandle: z.string().optional().describe("End structure handle for a new pipe."),
  insertionX: z.number().optional().describe("Insertion X for a new structure."),
  insertionY: z.number().optional().describe("Insertion Y for a new structure."),
  insertionZ: z.number().optional().describe("Insertion Z for a new structure (default 0)."),
  newPartName: z.string().optional().describe("New catalog part name (resize_pipe)."),
  newDiameter: z.number().optional().describe("New inner diameter (resize_pipe)."),
  tailwaterElevation: z.number().optional().describe("Downstream tailwater elevation, defaults to outlet invert (hgl_calculate)."),
  designFlow: z.number().optional().describe("Design flow in CFS, defaults to full-flow capacity (hgl_calculate/hydraulic_analysis)."),
  manningsN: z.number().optional().describe("Manning's roughness coefficient, default 0.013 (hgl_calculate/hydraulic_analysis)."),
  minCoverDepth: z.number().optional().describe("Minimum cover depth check (hydraulic_analysis)."),
  minVelocity: z.number().optional().describe("Minimum velocity check, ft/s (hydraulic_analysis)."),
  maxVelocity: z.number().optional().describe("Maximum velocity check, ft/s (hydraulic_analysis)."),
  minSlope: z.number().optional().describe("Minimum slope check, percent (hydraulic_analysis)."),
  structureName: z.string().optional().describe("Structure name (structure_properties)."),
};

export const PIPE_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "pipe",
  actions: {
    list_networks: {
      action: "list_networks",
      inputSchema: z.object({ action: z.literal("list_networks") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listPipeNetworks"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listPipeNetworks", {})
        ),
    },
    get_network: {
      action: "get_network",
      inputSchema: z.object({
        action: z.literal("get_network"),
        networkName: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getPipeNetwork"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getPipeNetwork", { networkName: args.networkName })
        ),
    },
    create_network: {
      action: "create_network",
      inputSchema: z.object({
        action: z.literal("create_network"),
        name: z.string(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createPipeNetwork"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createPipeNetwork", { name: args.name })
        ),
    },
    list_pipes: {
      action: "list_pipes",
      inputSchema: z.object({
        action: z.literal("list_pipes"),
        networkName: z.string(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listPipes"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listPipes", { networkName: args.networkName })
        ),
    },
    get_pipe: {
      action: "get_pipe",
      inputSchema: z.object({
        action: z.literal("get_pipe"),
        networkName: z.string(),
        pipeHandle: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getPipe"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getPipe", {
            networkName: args.networkName,
            pipeHandle: args.pipeHandle,
          })
        ),
    },
    list_structures: {
      action: "list_structures",
      inputSchema: z.object({
        action: z.literal("list_structures"),
        networkName: z.string(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listStructures"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listStructures", { networkName: args.networkName })
        ),
    },
    get_structure: {
      action: "get_structure",
      inputSchema: z.object({
        action: z.literal("get_structure"),
        networkName: z.string(),
        structureHandle: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getStructure"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getStructure", {
            networkName: args.networkName,
            structureHandle: args.structureHandle,
          })
        ),
    },
    add_structure: {
      action: "add_structure",
      inputSchema: z.object({
        action: z.literal("add_structure"),
        networkName: z.string(),
        partFamilyName: z.string(),
        partSizeName: z.string(),
        insertionX: z.number(),
        insertionY: z.number(),
        insertionZ: z.number().optional(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addStructureToNetwork"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addStructureToNetwork", {
            networkName: args.networkName,
            partFamilyName: args.partFamilyName,
            partSizeName: args.partSizeName,
            insertionX: args.insertionX,
            insertionY: args.insertionY,
            insertionZ: args.insertionZ,
          })
        ),
    },
    add_pipe: {
      action: "add_pipe",
      inputSchema: z.object({
        action: z.literal("add_pipe"),
        networkName: z.string(),
        partFamilyName: z.string(),
        partSizeName: z.string(),
        startStructureHandle: z.string(),
        endStructureHandle: z.string(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addPipeToNetwork"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addPipeToNetwork", {
            networkName: args.networkName,
            partFamilyName: args.partFamilyName,
            partSizeName: args.partSizeName,
            startStructureHandle: args.startStructureHandle,
            endStructureHandle: args.endStructureHandle,
          })
        ),
    },
    list_parts_lists: {
      action: "list_parts_lists",
      inputSchema: z.object({ action: z.literal("list_parts_lists") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listPartsLists"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listPartsLists", {})
        ),
    },
    get_rule_set: {
      action: "get_rule_set",
      inputSchema: z.object({
        action: z.literal("get_rule_set"),
        networkName: z.string(),
        pipeHandle: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getPipeRuleSet"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getPipeRuleSet", {
            networkName: args.networkName,
            pipeHandle: args.pipeHandle,
          })
        ),
    },
    get_overridden_rules: {
      action: "get_overridden_rules",
      inputSchema: z.object({
        action: z.literal("get_overridden_rules"),
        networkName: z.string(),
        pipeHandle: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getPipeOverriddenRules"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getPipeOverriddenRules", {
            networkName: args.networkName,
            pipeHandle: args.pipeHandle,
          })
        ),
    },
    check_interference: {
      action: "check_interference",
      inputSchema: z.object({
        action: z.literal("check_interference"),
        networkName: z.string(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["checkPipeNetworkInterference"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("checkPipeNetworkInterference", {
            networkName: args.networkName,
          })
        ),
    },
    resize_pipe: {
      action: "resize_pipe",
      inputSchema: z.object({
        action: z.literal("resize_pipe"),
        networkName: z.string(),
        pipeHandle: z.string(),
        newPartName: z.string().optional(),
        newDiameter: z.number().optional(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["resizePipeInNetwork"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("resizePipeInNetwork", {
            networkName: args.networkName,
            pipeHandle: args.pipeHandle,
            newPartName: args.newPartName,
            newDiameter: args.newDiameter,
          })
        ),
    },
    hgl_calculate: {
      action: "hgl_calculate",
      inputSchema: z.object({
        action: z.literal("hgl_calculate"),
        networkName: z.string(),
        tailwaterElevation: z.number().optional(),
        designFlow: z.number().optional(),
        manningsN: z.number().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["calculatePipeNetworkHgl"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("calculatePipeNetworkHgl", {
            networkName: args.networkName,
            tailwaterElevation: args.tailwaterElevation,
            designFlow: args.designFlow,
            manningsN: args.manningsN ?? 0.013,
          })
        ),
    },
    hydraulic_analysis: {
      action: "hydraulic_analysis",
      inputSchema: z.object({
        action: z.literal("hydraulic_analysis"),
        networkName: z.string(),
        designFlow: z.number().optional(),
        manningsN: z.number().optional(),
        minCoverDepth: z.number().optional(),
        minVelocity: z.number().optional(),
        maxVelocity: z.number().optional(),
        minSlope: z.number().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["analyzePipeNetworkHydraulics"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("analyzePipeNetworkHydraulics", {
            networkName: args.networkName,
            designFlow: args.designFlow,
            manningsN: args.manningsN ?? 0.013,
            minCoverDepth: args.minCoverDepth ?? 2.0,
            minVelocity: args.minVelocity ?? 2.0,
            maxVelocity: args.maxVelocity ?? 10.0,
            minSlope: args.minSlope ?? 0.5,
          })
        ),
    },
    structure_properties: {
      action: "structure_properties",
      inputSchema: z.object({
        action: z.literal("structure_properties"),
        networkName: z.string(),
        structureName: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getPipeStructureProperties"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getPipeStructureProperties", {
            networkName: args.networkName,
            structureName: args.structureName,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_pipe",
      displayName: "Civil 3D Pipe Network",
      description:
        "Manage gravity pipe networks. Actions: list_networks, get_network, list_pipes, " +
        "get_pipe (by handle), list_structures, get_structure (by handle), get_rule_set " +
        "(whether a pipe overrides its network rule set), get_overridden_rules (a pipe's " +
        "overridden design rules), resize_pipe, hgl_calculate (Manning's-based HGL backwater " +
        "walk), hydraulic_analysis (capacity/velocity/slope checks per pipe), " +
        "structure_properties (rim/sump/connected pipes by structure name) are real. Note: " +
        "create_network, add_structure, add_pipe, list_parts_lists, and check_interference are " +
        "not yet implemented — the compiler confirmed the guessed signatures/type names don't " +
        "match reality (e.g. Network.AddStructure exists but takes part family/size ObjectIds, " +
        "not strings, plus a rotation and a ref/bool pair). They return a 'planned' status with " +
        "the real signature fragments discovered so far, until confirmed against a live Civil " +
        "3D drawing. For real pipe cover depth, combine get_pipe/get_structure (gives " +
        "elevations) with civil3d_surface's get_elevation (gives ground elevation at that X,Y) " +
        "— subtract instead of a dedicated command.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list_networks",
        "get_network",
        "create_network",
        "list_pipes",
        "get_pipe",
        "list_structures",
        "get_structure",
        "add_pipe",
        "add_structure",
        "list_parts_lists",
        "get_rule_set",
        "get_overridden_rules",
        "check_interference",
        "resize_pipe",
        "hgl_calculate",
        "hydraulic_analysis",
        "structure_properties",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
