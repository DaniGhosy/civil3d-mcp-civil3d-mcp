import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const PressurePipeActionSchema = z.enum([
  "list_networks",
  "get_network",
  "list_parts",
  "get_part",
  "create_network",
]);

const canonicalInputShape = {
  action: PressurePipeActionSchema.describe("The pressure pipe network operation to perform."),
  networkName: z.string().optional().describe("Pressure pipe network name."),
  partHandle: z.string().optional().describe("Pressure part handle (pipe, fitting, or appurtenance)."),
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
      inputSchema: z.object({ action: z.literal("create_network"), networkName: z.string().optional() }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createPressureNetwork"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createPressureNetwork", { networkName: args.networkName })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_pressure_pipe",
      displayName: "Civil 3D Pressure Pipe Network",
      description:
        "Manage Civil 3D pressure pipe networks (a separate object model from gravity pipe " +
        "networks). AeccPressurePipesMgd.dll is now referenced in the plugin and the " +
        "PressurePipeNetwork type resolves, but every action here currently returns a " +
        "'planned' status: the real namespace of the network-listing extension method " +
        "(CivilDocumentPressurePipesExtension) and the part-enumeration member couldn't be " +
        "confirmed from public research — two guessed namespaces failed to compile. Needs " +
        "confirmation against a live Civil 3D drawing (e.g. Visual Studio's Object Browser " +
        "pointed at AeccPressurePipesMgd.dll) before implementing for real.",
      inputShape: canonicalInputShape,
      supportedActions: ["list_networks", "get_network", "list_parts", "get_part", "create_network"],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
