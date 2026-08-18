import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const IntersectionActionSchema = z.enum(["list", "get"]);

const canonicalInputShape = {
  action: IntersectionActionSchema.describe("The intersection operation to perform."),
  siteName: z.string().optional().describe("Not supported as a filter — Civil 3D intersections do not expose site membership through the managed API."),
  name: z.string().optional().describe("Intersection name (for get)."),
  includeCorridorInfo: z.boolean().optional().describe("Include associated corridor info."),
  includeCurbReturns: z.boolean().optional().describe("Include curb return region details."),
};

export const INTERSECTION_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "intersection",
  actions: {
    list: {
      action: "list",
      inputSchema: z.object({ action: z.literal("list"), siteName: z.string().optional() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listIntersections"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listIntersections", { siteName: args.siteName ?? null })
        ),
    },
    get: {
      action: "get",
      inputSchema: z.object({
        action: z.literal("get"),
        name: z.string(),
        includeCorridorInfo: z.boolean().optional(),
        includeCurbReturns: z.boolean().optional(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getIntersection"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getIntersection", {
            name: args.name,
            includeCorridorInfo: args.includeCorridorInfo ?? false,
            includeCurbReturns: args.includeCurbReturns ?? false,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_intersection",
      displayName: "Civil 3D Intersection",
      description: "Lists and inspects Civil 3D intersections. Creation remains available only through Civil 3D's native Create Intersection command, which is not exposed by the managed API.",
      inputShape: canonicalInputShape,
      supportedActions: ["list", "get"],
      resolveAction: (rawArgs) => ({ action: String(rawArgs.action ?? ""), args: rawArgs }),
    },
  ],
};
