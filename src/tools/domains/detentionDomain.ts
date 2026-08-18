import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const DetentionActionSchema = z.enum(["basin_size_calculate", "stage_storage"]);

const canonicalInputShape = {
  action: DetentionActionSchema.describe("The detention calculation to perform."),
  inflow: z.number().optional().describe("Peak inflow rate (cfs)."),
  outflow: z.number().optional().describe("Allowable peak outflow rate (cfs)."),
  stormDuration: z.number().optional().describe("Storm duration in minutes."),
  method: z.enum(["modified_rational", "triangular_hydrograph", "scs_curve_number"]).optional().describe("Storage estimation method."),
  sideSlope: z.number().optional().describe("Basin side slope ratio (H:1)."),
  bottomWidth: z.number().optional().describe("Basin bottom width."),
  freeboardDepth: z.number().optional().describe("Freeboard depth above design depth."),
  surfaceName: z.string().optional().describe("Reference surface name."),
  bottomElevation: z.number().optional().describe("Basin bottom elevation (for stage_storage)."),
  topElevation: z.number().optional().describe("Basin top elevation (for stage_storage)."),
  elevationIncrement: z.number().optional().describe("Elevation increment for the stage-storage table."),
  outletType: z.enum(["orifice", "weir", "riser"]).optional().describe("Outlet control type."),
  outletDiameter: z.number().optional().describe("Outlet diameter (inches)."),
  weirLength: z.number().optional().describe("Weir length."),
  dischargeCoefficient: z.number().optional().describe("Discharge coefficient."),
};

export const DETENTION_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "detention",
  actions: {
    basin_size_calculate: {
      action: "basin_size_calculate",
      inputSchema: z.object({
        action: z.literal("basin_size_calculate"),
        inflow: z.number().positive(),
        outflow: z.number().positive(),
        stormDuration: z.number().positive().optional(),
        method: z.enum(["modified_rational", "triangular_hydrograph", "scs_curve_number"]).optional(),
        sideSlope: z.number().positive().optional(),
        bottomWidth: z.number().nonnegative().optional(),
        freeboardDepth: z.number().nonnegative().optional(),
        surfaceName: z.string().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["calculateDetentionBasinSize"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("calculateDetentionBasinSize", {
            inflow: args.inflow,
            outflow: args.outflow,
            stormDuration: args.stormDuration ?? 60,
            method: args.method ?? "modified_rational",
            sideSlope: args.sideSlope ?? 3.0,
            bottomWidth: args.bottomWidth ?? 10.0,
            freeboardDepth: args.freeboardDepth ?? 1.0,
            surfaceName: args.surfaceName ?? null,
          })
        ),
    },
    stage_storage: {
      action: "stage_storage",
      inputSchema: z.object({
        action: z.literal("stage_storage"),
        surfaceName: z.string(),
        bottomElevation: z.number(),
        topElevation: z.number(),
        elevationIncrement: z.number().positive().optional(),
        outletType: z.enum(["orifice", "weir", "riser"]).optional(),
        outletDiameter: z.number().positive().optional(),
        weirLength: z.number().positive().optional(),
        dischargeCoefficient: z.number().positive().optional(),
      }),
      capabilities: ["query", "analyze", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["calculateDetentionStageStorage"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("calculateDetentionStageStorage", {
            surfaceName: args.surfaceName,
            bottomElevation: args.bottomElevation,
            topElevation: args.topElevation,
            elevationIncrement: args.elevationIncrement ?? 0.5,
            outletType: args.outletType ?? "orifice",
            outletDiameter: args.outletDiameter ?? null,
            weirLength: args.weirLength ?? null,
            dischargeCoefficient: args.dischargeCoefficient ?? null,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_detention",
      displayName: "Civil 3D Detention",
      description: "Calculates detention basin sizing and stage-storage workflows. Note: stage_storage requires surveyed stage-area data since the managed API does not expose inundated plan area at arbitrary elevations.",
      inputShape: canonicalInputShape,
      supportedActions: ["basin_size_calculate", "stage_storage"],
      resolveAction: (rawArgs) => ({ action: String(rawArgs.action ?? ""), args: rawArgs }),
    },
  ],
};
