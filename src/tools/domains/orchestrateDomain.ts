import { z } from "zod";
import type { DomainToolDefinition } from "../domainRuntime.js";
import { buildDomainToolCatalogEntries } from "../domainRuntime.js";
import { DOMAIN_DEFINITIONS } from "../register.js";

const PayItemSchema = z.object({
  code: z.string(),
  description: z.string(),
  unit: z.string(),
  unitPrice: z.number().nonnegative(),
});

const OrchestrateArgsSchema = z.object({
  action: z.literal("orchestrate"),
  request: z.string().min(1).optional(),
  execute: z.boolean().optional(),
  toolName: z.string().optional(),
  toolAction: z.string().optional(),
  toolParameters: z.record(z.unknown()).optional(),
  name: z.string().optional(),
  alignmentName: z.string().optional(),
  corridorName: z.string().optional(),
  profileName: z.string().optional(),
  surfaceName: z.string().optional(),
  networkName: z.string().optional(),
  groupName: z.string().optional(),
  featureLineName: z.string().optional(),
  criteriaName: z.string().optional(),
  side: z.string().optional(),
  projectFolder: z.string().optional(),
  shortcutName: z.string().optional(),
  shortcutType: z.string().optional(),
  templatePath: z.string().optional(),
  save: z.boolean().optional(),
  saveAs: z.string().optional(),
  limit: z.number().optional(),
  layerPrefix: z.string().optional(),
  pipeName: z.string().optional(),
  structureName: z.string().optional(),
  fittingName: z.string().optional(),
  partName: z.string().optional(),
  partsList: z.string().optional(),
  targetType: z.string().optional(),
  targetName: z.string().optional(),
  startPoint: z.object({ x: z.number(), y: z.number(), z: z.number().optional() }).optional(),
  endPoint: z.object({ x: z.number(), y: z.number(), z: z.number().optional() }).optional(),
  position: z.object({ x: z.number(), y: z.number(), z: z.number().optional() }).optional(),
  baseSurface: z.string().optional(),
  comparisonSurface: z.string().optional(),
  style: z.string().optional(),
  layer: z.string().optional(),
  labelSet: z.string().optional(),
  filePath: z.string().optional(),
  outputPath: z.string().optional(),
  query: z.string().optional(),
  payItems: z.array(PayItemSchema).optional(),
});

const ListToolCapabilitiesArgsSchema = z.object({
  action: z.literal("list_tool_capabilities"),
  domain: z.string().optional(),
  toolName: z.string().optional(),
});

export const ORCHESTRATE_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "docs",
  actions: {
    orchestrate: {
      action: "orchestrate",
      inputSchema: OrchestrateArgsSchema,
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) => {
        const { executeCivil3DOrchestrate } = await import("../civil3d_orchestrate.js");
        return await executeCivil3DOrchestrate(args);
      },
    },
    list_tool_capabilities: {
      action: "list_tool_capabilities",
      inputSchema: ListToolCapabilitiesArgsSchema,
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) => {
        const entries = DOMAIN_DEFINITIONS.flatMap((definition) => buildDomainToolCatalogEntries(definition));
        const filtered = entries.filter(
          (entry) => (!args.domain || entry.domain === args.domain) && (!args.toolName || entry.toolName === args.toolName)
        );
        return { totalTools: entries.length, tools: filtered };
      },
    },
  },
  exposures: [
    {
      toolName: "civil3d_docs",
      displayName: "Civil 3D Docs & Orchestration",
      description:
        "Two actions in one tool: list_tool_capabilities (introspects every registered " +
        "civil3d_* tool/action — domain, capabilities, plugin methods, whether it requires an " +
        "active drawing — filterable by domain/toolName) and orchestrate (route a natural- " +
        "language request to the right tool+action via local keyword matching — no LLM call — " +
        "extract parameters from the request text and the current AutoCAD selection, report " +
        "what's still missing, and optionally execute it). See civil3d_orchestrate for a " +
        "dedicated single-purpose exposure of just the orchestrate action.",
      inputShape: (() => {
        const { action: _action, ...orchestrateFields } = OrchestrateArgsSchema.shape;
        return {
          action: z.enum(["orchestrate", "list_tool_capabilities"]),
          ...orchestrateFields,
          domain: z.string().optional(),
        };
      })(),
      supportedActions: ["orchestrate", "list_tool_capabilities"],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action ?? "orchestrate"),
        args: rawArgs,
      }),
    },
    {
      toolName: "civil3d_orchestrate",
      displayName: "Civil 3D Orchestrate",
      description:
        "Routes a natural-language Civil 3D request to the right civil3d_* tool and action " +
        "using local keyword matching (no LLM call, no network dependency beyond the usual " +
        "Civil 3D plugin connection). Extracts parameters from the request text via regex plus " +
        "(if a drawing is connected) the current AutoCAD selection, reports which required " +
        "fields are still missing, and — only when execute:true and nothing is missing — " +
        "actually runs the resolved tool call and returns its result. Pass toolName (+ optional " +
        "toolAction/toolParameters) instead of request to target a specific tool directly, " +
        "bypassing keyword matching. Executing a mutating action through here still goes " +
        "through that action's own approval gate — this tool has no path to attach an " +
        "approvalToken, so a destructive action requested via execute:true will report " +
        "'Approval required' rather than silently running.",
      inputShape: (() => {
        const { action: _action, ...rest } = OrchestrateArgsSchema.shape;
        return rest;
      })(),
      supportedActions: ["orchestrate"],
      resolveAction: (rawArgs) => ({
        action: "orchestrate",
        args: { ...rawArgs, action: "orchestrate" },
      }),
    },
  ],
};
