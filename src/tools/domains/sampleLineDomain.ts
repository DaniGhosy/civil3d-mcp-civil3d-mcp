import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const SampleLineActionSchema = z.enum([
  "create_group",
  "list_groups",
  "create_line",
  "list_lines",
  "delete_group",
  "create_section_view_group",
  "list_section_views",
  "delete_section_view",
  "list_mass_haul_lines",
  "create_mass_haul_line",
  "report_quantities",
  "list_material_lists",
  "get_section_data",
  "create_section_views",
  "update_section_view_styles",
  "export_section_data",
]);

const canonicalInputShape = {
  action: SampleLineActionSchema.describe("The sample line / section / mass haul / QTO operation to perform."),
  alignmentName: z.string().optional().describe("Parent alignment name (create_group/get_section_data/create_section_views/update_section_view_styles/export_section_data)."),
  groupName: z.string().optional().describe("Sample line group name."),
  name: z.string().optional().describe("Name for a new sample line, or a section view name (delete_section_view)."),
  station: z.number().optional().describe("Station for a new sample line (create_line, single-station mode) or section lookup (get_section_data)."),
  points: z
    .array(z.object({ x: z.number(), y: z.number() }))
    .optional()
    .describe("2D points defining a custom sample line (create_line, points mode)."),
  x: z.number().optional().describe("Insertion X for a new section view group (create_section_view_group)."),
  y: z.number().optional().describe("Insertion Y for a new section view group."),
  z: z.number().optional().describe("Insertion Z for a new section view group (default 0)."),
  materialListName: z.string().optional().describe("Material list name (report_quantities)."),
  reportFileName: z.string().optional().describe("Output report file path (report_quantities)."),
  styleSheetFileName: z.string().optional().describe("Optional XSL stylesheet file path (report_quantities)."),
  sampleLineGroupName: z.string().optional().describe("Sample line group name (get_section_data/create_section_views/update_section_view_styles/export_section_data)."),
  insertionX: z.number().optional().describe("Insertion X (create_section_views)."),
  insertionY: z.number().optional().describe("Insertion Y (create_section_views)."),
  style: z.string().optional().describe("Section view style name (create_section_views/update_section_view_styles)."),
  bandSetStyle: z.string().optional().describe("Section view band-set style name (create_section_views/update_section_view_styles)."),
  leftOffset: z.number().optional().describe("Left offset range, must pair with rightOffset (create_section_views)."),
  rightOffset: z.number().optional().describe("Right offset range, must pair with leftOffset and be greater (create_section_views)."),
  stationStart: z.number().optional().describe("Start station (create_section_views/export_section_data)."),
  stationEnd: z.number().optional().describe("End station (create_section_views/export_section_data)."),
  applyToAll: z.boolean().optional().describe("Apply the style to every section view in the group, not just the first (update_section_view_styles, default true)."),
  outputPath: z.string().optional().describe("CSV output path (export_section_data)."),
  overwrite: z.boolean().optional().describe("Overwrite an existing output file (export_section_data)."),
  includeElevations: z.boolean().optional().describe("Include an Elevation column (export_section_data, default true)."),
  includeMaterials: z.boolean().optional().describe("Not supported — must be false or omitted (export_section_data)."),
};

