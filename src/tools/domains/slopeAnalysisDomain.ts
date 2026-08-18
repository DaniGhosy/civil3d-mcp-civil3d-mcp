import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const SlopeAnalysisActionSchema = z.enum(["geometry_calculate"]);

const canonicalInputShape = {
  action: SlopeAnalysisActionSchema.describe("The slope analysis operation to perform."),
  alignmentName: z.string().optional().describe("Alignment name (defaults to the first alignment in the drawing)."),
  profileName: z.string().optional().describe("Profile name (defaults to the finished-grade profile)."),
  surfaceName: z.string().optional().describe("Existing ground surface name (defaults to the first surface in the drawing)."),
  cutSlopeRatio: z.number().optional().describe("Cut slope ratio (H:1)."),
  fillSlopeRatio: z.number().optional().describe("Fill slope ratio (H:1)."),
  benchWidth: z.number().optional().describe("Bench width, if benching is used."),
  benchHeightInterval: z.number().optional().describe("Vertical interval between benches."),
  stationStart: z.number().optional().describe("Start station."),
  stationEnd: z.number().optional().describe("End station."),
  stationInterval: z.number().optional().describe("Station sampling interval."),
  roadwayWidth: z.number().optional().describe("Roadway half-width offset before the catch point."),
};

export const SLOPE_ANALYSIS_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "slope_analysis",
  actions: {
    geometry_calculate: {
      action: "geometry_calculate",
      inputSchema: z.object({
        action: z.literal("geometry_calculate"),
        alignmentName: z.string().optional(),
        profileName: z.string().optional(),
        surfaceName: z.string().optional(),
        cutSlopeRatio: z.number().positive().optional(),
        fillSlopeRatio: z.number().positive().optional(),
        benchWidth: z.number().nonnegative().optional(),
        benchHeightInterval: z.number().positive().optional(),
        stationStart: z.number().optional(),
        stationEnd: z.number().optional(),
        stationInterval: z.number().positive().optional(),
        roadwayWidth: z.number().nonnegative().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["calculateSlopeGeometry"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("calculateSlopeGeometry", {
            alignmentName: args.alignmentName ?? null,
            profileName: args.profileName ?? null,
            surfaceName: args.surfaceName ?? null,
            cutSlopeRatio: args.cutSlopeRatio ?? 2.0,
            fillSlopeRatio: args.fillSlopeRatio ?? 3.0,
            benchWidth: args.benchWidth ?? 0,
            benchHeightInterval: args.benchHeightInterval ?? 20,
            stationStart: args.stationStart ?? null,
            stationEnd: args.stationEnd ?? null,
            stationInterval: args.stationInterval ?? 10,
            roadwayWidth: args.roadwayWidth ?? null,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_slope_analysis",
      displayName: "Civil 3D Slope Analysis",
      description: "Calculates daylight/slope geometry (cut-fill, catch points, benching) along an alignment. Geotechnical stability checks require an engineer-approved external analysis model and are not exposed as a Civil 3D managed-API operation.",
      inputShape: canonicalInputShape,
      supportedActions: ["geometry_calculate"],
      resolveAction: (rawArgs) => ({ action: String(rawArgs.action ?? ""), args: rawArgs }),
    },
  ],
};
