import { z, type ZodRawShape, type ZodTypeAny } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ToolCapability, ToolCatalogEntry, ToolDomain } from "./toolMetadata.js";
import { captureToolHandler } from "./toolHandlerRegistry.js";
import { approvalPolicy, getActiveDrawingFingerprint } from "./approvalPolicy.js";
import { idempotencyStore } from "./idempotencyStore.js";
import { maybeStoreReportResource } from "./reportResourceStore.js";

type JsonObject = Record<string, unknown>;

/**
 * Defines a single action within a domain (e.g. "list", "get", "create").
 */
export interface DomainActionDefinition<TArgs = JsonObject> {
  action: string;
  inputSchema: z.ZodType<TArgs>;
  execute: (args: TArgs) => Promise<unknown>;
  responseSchema?: ZodTypeAny;
  capabilities: ToolCapability[];
  requiresActiveDrawing: boolean;
  safeForRetry: boolean;
  pluginMethods?: string[];
}

/**
 * Defines how a domain tool is exposed to the MCP client.
 * A single exposure becomes one MCP tool with multiple actions.
 */
export interface DomainToolExposure {
  toolName: string;
  displayName: string;
  description: string;
  inputShape: ZodRawShape;
  supportedActions: string[];
  resolveAction: (rawArgs: JsonObject) => { action: string; args: JsonObject };
  capabilities?: ToolCapability[];
  operations?: string[];
  pluginMethods?: string[];
  requiresActiveDrawing?: boolean;
  safeForRetry?: boolean;
  status?: "implemented" | "planned";
}

/**
 * Full definition of a domain — its actions and how they're exposed as tools.
 */
export interface DomainToolDefinition {
  domain: ToolDomain;
  actions: Record<string, DomainActionDefinition>;
  exposures: DomainToolExposure[];
}

function uniqueStrings(values: Iterable<string | undefined>): string[] | undefined {
  const unique = [...new Set([...values].filter((v): v is string => Boolean(v)))];
  return unique.length > 0 ? unique : undefined;
}

function uniqueCapabilities(values: Iterable<ToolCapability | undefined>): ToolCapability[] {
  return [...new Set([...values].filter((v): v is ToolCapability => Boolean(v)))];
}

function buildToolErrorResult(toolName: string, actionName: string | undefined, error: unknown) {
  const message = error instanceof Error ? error.message : String(error);
  const scopedName = actionName ? `${toolName} action '${actionName}'` : toolName;

  console.error(`Error in ${scopedName}:`, error);

  return {
    content: [
      {
        type: "text" as const,
        text: `${scopedName} failed: ${message}`,
      },
    ],
    isError: true,
  };
}

/**
 * Execute a domain tool exposure with the given raw args.
 */
/**
 * Approval gating, idempotent-retry support, and report-resource caching (Fase 2, ported from
 * Civil3D-mcp-main). All three hook in here — the single choke point every domain action already
 * passes through — rather than touching each domain file. Adapted from source in two ways:
 *   - No progress notifications / requestId / logger wiring (source's version also threads a
 *     request-scoped logger and MCP progress notifications through here; this repo doesn't have
 *     that infrastructure and didn't need it added just for this).
 *   - No "resource_link" content block for cached reports (this repo's older MCP SDK doesn't
 *     support that content type) — instead the JSON result gets a plain `_reportResource:
 *     {uri, name}` field merged in when a report was cached, so the pointer is still visible to
 *     the caller; the resource itself is still fetchable via civil3d://reports/{id}
 *     (see mcpResources.ts).
 */
