import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const CorridorActionSchema = z.enum([
  "list",
  "get",
  "rebuild",
  "get_surfaces",
  "get_feature_lines",
  "compute_volumes",
  "list_baselines",
  "list_baseline_regions",
  "add_baseline_region",
  "get_targets",
  "create_surface_from_corridor_surface",
  "set_target_mappings",
  "delete_region",
]);

const TargetMappingSchema = z.object({
  parameterName: z.string(),
  targetType: z.enum(["surface", "alignment", "profile"]),
  targetName: z.string(),
});

const canonicalInputShape = {
  action: CorridorActionSchema.describe("The corridor operation to perform."),
  name: z.string().optional().describe("Corridor name."),
  baselineName: z.string().optional().describe("Baseline name."),
  regionName: z.string().optional().describe("Baseline region name."),
  assemblyName: z.string().optional().describe("Assembly name to apply to a new region (add_baseline_region)."),
  startStation: z.number().optional().describe("Region start station (add_baseline_region)."),
  endStation: z.number().optional().describe("Region end station (add_baseline_region)."),
  corridorSurfaceName: z.string().optional().describe("Corridor surface name (create_surface_from_corridor_surface)."),
  newSurfaceName: z.string().optional().describe("Name for the new independent surface (create_surface_from_corridor_surface)."),
  baselineIndex: z.number().int().min(0).optional().describe("Zero-based baseline index (set_target_mappings/delete_region)."),
  regionIndex: z.number().int().min(0).optional().describe("Zero-based region index within the baseline (set_target_mappings/delete_region)."),
  targets: z.array(TargetMappingSchema).optional().describe("Target mappings to apply to the region (set_target_mappings)."),
};

export const CORRIDOR_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "corridor",
  actions: {
    list: {
      action: "list",
      inputSchema: z.object({ action: z.literal("list") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listCorridors"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listCorridors", {})
        ),
    },
    get: {
      action: "get",
      inputSchema: z.object({ action: z.literal("get"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getCorridor"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getCorridor", { name: args.name })
        ),
    },
    rebuild: {
      action: "rebuild",
      inputSchema: z.object({ action: z.literal("rebuild"), name: z.string() }),
      capabilities: ["manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["rebuildCorridor"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("rebuildCorridor", { name: args.name })
        ),
    },
    get_surfaces: {
      action: "get_surfaces",
      inputSchema: z.object({ action: z.literal("get_surfaces"), name: z.string() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getCorridorSurfaces"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getCorridorSurfaces", { name: args.name })
        ),
    },
    get_feature_lines: {
      action: "get_feature_lines",
      inputSchema: z.object({
        action: z.literal("get_feature_lines"),
        name: z.string(),
        baselineName: z.string(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getCorridorFeatureLines"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getCorridorFeatureLines", { name: args.name, baselineName: args.baselineName })
        ),
    },
    compute_volumes: {
      action: "compute_volumes",
      inputSchema: z.object({ action: z.literal("compute_volumes"), name: z.string() }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["computeCorridorVolumes"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("computeCorridorVolumes", { name: args.name })
        ),
    },
    list_baselines: {
      action: "list_baselines",
      inputSchema: z.object({ action: z.literal("list_baselines"), name: z.string() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listBaselines"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listBaselines", { name: args.name })
        ),
    },
    list_baseline_regions: {
      action: "list_baseline_regions",
      inputSchema: z.object({
        action: z.literal("list_baseline_regions"),
        name: z.string(),
        baselineName: z.string(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listBaselineRegions"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listBaselineRegions", { name: args.name, baselineName: args.baselineName })
        ),
    },
    add_baseline_region: {
      action: "add_baseline_region",
      inputSchema: z.object({
        action: z.literal("add_baseline_region"),
        name: z.string(),
        baselineName: z.string(),
        regionName: z.string(),
        assemblyName: z.string(),
        startStation: z.number(),
        endStation: z.number(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addBaselineRegion"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addBaselineRegion", {
            name: args.name,
            baselineName: args.baselineName,
            regionName: args.regionName,
            assemblyName: args.assemblyName,
            startStation: args.startStation,
            endStation: args.endStation,
          })
        ),
    },
    get_targets: {
      action: "get_targets",
      inputSchema: z.object({
        action: z.literal("get_targets"),
        name: z.string(),
        baselineName: z.string(),
        regionName: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getCorridorTargets"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getCorridorTargets", {
            name: args.name,
            baselineName: args.baselineName,
            regionName: args.regionName,
          })
        ),
    },
    create_surface_from_corridor_surface: {
      action: "create_surface_from_corridor_surface",
      inputSchema: z.object({
        action: z.literal("create_surface_from_corridor_surface"),
        name: z.string(),
        corridorSurfaceName: z.string(),
        newSurfaceName: z.string(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createSurfaceFromCorridorSurface"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createSurfaceFromCorridorSurface", {
            name: args.name,
            corridorSurfaceName: args.corridorSurfaceName,
            newSurfaceName: args.newSurfaceName,
          })
        ),
    },
    set_target_mappings: {
      action: "set_target_mappings",
      inputSchema: z.object({
        action: z.literal("set_target_mappings"),
        name: z.string(),
        baselineIndex: z.number().int().min(0).optional(),
        regionIndex: z.number().int().min(0).optional(),
        targets: z.array(TargetMappingSchema),
      }),
      capabilities: ["edit", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["setCorridorTargetMappings"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("setCorridorTargetMappings", {
            corridorName: args.name,
            baselineIndex: args.baselineIndex ?? 0,
            regionIndex: args.regionIndex ?? 0,
            targets: args.targets,
          })
        ),
    },
    delete_region: {
      action: "delete_region",
      inputSchema: z.object({
        action: z.literal("delete_region"),
        name: z.string(),
        baselineIndex: z.number().int().min(0).optional(),
        regionIndex: z.number().int().min(0),
      }),
      capabilities: ["edit", "delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteCorridorRegion"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteCorridorRegion", {
            corridorName: args.name,
            baselineIndex: args.baselineIndex ?? 0,
            regionIndex: args.regionIndex,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_corridor",
      displayName: "Civil 3D Corridor",
      description:
        "Manage Civil 3D corridors (3D road models). Actions: list, get (by name), rebuild, " +
        "get_surfaces (corridor surfaces by name), create_surface_from_corridor_surface " +
        "(detach an independent, dynamically-linked civil3d_surface from a corridor surface), " +
        "get_feature_lines (by baseline, via BaselineFeatureLines), list_baselines, " +
        "list_baseline_regions, add_baseline_region, get_targets (surface/alignment targets " +
        "of a baseline region, by name), set_target_mappings (write target mappings, by " +
        "baseline/region index), delete_region (by baseline/region index). Note: " +
        "compute_volumes has no direct API — combine get_surfaces + " +
        "create_surface_from_corridor_surface with civil3d_surface's " +
        "compute_volume/get_area_elevation_table instead. There is no .NET API for creating " +
        "intersections/roundabouts (confirmed COM/UI-only) — not covered by this tool.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list",
        "get",
        "rebuild",
        "get_surfaces",
        "get_feature_lines",
        "compute_volumes",
        "list_baselines",
        "list_baseline_regions",
        "add_baseline_region",
        "get_targets",
        "create_surface_from_corridor_surface",
        "set_target_mappings",
        "delete_region",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
