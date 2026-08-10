import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const SheetProductionActionSchema = z.enum(["list_view_frames", "list_match_lines"]);

const canonicalInputShape = {
  action: SheetProductionActionSchema.describe("The sheet production operation to perform."),
};

export const SHEET_PRODUCTION_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "workflow",
  actions: {
    list_view_frames: {
      action: "list_view_frames",
      inputSchema: z.object({ action: z.literal("list_view_frames") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listViewFrames"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listViewFrames", {})
        ),
    },
    list_match_lines: {
      action: "list_match_lines",
      inputSchema: z.object({ action: z.literal("list_match_lines") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listMatchLines"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listMatchLines", {})
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_sheet_production",
      displayName: "Civil 3D Sheet Production (view frames / match lines)",
      description:
        "Read-only access to plan production objects (view frames, match lines) already " +
        "created in the drawing via the Civil 3D UI. Actions: list_view_frames, " +
        "list_match_lines. Note: creating view frames, sheets, or match lines is NOT possible " +
        "via the .NET API — this is a confirmed Autodesk limitation (an open, unresolved " +
        "feature request asks for exactly this), not something to work around here. Use the " +
        "Civil 3D UI (Create View Frames / Create Sheets wizards) to produce these, then use " +
        "this tool to inspect what was created.",
      inputShape: canonicalInputShape,
      supportedActions: ["list_view_frames", "list_match_lines"],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