export const SAMPLE_LINE_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "section",
  actions: {
    create_group: {
      action: "create_group",
      inputSchema: z.object({
        action: z.literal("create_group"),
        alignmentName: z.string(),
        name: z.string(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createSampleLineGroup"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createSampleLineGroup", {
            alignmentName: args.alignmentName,
            name: args.name,
          })
        ),
    },
    list_groups: {
      action: "list_groups",
      inputSchema: z.object({ action: z.literal("list_groups") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSampleLineGroups"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSampleLineGroups", {})
        ),
    },
    create_line: {
      action: "create_line",
      inputSchema: z.object({
        action: z.literal("create_line"),
        groupName: z.string(),
        name: z.string(),
        station: z.number().optional(),
        points: z.array(z.object({ x: z.number(), y: z.number() })).optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createSampleLine"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createSampleLine", {
            groupName: args.groupName,
            name: args.name,
            station: args.station,
            points: args.points,
          })
        ),
    },
    list_lines: {
      action: "list_lines",
      inputSchema: z.object({ action: z.literal("list_lines"), groupName: z.string() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSampleLines"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSampleLines", { groupName: args.groupName })
        ),
    },
    delete_group: {
      action: "delete_group",
      inputSchema: z.object({ action: z.literal("delete_group"), groupName: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteSampleLineGroup"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteSampleLineGroup", { groupName: args.groupName })
        ),
    },
    create_section_view_group: {
      action: "create_section_view_group",
      inputSchema: z.object({
        action: z.literal("create_section_view_group"),
        groupName: z.string(),
        x: z.number(),
        y: z.number(),
        z: z.number().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createSectionViewGroup"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createSectionViewGroup", {
            groupName: args.groupName,
            x: args.x,
            y: args.y,
            z: args.z,
          })
        ),
    },
    list_section_views: {
      action: "list_section_views",
      inputSchema: z.object({ action: z.literal("list_section_views") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSectionViews"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSectionViews", {})
        ),
    },
    delete_section_view: {
      action: "delete_section_view",
      inputSchema: z.object({ action: z.literal("delete_section_view"), name: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteSectionView"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteSectionView", { name: args.name })
        ),
    },
    list_mass_haul_lines: {
      action: "list_mass_haul_lines",
      inputSchema: z.object({ action: z.literal("list_mass_haul_lines") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listMassHaulLines"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listMassHaulLines", {})
        ),
    },
    create_mass_haul_line: {
      action: "create_mass_haul_line",
      inputSchema: z.object({ action: z.literal("create_mass_haul_line") }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createMassHaulLine"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createMassHaulLine", {})
        ),
    },
    report_quantities: {
      action: "report_quantities",
      inputSchema: z.object({
        action: z.literal("report_quantities"),
        groupName: z.string(),
        materialListName: z.string(),
        reportFileName: z.string(),
        styleSheetFileName: z.string().optional(),
      }),
      capabilities: ["query", "export"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["reportQuantities"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("reportQuantities", {
            groupName: args.groupName,
            materialListName: args.materialListName,
            reportFileName: args.reportFileName,
            styleSheetFileName: args.styleSheetFileName,
          })
        ),
    },
    list_material_lists: {
      action: "list_material_lists",
      inputSchema: z.object({ action: z.literal("list_material_lists") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listMaterialLists"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listMaterialLists", {})
        ),
    },
    get_section_data: {
      action: "get_section_data",
      inputSchema: z.object({
        action: z.literal("get_section_data"),
        alignmentName: z.string(),
        sampleLineGroupName: z.string(),
        station: z.number(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSectionData"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getSectionData", {
            alignmentName: args.alignmentName,
            sampleLineGroupName: args.sampleLineGroupName,
            station: args.station,
          })
        ),
    },
    create_section_views: {
      action: "create_section_views",
      inputSchema: z.object({
        action: z.literal("create_section_views"),
        alignmentName: z.string(),
        sampleLineGroupName: z.string(),
        insertionX: z.number(),
        insertionY: z.number(),
        style: z.string().optional(),
        bandSetStyle: z.string().optional(),
        leftOffset: z.number().optional(),
        rightOffset: z.number().optional(),
        stationStart: z.number().optional(),
        stationEnd: z.number().optional(),
      }),
      capabilities: ["create", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createSectionViews"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createSectionViews", {
            alignmentName: args.alignmentName,
            sampleLineGroupName: args.sampleLineGroupName,
            insertionX: args.insertionX,
            insertionY: args.insertionY,
            style: args.style,
            bandSetStyle: args.bandSetStyle,
            leftOffset: args.leftOffset,
            rightOffset: args.rightOffset,
            stationStart: args.stationStart,
            stationEnd: args.stationEnd,
          })
        ),
    },
    update_section_view_styles: {
      action: "update_section_view_styles",
      inputSchema: z.object({
        action: z.literal("update_section_view_styles"),
        alignmentName: z.string(),
        sampleLineGroupName: z.string(),
        style: z.string().optional(),
        bandSetStyle: z.string().optional(),
        applyToAll: z.boolean().optional(),
      }),
      capabilities: ["edit", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["updateSectionViewStyles"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("updateSectionViewStyles", {
            alignmentName: args.alignmentName,
            sampleLineGroupName: args.sampleLineGroupName,
            style: args.style,
            bandSetStyle: args.bandSetStyle,
            applyToAll: args.applyToAll ?? true,
          })
        ),
    },
    export_section_data: {
      action: "export_section_data",
      inputSchema: z.object({
        action: z.literal("export_section_data"),
        alignmentName: z.string(),
        sampleLineGroupName: z.string(),
        outputPath: z.string(),
        includeElevations: z.boolean().optional(),
        includeMaterials: z.boolean().optional(),
        stationStart: z.number().optional(),
        stationEnd: z.number().optional(),
        overwrite: z.boolean().optional(),
      }),
      capabilities: ["export"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["exportSectionData"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("exportSectionData", {
            alignmentName: args.alignmentName,
            sampleLineGroupName: args.sampleLineGroupName,
            outputPath: args.outputPath,
            includeElevations: args.includeElevations ?? true,
            includeMaterials: args.includeMaterials ?? false,
            stationStart: args.stationStart,
            stationEnd: args.stationEnd,
            overwrite: args.overwrite ?? false,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_sample_line",
      displayName: "Civil 3D Sample Lines, Sections, Mass Haul & QTO",
      description:
        "Manage Civil 3D sample lines, section views, mass haul lines, and quantity takeoff " +
        "reports. Actions: create_group (sample line group on an alignment), list_groups, " +
        "create_line (a sample line, either at a single station or through custom 2D points), " +
        "list_lines, delete_group, create_section_view_group (generates section views for a " +
        "sample line group at an insertion point), list_section_views, delete_section_view, " +
        "list_mass_haul_lines, report_quantities (writes a QTO report file for a material list " +
        "over a sample line group), get_section_data, create_section_views (offset-range " +
        "variant of create_section_view_group with explicit style/band-set application), " +
        "update_section_view_styles, export_section_data (CSV of station/source/offset[/" +
        "elevation] — includeMaterials is not supported and must stay false). Note: " +
        "create_mass_haul_line and list_material_lists are not yet implemented — no confirmed " +
        "API member was found for creating mass haul diagrams or enumerating available material " +
        "lists, so they return a 'planned' status.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "create_group",
        "list_groups",
        "create_line",
        "list_lines",
        "delete_group",
        "create_section_view_group",
        "list_section_views",
        "delete_section_view",
        "list_mass_haul_lines",
        "create_mass_haul_line",
        "report_quantities",
        "list_material_lists",
        "get_section_data",
        "create_section_views",
        "update_section_view_styles",
        "export_section_data",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
