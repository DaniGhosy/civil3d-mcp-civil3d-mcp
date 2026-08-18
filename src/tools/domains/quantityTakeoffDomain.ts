import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const RegionSchema = z.array(z.object({ x: z.number(), y: z.number() }));

const QuantityTakeoffActionSchema = z.enum([
  "surface_volume",
  "pipe_network_lengths",
  "pressure_network_lengths",
  "parcel_areas",
  "alignment_lengths",
  "point_count_by_group",
  "export_to_csv",
  "earthwork_summary",
]);

const canonicalInputShape = {
  action: QuantityTakeoffActionSchema.describe("The quantity takeoff operation to perform."),
  name: z.string().optional().describe("Network name (pipe_network_lengths/pressure_network_lengths)."),
  startStation: z.number().optional().describe("Clip start station (alignment_lengths)."),
  endStation: z.number().optional().describe("Clip end station (alignment_lengths)."),
  baseSurface: z.string().optional().describe("Base/existing surface name (surface_volume, earthwork_summary)."),
  comparisonSurface: z.string().optional().describe("Comparison surface name (surface_volume)."),
  designSurface: z.string().optional().describe("Design/proposed surface name (earthwork_summary)."),
  corridorName: z.string().optional().describe("Corridor name, carried through into the response only (surface_volume)."),
  region: RegionSchema.optional().describe("Optional polygon (min 3 points) to clip the volume computation to (surface_volume) — not yet applied by the plugin, carried through for future use."),
  groupBySize: z.boolean().optional().describe("Break down lengths by pipe size (pipe_network_lengths/pressure_network_lengths)."),
  groupByMaterial: z.boolean().optional().describe("Break down lengths by pipe material (pipe_network_lengths/pressure_network_lengths)."),
  siteName: z.string().optional().describe("Restrict to one site (parcel_areas)."),
  parcelNames: z.array(z.string()).optional().describe("Restrict to specific parcel names (parcel_areas)."),
  names: z.array(z.string()).optional().describe("Restrict to specific alignment names (alignment_lengths)."),
  groupNames: z.array(z.string()).optional().describe("Restrict to specific point group names (point_count_by_group)."),
  outputPath: z.string().optional().describe("CSV output file path (export_to_csv)."),
  overwrite: z.boolean().optional().describe("Overwrite an existing file (export_to_csv)."),
  includeAlignments: z.boolean().optional().describe("Include an alignments section, default true (export_to_csv)."),
  includeSurfaces: z.boolean().optional().describe("Include a surfaces section, default false (export_to_csv)."),
  includePipeNetworks: z.boolean().optional().describe("Include a gravity pipe networks section, default false (export_to_csv)."),
  includeParcelAreas: z.boolean().optional().describe("Include a parcel areas section, default false (export_to_csv)."),
  includePointGroups: z.boolean().optional().describe("Include a point groups section, default false (export_to_csv)."),
  filterNames: z.array(z.string()).optional().describe("Restrict every included section to these object names (export_to_csv)."),
  alignmentName: z.string().optional().describe("Reserved for station-clipped earthwork — currently unsupported, see earthwork_summary description (earthwork_summary)."),
  stationInterval: z.number().optional().describe("Reserved for station-clipped earthwork — currently unsupported (earthwork_summary)."),
};

