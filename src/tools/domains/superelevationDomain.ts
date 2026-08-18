import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const canonicalInputShape = {
  action: z.enum(["get", "set", "design_check", "report"]).describe("The superelevation operation to perform."),
  alignmentName: z.string().optional(),
  includeRawData: z.boolean().optional(),
  designSpeed: z.number().optional(),
  normalCrownSlope: z.number().optional(),
  attainmentMethod: z.enum(["AASHTO_2001", "AASHTO_2011", "manual"]).optional(),
  pivotPoint: z.enum(["centerline", "inside_edge", "outside_edge"]).optional(),
  maxSuperelevation: z.number().optional(),
  checkAttainmentLength: z.boolean().optional(),
  checkRunoffLength: z.boolean().optional(),
  outputPath: z.string().optional(),
  overwrite: z.boolean().optional(),
  includeRunoffTable: z.boolean().optional(),
  includeViolations: z.boolean().optional(),
};

export const SUPERELEVATION_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "superelevation",
  actions: {
    get: {
      action: "get",
      inputSchema: z.object({ action: z.literal("get"), alignmentName: z.string(), includeRawData: z.boolean().optional() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSuperelevation"],
      execute: async (args: any) => await withApplicationConnection(async (c) =>
        await c.sendCommand("getSuperelevation", { alignmentName: args.alignmentName, includeRawData: args.includeRawData ?? false })),
    },
    set: {
      action: "set",
      inputSchema: z.object({
        action: z.literal("set"),
        alignmentName: z.string(),
        designSpeed: z.number().positive(),
        normalCrownSlope: z.number(),
        attainmentMethod: z.enum(["AASHTO_2001", "AASHTO_2011", "manual"]).optional(),
        pivotPoint: z.enum(["centerline", "inside_edge", "outside_edge"]).optional(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["setSuperelevation"],
      execute: async (args: any) => await withApplicationConnection(async (c) =>
        await c.sendCommand("setSuperelevation", {
          alignmentName: args.alignmentName,
          designSpeed: args.designSpeed,
          normalCrownSlope: args.normalCrownSlope,
          attainmentMethod: args.attainmentMethod ?? "AASHTO_2011",
          pivotPoint: args.pivotPoint ?? "centerline",
        })),
    },
    design_check: {
      action: "design_check",
      inputSchema: z.object({
        action: z.literal("design_check"),
        alignmentName: z.string(),
        designSpeed: z.number().positive(),
        maxSuperelevation: z.number().positive(),
        checkAttainmentLength: z.boolean().optional(),
        checkRunoffLength: z.boolean().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["checkSuperelevationDesign"],
      execute: async (args: any) => await withApplicationConnection(async (c) =>
        await c.sendCommand("checkSuperelevationDesign", {
          alignmentName: args.alignmentName,
          designSpeed: args.designSpeed,
          maxSuperelevation: args.maxSuperelevation,
          checkAttainmentLength: args.checkAttainmentLength ?? false,
          checkRunoffLength: args.checkRunoffLength ?? false,
        })),
    },
    report: {
      action: "report",
      inputSchema: z.object({
        action: z.literal("report"),
        alignmentName: z.string(),
        outputPath: z.string().optional(),
        overwrite: z.boolean().optional(),
        includeRunoffTable: z.boolean().optional(),
        includeViolations: z.boolean().optional(),
      }),
      capabilities: ["query", "generate", "export"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["generateSuperelevationReport"],
      execute: async (args: any) => await withApplicationConnection(async (c) =>
        await c.sendCommand("generateSuperelevationReport", {
          alignmentName: args.alignmentName,
          outputPath: args.outputPath ?? null,
          overwrite: args.overwrite ?? false,
          includeRunoffTable: args.includeRunoffTable ?? true,
          includeViolations: args.includeViolations ?? false,
        })),
    },
  },
  exposures: [
    {
      toolName: "civil3d_superelevation",
      displayName: "Civil 3D Superelevation",
      description: "Gets, sets, checks, and reports alignment superelevation. Note: 'set' always returns a capability error — Civil 3D 2026 does not expose wizard calculation settings through the managed API. 'design_check' only evaluates an explicit maximum cross-slope limit (checkAttainmentLength/checkRunoffLength must both be false).",
      inputShape: canonicalInputShape,
      supportedActions: ["get", "set", "design_check", "report"],
      resolveAction: (rawArgs) => ({ action: String(rawArgs.action ?? ""), args: rawArgs }),
    },
  ],
};
