import { z } from "zod";
import { routeIntent, type RouteParams, type RouteResult } from "../orchestration/IntentRouter.js";
import { findToolCatalogEntry } from "../orchestration/ToolCatalog.js";
import { getProjectContext, type ProjectContext } from "../orchestration/ProjectContextService.js";
import { buildWorkflowPlan } from "../orchestration/WorkflowPlanner.js";
import { resolveParamsFromSelection } from "../orchestration/SelectionResolver.js";
import { executeRegisteredTool } from "./toolHandlerRegistry.js";

const ROUTE_PARAM_KEYS: Array<keyof RouteParams> = [
  "name",
  "alignmentName",
  "corridorName",
  "profileName",
  "surfaceName",
  "networkName",
  "groupName",
  "featureLineName",
  "criteriaName",
  "side",
  "projectFolder",
  "shortcutName",
  "shortcutType",
  "templatePath",
  "save",
  "saveAs",
  "limit",
  "layerPrefix",
  "pipeName",
  "structureName",
  "fittingName",
  "partName",
  "partsList",
  "targetType",
  "targetName",
  "startPoint",
  "endPoint",
  "position",
  "baseSurface",
  "comparisonSurface",
  "style",
  "layer",
  "labelSet",
  "filePath",
  "outputPath",
  "query",
];

const Civil3DOrchestrateInputShape = {
  request: z.string().min(1).optional().describe("Natural-language Civil 3D request."),
  execute: z.boolean().optional().describe("When true, execute the selected action if enough parameters are available."),
  toolName: z.string().optional().describe("Exact registered tool name to plan or execute through the orchestrator."),
  toolAction: z.string().optional().describe("Exact action when targeting a multi-action tool directly."),
  toolParameters: z.record(z.unknown()).optional().describe("Exact tool parameters when targeting a tool directly."),
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
};

const Civil3DOrchestrateInputSchema = z.object(Civil3DOrchestrateInputShape);

export type OrchestrateArgs = z.infer<typeof Civil3DOrchestrateInputSchema>;

function hasRequiredValue(value: unknown): boolean {
  if (typeof value === "string") return value.trim().length > 0;
  if (typeof value === "number") return Number.isFinite(value);
  if (Array.isArray(value)) return value.length > 0;
  return value != null;
}

function pickRouteParams(source?: Record<string, unknown>): Partial<RouteParams> {
  if (!source) return {};
  return Object.fromEntries(ROUTE_PARAM_KEYS.filter((key) => source[key] !== undefined).map((key) => [key, source[key]])) as Partial<RouteParams>;
}

/** Prefers explicit args over whatever the free-text router extracted for the same field. */
function mergeRouteParams(args: OrchestrateArgs, extracted: RouteParams): RouteParams {
  const merged: Record<string, unknown> = { ...extracted };
  for (const key of ROUTE_PARAM_KEYS) {
    const explicitValue = (args as Record<string, unknown>)[key];
    if (explicitValue !== undefined) merged[key] = explicitValue;
  }
  return merged as RouteParams;
}

function findMissingRequiredFields(requiredFields: string[], params: Record<string, unknown>) {
  return requiredFields.filter((field) => !hasRequiredValue(params[field]));
}

function buildDirectRoute(args: OrchestrateArgs): RouteResult {
  const requestedToolName = args.toolName;
  if (!requestedToolName) {
    throw new Error("toolName is required for direct tool orchestration.");
  }

  const requestedAction = args.toolAction ?? (typeof args.toolParameters?.action === "string" ? String(args.toolParameters.action) : undefined);

  const match = findToolCatalogEntry(requestedToolName, requestedAction);
  if (!match) {
    throw new Error(
      requestedAction
        ? `Tool '${requestedToolName}' with action '${requestedAction}' was not found in the tool catalog.`
        : `Tool '${requestedToolName}' was not found in the tool catalog.`
    );
  }

  return {
    match,
    confidence: 1,
    missingFields: [],
    extractedParams: {},
    reasoning: requestedAction
      ? `Used exact tool override for '${requestedToolName}' action '${requestedAction}'.`
      : `Used exact tool override for '${requestedToolName}'.`,
  };
}

