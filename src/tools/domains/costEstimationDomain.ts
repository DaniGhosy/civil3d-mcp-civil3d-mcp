import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const PayItemSchema = z.object({ code: z.string(), description: z.string(), unit: z.string(), unitPrice: z.number().nonnegative() });

const canonicalInputShape = {
  action: z.enum(["pay_items_export", "material_cost_estimate"]).describe("The cost estimation operation to perform."),
  outputPath: z.string().optional().describe("Output CSV path (required for pay_items_export)."),
  overwrite: z.boolean().optional(),
  corridorName: z.string().optional(),
  baseSurface: z.string().optional().describe("Existing-ground surface for earthwork volumes."),
  designSurface: z.string().optional().describe("Design surface for earthwork volumes."),
  alignmentName: z.string().optional(),
  payItems: z.array(PayItemSchema).optional().describe("Pay item catalog with unit prices (required for material_cost_estimate)."),
  includeEarthwork: z.boolean().optional(),
  includeCorridorMaterials: z.boolean().optional(),
  includePipeLengths: z.boolean().optional(),
  includeStructureCounts: z.boolean().optional(),
  contingencyPercent: z.number().optional(),
  mobilizationPercent: z.number().optional(),
};

export const COST_ESTIMATION_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "cost_estimation",
  actions: {
    pay_items_export: {
      action: "pay_items_export",
      inputSchema: z.object({
        action: z.literal("pay_items_export"),
        outputPath: z.string(),
        overwrite: z.boolean().optional(),
        corridorName: z.string().optional(),
        baseSurface: z.string().optional(),
        designSurface: z.string().optional(),
        alignmentName: z.string().optional(),
        payItems: z.array(PayItemSchema).optional(),
        includeEarthwork: z.boolean().optional(),
        includeCorridorMaterials: z.boolean().optional(),
        includePipeLengths: z.boolean().optional(),
        includeStructureCounts: z.boolean().optional(),
      }),
      capabilities: ["export", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["exportPayItems"],
      execute: async (args: any) => await withApplicationConnection(async (c) =>
        await c.sendCommand("exportPayItems", {
          outputPath: args.outputPath,
          overwrite: args.overwrite ?? false,
          corridorName: args.corridorName ?? null,
          baseSurface: args.baseSurface ?? null,
          designSurface: args.designSurface ?? null,
          alignmentName: args.alignmentName ?? null,
          payItems: args.payItems ?? [],
          includeEarthwork: args.includeEarthwork ?? true,
          includeCorridorMaterials: args.includeCorridorMaterials ?? true,
          includePipeLengths: args.includePipeLengths ?? true,
          includeStructureCounts: args.includeStructureCounts ?? true,
        })),
    },
    material_cost_estimate: {
      action: "material_cost_estimate",
      inputSchema: z.object({
        action: z.literal("material_cost_estimate"),
        corridorName: z.string().optional(),
        baseSurface: z.string().optional(),
        designSurface: z.string().optional(),
        alignmentName: z.string().optional(),
        contingencyPercent: z.number().nonnegative().optional(),
        mobilizationPercent: z.number().nonnegative().optional(),
        payItems: z.array(PayItemSchema),
        outputPath: z.string().optional(),
        overwrite: z.boolean().optional(),
      }),
      capabilities: ["query", "analyze", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["calculateMaterialCostEstimate"],
      execute: async (args: any) => await withApplicationConnection(async (c) =>
        await c.sendCommand("calculateMaterialCostEstimate", {
          corridorName: args.corridorName ?? null,
          baseSurface: args.baseSurface ?? null,
          designSurface: args.designSurface ?? null,
          alignmentName: args.alignmentName ?? null,
          contingencyPercent: args.contingencyPercent ?? 0,
          mobilizationPercent: args.mobilizationPercent ?? 5,
          payItems: args.payItems,
          outputPath: args.outputPath ?? null,
          overwrite: args.overwrite ?? false,
        })),
    },
  },
  exposures: [
    {
      toolName: "civil3d_cost_estimation",
      displayName: "Civil 3D Cost Estimation",
      description: "Exports pay items and calculates material cost estimates from Civil 3D quantities (earthwork, pipe lengths, structure counts) through a single domain tool. Corridor material quantities are not available from the managed API.",
      inputShape: canonicalInputShape,
      supportedActions: ["pay_items_export", "material_cost_estimate"],
      resolveAction: (rawArgs) => ({ action: String(rawArgs.action ?? ""), args: rawArgs }),
    },
  ],
};
