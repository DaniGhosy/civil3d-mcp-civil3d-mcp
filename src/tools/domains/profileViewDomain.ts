import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const ProfileViewActionSchema = z.enum(["create", "list", "get", "delete", "get_bands"]);

const canonicalInputShape = {
  action: ProfileViewActionSchema.describe("The profile view operation to perform."),
  alignmentName: z.string().optional().describe("Parent alignment name (for create)."),
  name: z.string().optional().describe("Profile view name."),
  x: z.number().optional().describe("Insertion point X (for create). Use civil3d_object resolve_location to compute this."),
  y: z.number().optional().describe("Insertion point Y (for create)."),
  z: z.number().optional().describe("Insertion point Z (for create, default 0)."),
  bandSetStyle: z.string().optional().describe("Profile view band set style name (default 'Standard')."),
  viewStyle: z.string().optional().describe("Profile view style name (default 'Standard')."),
};

export const PROFILE_VIEW_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "profile",
  actions: {
    create: {
      action: "create",
      inputSchema: z.object({
        action: z.literal("create"),
        alignmentName: z.string(),
        name: z.string(),
        x: z.number(),
        y: z.number(),
        z: z.number().optional(),
        bandSetStyle: z.string().optional(),
        viewStyle: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createProfileView"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createProfileView", {
            alignmentName: args.alignmentName,
            name: args.name,
            x: args.x,
            y: args.y,
            z: args.z,
            bandSetStyle: args.bandSetStyle,
            viewStyle: args.viewStyle,
          })
        ),
    },
    list: {
      action: "list",
      inputSchema: z.object({ action: z.literal("list") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listProfileViews"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listProfileViews", {})
        ),
    },
    get: {
      action: "get",
      inputSchema: z.object({ action: z.literal("get"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getProfileView"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getProfileView", { name: args.name })
        ),
    },
    delete: {
      action: "delete",
      inputSchema: z.object({ action: z.literal("delete"), name: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteProfileView"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteProfileView", { name: args.name })
        ),
    },
    get_bands: {
      action: "get_bands",
      inputSchema: z.object({ action: z.literal("get_bands"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getProfileViewBands"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getProfileViewBands", { name: args.name })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_profile_view",
      displayName: "Civil 3D Profile View",
      description:
        "Manage Civil 3D profile views (the graphic/annotated display of a profile, distinct " +
        "from the profile data itself). Actions: create (at an X,Y,Z insertion point — use " +
        "civil3d_object resolve_location first to compute it from coordinates, a mouse click, " +
        "or a reference object), list, get (by name), delete, get_bands (current top/bottom band items).",
      inputShape: canonicalInputShape,
      supportedActions: ["create", "list", "get", "delete", "get_bands"],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