export const QUANTITY_TAKEOFF_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "quantity_takeoff",
  actions: {
    surface_volume: {
      action: "surface_volume",
      inputSchema: z
        .object({
          action: z.literal("surface_volume"),
          baseSurface: z.string(),
          comparisonSurface: z.string(),
          corridorName: z.string().optional(),
          region: RegionSchema.optional(),
        })
        .superRefine((v, ctx) => {
          if (v.region != null && v.region.length < 3) {
            ctx.addIssue({ code: z.ZodIssueCode.custom, message: "region polygon must contain at least 3 points", path: ["region"] });
          }
        }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["qtySurfaceVolume"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("qtySurfaceVolume", {
            baseSurface: args.baseSurface,
            comparisonSurface: args.comparisonSurface,
            corridorName: args.corridorName,
          })
        ),
    },
    pipe_network_lengths: {
      action: "pipe_network_lengths",
      inputSchema: z.object({
        action: z.literal("pipe_network_lengths"),
        name: z.string(),
        groupBySize: z.boolean().optional(),
        groupByMaterial: z.boolean().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["qtyPipeNetworkLengths"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("qtyPipeNetworkLengths", {
            name: args.name,
            groupBySize: args.groupBySize ?? false,
            groupByMaterial: args.groupByMaterial ?? false,
          })
        ),
    },
    pressure_network_lengths: {
      action: "pressure_network_lengths",
      inputSchema: z.object({
        action: z.literal("pressure_network_lengths"),
        name: z.string(),
        groupBySize: z.boolean().optional(),
        groupByMaterial: z.boolean().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["qtyPressureNetworkLengths"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("qtyPressureNetworkLengths", {
            name: args.name,
            groupBySize: args.groupBySize ?? false,
            groupByMaterial: args.groupByMaterial ?? false,
          })
        ),
    },
    parcel_areas: {
      action: "parcel_areas",
      inputSchema: z.object({
        action: z.literal("parcel_areas"),
        siteName: z.string().optional(),
        parcelNames: z.array(z.string()).optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["qtyParcelAreas"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("qtyParcelAreas", { siteName: args.siteName, parcelNames: args.parcelNames })
        ),
    },
    alignment_lengths: {
      action: "alignment_lengths",
      inputSchema: z.object({
        action: z.literal("alignment_lengths"),
        names: z.array(z.string()).optional(),
        startStation: z.number().optional(),
        endStation: z.number().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["qtyAlignmentLengths"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("qtyAlignmentLengths", {
            names: args.names,
            startStation: args.startStation,
            endStation: args.endStation,
          })
        ),
    },
    point_count_by_group: {
      action: "point_count_by_group",
      inputSchema: z.object({
        action: z.literal("point_count_by_group"),
        groupNames: z.array(z.string()).optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["qtyPointCountByGroup"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("qtyPointCountByGroup", { groupNames: args.groupNames })
        ),
    },
    export_to_csv: {
      action: "export_to_csv",
      inputSchema: z.object({
        action: z.literal("export_to_csv"),
        outputPath: z.string(),
        overwrite: z.boolean().optional(),
        includeAlignments: z.boolean().optional(),
        includeSurfaces: z.boolean().optional(),
        includePipeNetworks: z.boolean().optional(),
        includeParcelAreas: z.boolean().optional(),
        includePointGroups: z.boolean().optional(),
        filterNames: z.array(z.string()).optional(),
      }),
      capabilities: ["export", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["qtyExportToCsv"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("qtyExportToCsv", {
            outputPath: args.outputPath,
            overwrite: args.overwrite ?? false,
            includeAlignments: args.includeAlignments ?? true,
            includeSurfaces: args.includeSurfaces ?? false,
            includePipeNetworks: args.includePipeNetworks ?? false,
            includeParcelAreas: args.includeParcelAreas ?? false,
            includePointGroups: args.includePointGroups ?? false,
            filterNames: args.filterNames,
          })
        ),
    },
    earthwork_summary: {
      action: "earthwork_summary",
      inputSchema: z.object({
        action: z.literal("earthwork_summary"),
        baseSurface: z.string(),
        designSurface: z.string(),
        alignmentName: z.string().optional(),
        startStation: z.number().optional(),
        endStation: z.number().optional(),
        stationInterval: z.number().positive().optional(),
      }),
      capabilities: ["query", "analyze", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["qtyEarthworkSummary"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("qtyEarthworkSummary", {
            baseSurface: args.baseSurface,
            designSurface: args.designSurface,
            alignmentName: args.alignmentName,
            startStation: args.startStation,
            endStation: args.endStation,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_quantity_takeoff",
      displayName: "Civil 3D Quantity Takeoff",
      description:
        "Aggregates Civil 3D object data for cost estimation. Actions: surface_volume (exact " +
        "TinVolumeSurface cut/fill/net between two surfaces — 'region' is accepted but not yet " +
        "applied by the plugin), pipe_network_lengths / pressure_network_lengths (total length " +
        "plus an optional size/material breakdown), parcel_areas, alignment_lengths (optionally " +
        "station-clipped), point_count_by_group, export_to_csv (writes a multi-section CSV — " +
        "alignments/surfaces/pipe networks/parcel areas/point groups, each independently " +
        "toggleable and name-filterable), earthwork_summary (whole-surface cut/fill/net via " +
        "TinVolumeSurface; station-clipped earthwork is NOT supported — the managed API has no " +
        "station-volume path without sample lines/QTO, so alignmentName/startStation/endStation " +
        "raise an error instead of silently ignoring the clip). Corridor QTO material lists are " +
        "out of scope — they belong to SampleLineGroup objects, not Corridor baselines.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "surface_volume",
        "pipe_network_lengths",
        "pressure_network_lengths",
        "parcel_areas",
        "alignment_lengths",
        "point_count_by_group",
        "export_to_csv",
        "earthwork_summary",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