function buildExactToolParameters(args: OrchestrateArgs, params: RouteParams, selectedAction: string): Record<string, unknown> {
  const parameterObject = args.toolParameters ?? {};
  const explicitAction = args.toolAction ?? (typeof parameterObject.action === "string" ? String(parameterObject.action) : selectedAction);

  const exactParameters: Record<string, unknown> = {
    action: explicitAction,
    ...params,
    ...parameterObject,
  };

  return Object.fromEntries(Object.entries(exactParameters).filter(([, value]) => value !== undefined));
}

function buildDirectExecutionContext(args: OrchestrateArgs) {
  const routed = buildDirectRoute(args);
  const params = mergeRouteParams(args, pickRouteParams(args.toolParameters) as RouteParams);
  const exactParameters = buildExactToolParameters(args, params, routed.match.action);
  const missingFields = findMissingRequiredFields(routed.match.requiredFields, exactParameters);

  return { routed, params, exactParameters, missingFields };
}

async function executeIntent(intent: RouteResult, params: RouteParams) {
  const toolArgs = intent.match.buildToolArgs(params as unknown as Record<string, unknown>);
  return await executeRegisteredTool(intent.match.toolName, toolArgs);
}

export async function executeToolCallViaOrchestrator(toolName: string, parameters: Record<string, unknown>) {
  const directExecution = buildDirectExecutionContext({
    toolName,
    toolAction: typeof parameters.action === "string" ? String(parameters.action) : undefined,
    toolParameters: parameters,
  } as OrchestrateArgs);

  if (directExecution.missingFields.length > 0) {
    throw new Error(`Missing required fields: ${directExecution.missingFields.join(", ")}`);
  }

  return await executeRegisteredTool(directExecution.routed.match.toolName, directExecution.exactParameters);
}

export async function executeCivil3DOrchestrate(rawArgs: OrchestrateArgs) {
  const args = Civil3DOrchestrateInputSchema.parse(rawArgs);
  if (!args.request && !args.toolName) {
    throw new Error("civil3d_orchestrate requires either a request or a toolName.");
  }

  const directExecution = args.toolName ? buildDirectExecutionContext(args) : null;
  const routed = directExecution?.routed ?? routeIntent(args.request as string);

  let projectContext: ProjectContext | null = null;
  try {
    projectContext = await getProjectContext();
  } catch {
    // No live Civil 3D connection — proceed without selection-based inference or object-count advice.
  }

  const mergedParams = directExecution?.params ?? mergeRouteParams(args, routed.extractedParams);
  const selectionResolution = projectContext
    ? resolveParamsFromSelection(mergedParams, projectContext)
    : { resolvedParams: mergedParams, inferredFromSelection: [] as string[] };
  const params = selectionResolution.resolvedParams;
  const workflowPlan = buildWorkflowPlan(routed, params, projectContext);
  const missingFields = findMissingRequiredFields(routed.match.requiredFields, directExecution?.exactParameters ?? (params as Record<string, unknown>));

  const response: Record<string, unknown> = {
    request: args.request ?? `direct:${args.toolName}`,
    selectedIntent: routed.match.intent,
    selectedTool: routed.match.toolName,
    selectedAction: routed.match.action,
    confidence: routed.confidence,
    reasoning: routed.reasoning,
    params,
    inferredFromSelection: selectionResolution.inferredFromSelection,
    projectContext,
    workflowPlan,
    missingFields,
    canExecute: missingFields.length === 0,
  };

  if (args.execute === true) {
    if (missingFields.length > 0) {
      response.status = "needs_input";
      response.message = `Missing required fields: ${missingFields.join(", ")}`;
    } else {
      response.status = "executed";
      response.result = directExecution
        ? await executeRegisteredTool(routed.match.toolName, directExecution.exactParameters)
        : await executeIntent(routed, params);
    }
  } else {
    response.status = missingFields.length > 0 ? "planned_needs_input" : "planned";
  }

  return response;
}
