import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const BlocksActionSchema = z.enum([
  "list_block_definitions",
  "count_blocks_by_name",
  "get_block_attributes",
  "list_blocks_by_layer",
  "get_block_insertion_points",
  "list_dynamic_block_states",
]);

const canonicalInputShape = {
  action: BlocksActionSchema.describe("The block-reading operation to perform."),
  name: z.string().optional().describe("Block definition name (e.g. \"TOMACORRIENTE\")."),
  layer: z.string().optional().describe("Layer name to filter block insertions by."),
  layout: z.string().optional().describe("Layout name to restrict the count to (count_blocks_by_name only)."),
};

export const BLOCKS_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "blocks",
  actions: {
    list_block_definitions: {
      action: "list_block_definitions",
      inputSchema: z.object({ action: z.literal("list_block_definitions") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listBlockDefinitions"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listBlockDefinitions", {})
        ),
    },
    count_blocks_by_name: {
      action: "count_blocks_by_name",
      inputSchema: z.object({
        action: z.literal("count_blocks_by_name"),
        name: z.string(),
        layout: z.string().optional(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["countBlocksByName"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("countBlocksByName", { name: args.name, layout: args.layout })
        ),
    },
    get_block_attributes: {
      action: "get_block_attributes",
      inputSchema: z.object({ action: z.literal("get_block_attributes"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getBlockAttributes"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getBlockAttributes", { name: args.name })
        ),
    },
    list_blocks_by_layer: {
      action: "list_blocks_by_layer",
      inputSchema: z.object({ action: z.literal("list_blocks_by_layer"), layer: z.string() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listBlocksByLayer"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listBlocksByLayer", { layer: args.layer })
        ),
    },
    get_block_insertion_points: {
      action: "get_block_insertion_points",
      inputSchema: z.object({ action: z.literal("get_block_insertion_points"), name: z.string() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getBlockInsertionPoints"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getBlockInsertionPoints", { name: args.name })
        ),
    },
    list_dynamic_block_states: {
      action: "list_dynamic_block_states",
      inputSchema: z.object({ action: z.literal("list_dynamic_block_states"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listDynamicBlockStates"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listDynamicBlockStates", { name: args.name })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_blocks",
      displayName: "Civil 3D Blocks",
      description:
        "Read block (symbol) data straight from the drawing database — for counting inserted " +
        "symbols like outlets, columns, or pipes drawn as library blocks. 100% exact, no geometry " +
        "interpretation. Actions: list_block_definitions (every block name + total insertion count), " +
        "count_blocks_by_name (exact count for one block, optionally scoped to a layout), " +
        "get_block_attributes (per-instance attribute tag/value pairs, e.g. power rating or code), " +
        "list_blocks_by_layer (insertion counts grouped by name for a given layer), " +
        "get_block_insertion_points (X,Y,Z + rotation per insertion, for room/zone counting), " +
        "list_dynamic_block_states (current value of every dynamic property per insertion, e.g. " +
        "visibility state — only returns something for blocks that are actually dynamic).",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list_block_definitions",
        "count_blocks_by_name",
        "get_block_attributes",
        "list_blocks_by_layer",
        "get_block_insertion_points",
        "list_dynamic_block_states",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
