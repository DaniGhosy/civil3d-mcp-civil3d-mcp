import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const StyleObjectTypeSchema = z.enum([
  "surface",
  "alignment",
  "profile",
  "corridor",
  "pipe",
  "structure",
  "point",
  "section",
  "assembly",
]);

const StyleActionSchema = z.enum(["list_styles", "get_style"]);

const canonicalInputShape = {
  action: StyleActionSchema.describe("The style operation to perform."),
  objectType: StyleObjectTypeSchema.describe("Civil 3D object type owning the style collection."),
  styleName: z.string().optional().describe("Style name (get_style)."),
};

export const STYLE_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "style",
  actions: {
    list_styles: {
      action: "list_styles",
      inputSchema: z.object({ action: z.literal("list_styles"), objectType: StyleObjectTypeSchema }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listStyles"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listStyles", { objectType: args.objectType })
        ),
    },
    get_style: {
      action: "get_style",
      inputSchema: z.object({
        action: z.literal("get_style"),
        objectType: StyleObjectTypeSchema,
        styleName: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getStyle"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getStyle", { objectType: args.objectType, styleName: args.styleName })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_style",
      displayName: "Civil 3D Style",
      description:
        "Lists and inspects Civil 3D object styles (not label styles — use civil3d_label for " +
        "those). Actions: list_styles (name/handle/isDefault for every style of an objectType), " +
        "get_style (full detail for one named style, including every readable scalar property " +
        "found via reflection). Supported objectType values: surface, alignment, profile, " +
        "corridor, pipe, structure, point, section, assembly.",
      inputShape: canonicalInputShape,
      supportedActions: ["list_styles", "get_style"],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