async function executeExposure(
  definition: DomainToolDefinition,
  exposure: DomainToolExposure,
  rawArgs: JsonObject
) {
  const resolved = exposure.resolveAction(rawArgs);
  const actionName = resolved.action;

  if (!exposure.supportedActions.includes(actionName)) {
    throw new Error(
      `Unsupported action '${actionName}' for tool '${exposure.toolName}'. ` +
        `Supported actions: ${exposure.supportedActions.join(", ")}.`
    );
  }

  const actionDefinition = definition.actions[actionName];
  if (!actionDefinition) {
    throw new Error(`Action '${actionName}' is not defined for domain '${definition.domain}'.`);
  }

  const parsedArgs = actionDefinition.inputSchema.parse(resolved.args);

  const idempotencyKey = typeof rawArgs.idempotencyKey === "string" ? rawArgs.idempotencyKey : undefined;
  if (idempotencyKey && !actionDefinition.safeForRetry) {
    const error = new Error(`Action '${actionName}' does not support idempotent retries.`) as Error & { code: string };
    error.code = "CIVIL3D.INVALID_INPUT";
    throw error;
  }

  const executeOnce = async () => {
    await approvalPolicy.enforce(
      {
        toolName: exposure.toolName,
        action: actionName,
        capabilities: actionDefinition.capabilities,
        safeForRetry: actionDefinition.safeForRetry,
        requiresActiveDrawing: actionDefinition.requiresActiveDrawing,
      },
      rawArgs
    );

    const response = await actionDefinition.execute(parsedArgs);
    const validatedResponse = actionDefinition.responseSchema
      ? actionDefinition.responseSchema.parse(response)
      : response;
    const serializedResponse = JSON.stringify(validatedResponse, null, 2);
    const reportResource = maybeStoreReportResource(actionName, serializedResponse);

    const resultForCaller =
      reportResource && validatedResponse && typeof validatedResponse === "object" && !Array.isArray(validatedResponse)
        ? { ...(validatedResponse as Record<string, unknown>), _reportResource: { uri: reportResource.uri, name: reportResource.name } }
        : validatedResponse;

    return {
      content: [
        {
          type: "text" as const,
          text: JSON.stringify(resultForCaller, null, 2),
        },
      ],
    };
  };

  if (!idempotencyKey) return executeOnce();

  const signature = { ...rawArgs };
  delete signature.approvalToken;
  delete signature.idempotencyKey;
  const drawingScope = actionDefinition.requiresActiveDrawing
    ? await getActiveDrawingFingerprint()
    : "drawing-independent";
  return idempotencyStore.execute(`${exposure.toolName}:${actionName}:${drawingScope}`, idempotencyKey, signature, executeOnce);
}

/**
 * Register all tool exposures from a domain definition with the MCP server.
 */
export function registerDomainTools(server: McpServer, definition: DomainToolDefinition) {
  for (const exposure of definition.exposures) {
    const handler = async (rawArgs: Record<string, unknown>) => {
      try {
        return await executeExposure(definition, exposure, rawArgs as JsonObject);
      } catch (error) {
        const actionName =
          typeof (rawArgs as JsonObject).action === "string"
            ? String((rawArgs as JsonObject).action)
            : exposure.supportedActions.length === 1
              ? exposure.supportedActions[0]
              : undefined;

        return buildToolErrorResult(exposure.toolName, actionName, error);
      }
    };

    const inputShapeWithApprovalFields: ZodRawShape = {
      ...exposure.inputShape,
      approvalToken: z.string().min(1).optional(),
      idempotencyKey: z.string().min(1).max(128).optional(),
    };

    server.tool(exposure.toolName, exposure.description, inputShapeWithApprovalFields, handler);
    captureToolHandler(exposure.toolName, handler);
  }
}

/**
 * Build catalog entries from a domain definition (for documentation/introspection).
 */
export function buildDomainToolCatalogEntries(
  definition: DomainToolDefinition
): ToolCatalogEntry[] {
  return definition.exposures.map((exposure) => {
    const supportedActionDefs = exposure.supportedActions
      .map((a) => definition.actions[a])
      .filter((a): a is DomainActionDefinition => Boolean(a));

    return {
      toolName: exposure.toolName,
      displayName: exposure.displayName,
      description: exposure.description,
      domain: definition.domain,
      capabilities:
        exposure.capabilities ??
        uniqueCapabilities(supportedActionDefs.flatMap((a) => a.capabilities)),
      operations:
        exposure.operations ??
        (exposure.supportedActions.length > 1 ? exposure.supportedActions : undefined),
      pluginMethods:
        exposure.pluginMethods ??
        uniqueStrings(supportedActionDefs.flatMap((a) => a.pluginMethods ?? [])),
      requiresActiveDrawing:
        exposure.requiresActiveDrawing ??
        supportedActionDefs.some((a) => a.requiresActiveDrawing),
      safeForRetry:
        exposure.safeForRetry ?? supportedActionDefs.every((a) => a.safeForRetry),
      status: exposure.status ?? "implemented",
    };
  });
}
