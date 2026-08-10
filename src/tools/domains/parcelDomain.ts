import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const ParcelActionSchema = z.enum(["list_sites", "list", "get", "delete", "create"]);

const canonicalInputShape = {
  action: ParcelActionSchema.describe("The parcel operation to perform."),
  siteName: z.string().optional().describe("Parcel site name (filter for list, required for create)."),
  name: z.string().optional().describe("Parcel name (for get/delete)."),
  boundaryLayer: z.string().optional().describe("Layer containing closed boundary entities to convert into parcels (for create)."),
};

export const PARCEL_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "parcel",
  actions: {
    list_sites: {
      action: "list_sites",
      inputSchema: z.object({ action: z.literal("list_sites") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listParcelSites"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listParcelSites", {})
        ),
    },
    list: {
      action: "list",
      inputSchema: z.object({
        action: z.literal("list"),
        siteName: z.string().optional(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listParcels"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listParcels", { siteName: args.siteName })
        ),
    },
    get: {
      action: "get",
      inputSchema: z.object({ action: z.literal("get"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getParcel"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getParcel", { name: args.name })
        ),
    },
    delete: {
      action: "delete",
      inputSchema: z.object({ action: z.literal("delete"), name: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteParcel"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteParcel", { name: args.name })
        ),
    },
    create: {
      action: "create",
      inputSchema: z.object({
        action: z.literal("create"),
        siteName: z.string().optional(),
        boundaryLayer: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createParcel"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createParcel", {
            siteName: args.siteName,
            boundaryLayer: args.boundaryLayer,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_parcel",
      displayName: "Civil 3D Parcel",
      description:
        "Manage Civil 3D parcels. Actions: list_sites (parcel sites in the drawing), " +
        "list (parcels, optionally filtered by siteName), get (by name), delete (by name). " +
        "Note: create is not yet implemented — it returns a 'planned' status until the parcel " +
        "layout workflow's exact factory method is confirmed against a live Civil 3D drawing.",
      inputShape: canonicalInputShape,
      supportedActions: ["list_sites", "list", "get", "delete", "create"],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
