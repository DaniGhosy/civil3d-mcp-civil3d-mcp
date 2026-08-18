import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const SheetProductionActionSchema = z.enum([
  "list_view_frames",
  "list_match_lines",
  "list_sheet_sets",
  "get_sheet_set_info",
  "add_sheet",
  "get_sheet_properties",
  "set_sheet_title_block",
  "update_plan_profile_sheet_alignment",
  "create_sheet_view",
  "set_sheet_view_scale",
]);

const canonicalInputShape = {
  action: SheetProductionActionSchema.describe("The sheet production operation to perform."),
  name: z.string().optional().describe("Sheet set name (get_sheet_set_info)."),
  sheetSetName: z.string().optional().describe("Sheet set name (add_sheet/get_sheet_properties/set_sheet_title_block/update_plan_profile_sheet_alignment)."),
  sheetName: z.string().optional().describe("Sheet name."),
  sheetNumber: z.string().optional().describe("Sheet number, default '1' (add_sheet)."),
  layoutName: z.string().optional().describe("Layout to attach the new sheet to (add_sheet), or the paper-space layout to work in (create_sheet_view/set_sheet_view_scale)."),
  titleBlockPath: z.string().optional().describe("Title block .dwg/.dwt path (set_sheet_title_block)."),
  alignmentName: z.string().optional().describe("Alignment name (update_plan_profile_sheet_alignment)."),
  profileName: z.string().optional().describe("Profile name (update_plan_profile_sheet_alignment)."),
  viewName: z.string().optional().describe("Named view to apply to the new viewport (create_sheet_view)."),
  centerX: z.number().optional().describe("Viewport center X, paper space units (create_sheet_view, default 0)."),
  centerY: z.number().optional().describe("Viewport center Y, paper space units (create_sheet_view, default 0)."),
  width: z.number().optional().describe("Viewport width, paper space units (create_sheet_view, default 8)."),
  height: z.number().optional().describe("Viewport height, paper space units (create_sheet_view, default 6)."),
  scale: z.number().optional().describe("Viewport scale, e.g. 50 for 1\"=50' (create_sheet_view/set_sheet_view_scale)."),
  viewportHandle: z.string().optional().describe("Specific viewport handle; defaults to the first viewport in the layout (set_sheet_view_scale)."),
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
    list_sheet_sets: {
      action: "list_sheet_sets",
      inputSchema: z.object({ action: z.literal("list_sheet_sets") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSheetSets"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSheetSets", {})
        ),
    },
    get_sheet_set_info: {
      action: "get_sheet_set_info",
      inputSchema: z.object({ action: z.literal("get_sheet_set_info"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSheetSetInfo"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getSheetSetInfo", { name: args.name })
        ),
    },
    add_sheet: {
      action: "add_sheet",
      inputSchema: z.object({
        action: z.literal("add_sheet"),
        sheetSetName: z.string(),
        sheetName: z.string(),
        sheetNumber: z.string().optional(),
        layoutName: z.string().optional(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addSheet"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addSheet", {
            sheetSetName: args.sheetSetName,
            sheetName: args.sheetName,
            sheetNumber: args.sheetNumber,
            layoutName: args.layoutName,
          })
        ),
    },
    get_sheet_properties: {
      action: "get_sheet_properties",
      inputSchema: z.object({
        action: z.literal("get_sheet_properties"),
        sheetSetName: z.string(),
        sheetName: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSheetProperties"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getSheetProperties", { sheetSetName: args.sheetSetName, sheetName: args.sheetName })
        ),
    },
    set_sheet_title_block: {
      action: "set_sheet_title_block",
      inputSchema: z.object({
        action: z.literal("set_sheet_title_block"),
        sheetSetName: z.string(),
        sheetName: z.string(),
        titleBlockPath: z.string(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["setSheetTitleBlock"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("setSheetTitleBlock", {
            sheetSetName: args.sheetSetName,
            sheetName: args.sheetName,
            titleBlockPath: args.titleBlockPath,
          })
        ),
    },
    update_plan_profile_sheet_alignment: {
      action: "update_plan_profile_sheet_alignment",
      inputSchema: z.object({
        action: z.literal("update_plan_profile_sheet_alignment"),
        sheetSetName: z.string(),
        sheetName: z.string(),
        alignmentName: z.string(),
        profileName: z.string().optional(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["updatePlanProfileSheetAlignment"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("updatePlanProfileSheetAlignment", {
            sheetSetName: args.sheetSetName,
            sheetName: args.sheetName,
            alignmentName: args.alignmentName,
            profileName: args.profileName,
          })
        ),
    },
    create_sheet_view: {
      action: "create_sheet_view",
      inputSchema: z.object({
        action: z.literal("create_sheet_view"),
        layoutName: z.string(),
        viewName: z.string().optional(),
        centerX: z.number().optional(),
        centerY: z.number().optional(),
        width: z.number().optional(),
        height: z.number().optional(),
        scale: z.number().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createSheetView"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createSheetView", {
            layoutName: args.layoutName,
            viewName: args.viewName,
            centerX: args.centerX ?? 0,
            centerY: args.centerY ?? 0,
            width: args.width ?? 8,
            height: args.height ?? 6,
            scale: args.scale,
          })
        ),
    },
    set_sheet_view_scale: {
      action: "set_sheet_view_scale",
      inputSchema: z.object({
        action: z.literal("set_sheet_view_scale"),
        layoutName: z.string(),
        viewportHandle: z.string().optional(),
        scale: z.number(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["setSheetViewScale"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("setSheetViewScale", {
            layoutName: args.layoutName,
            viewportHandle: args.viewportHandle,
            scale: args.scale,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_sheet_production",
      displayName: "Civil 3D Sheet Production",
      description:
        "Manage Civil 3D plan production. list_view_frames/list_match_lines read view frame " +
        "and match line objects already created via the Civil 3D UI — creating those is NOT " +
        "possible via the .NET API (confirmed Autodesk limitation, open feature request). " +
        "Separately, list_sheet_sets/get_sheet_set_info/add_sheet/get_sheet_properties/" +
        "set_sheet_title_block/update_plan_profile_sheet_alignment/create_sheet_view/" +
        "set_sheet_view_scale manage sheet SETs (a higher-level Sheet Set Manager abstraction) " +
        "and are real — sheet sets themselves must already exist (created via Sheet Set " +
        "Manager); this tool adds/edits sheets within one.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list_view_frames",
        "list_match_lines",
        "list_sheet_sets",
        "get_sheet_set_info",
        "add_sheet",
        "get_sheet_properties",
        "set_sheet_title_block",
        "update_plan_profile_sheet_alignment",
        "create_sheet_view",
        "set_sheet_view_scale",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
