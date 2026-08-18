import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const CoordinateSystemActionSchema = z.enum(["info", "transform"]);

const canonicalInputShape = {
  action: CoordinateSystemActionSchema.describe("The coordinate system operation to perform."),
  fromSystem: z.enum(["drawing", "geographic"]).optional().describe("Source coordinate system for transform."),
  toSystem: z.enum(["drawing", "geographic"]).optional().describe("Target coordinate system for transform."),
  x: z.number().optional().describe("X coordinate to transform."),
  y: z.number().optional().describe("Y coordinate to transform."),
  z: z.number().optional().describe("Z coordinate to transform."),
};

export const COORDINATE_SYSTEM_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "coordinate_system",
  actions: {
    info: {
      action: "info",
      inputSchema: z.object({ action: z.literal("info") }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      pluginMethods: ["getCoordinateSystemInfo"],
      execute: async () =>
        await withApplicationConnection(async (c) => await c.sendCommand("getCoordinateSystemInfo", {})),
    },
    transform: {
      action: "transform",
      inputSchema: z.object({
        action: z.literal("transform"),
        fromSystem: z.enum(["drawing", "geographic"]),
        toSystem: z.enum(["drawing", "geographic"]),
        x: z.number(),
        y: z.number(),
        z: z.number().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      pluginMethods: ["transformCoordinates"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("transformCoordinates", {
            fromSystem: args.fromSystem,
            toSystem: args.toSystem,
            x: args.x,
            y: args.y,
            z: args.z,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_coordinate_system",
      displayName: "Civil 3D Coordinate System",
      description: "Provides coordinate system information and coordinate transformations.",
      inputShape: canonicalInputShape,
      supportedActions: ["info", "transform"],
      resolveAction: (rawArgs) => ({ action: String(rawArgs.action ?? ""), args: rawArgs }),
    },
  ],
};
