import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { approvalPolicy, isApprovalRequired } from "./approvalPolicy.js";
import { captureToolHandler } from "./toolHandlerRegistry.js";
import { DOMAIN_DEFINITIONS } from "./register.js";
import type { DomainActionDefinition, DomainToolExposure } from "./domainRuntime.js";

type JsonObject = Record<string, unknown>;

const approvalInputShape = {
  toolName: z.string().min(1),
  action: z.string().min(1),
  parameters: z.record(z.unknown()),
  ttlSeconds: z.number().int().min(30).max(600).optional(),
};

/**
 * Finds the exposure + underlying action definition for a (toolName, action) pair by scanning
 * DOMAIN_DEFINITIONS (register.ts) — this repo's equivalent of source's toolManifest.ts registry.
 */
function findManifestAction(
  toolName: string,
  action: string
): { exposure: DomainToolExposure; actionDefinition: DomainActionDefinition } | undefined {
  for (const definition of DOMAIN_DEFINITIONS) {
    const exposure = definition.exposures.find((candidate) => candidate.toolName === toolName);
    if (!exposure) continue;
    const actionDefinition = definition.actions[action];
    if (!actionDefinition || !exposure.supportedActions.includes(action)) return undefined;
    return { exposure, actionDefinition };
  }
  return undefined;
}

function resolveApprovalTarget(args: { toolName: string; action: string; parameters: JsonObject }) {
  const target = findManifestAction(args.toolName, args.action);
  if (!target) {
    throw new Error(`Unknown tool action '${args.toolName}' / '${args.action}'.`);
  }

  const resolved = target.exposure.resolveAction(args.parameters);
  if (resolved.action !== args.action) {
    throw new Error(
      `Parameters do not resolve to action '${args.action}' for tool '${args.toolName}'. ` +
        "Include the tool action in parameters when calling a multi-action tool."
    );
  }
  target.actionDefinition.inputSchema.parse(resolved.args);
  return target;
}

function successResult(structuredContent: JsonObject) {
  return {
    content: [{ type: "text" as const, text: JSON.stringify(structuredContent, null, 2) }],
  };
}

function errorResult(toolName: string, error: unknown) {
  const message = error instanceof Error ? error.message : String(error);
  return {
    content: [{ type: "text" as const, text: `${toolName} failed: ${message}` }],
    isError: true,
  };
}

export function registerApprovalTool(server: McpServer) {
  const previewHandler = async (rawArgs: Record<string, unknown>) => {
    try {
      const args = rawArgs as unknown as { toolName: string; action: string; parameters: JsonObject };
      const target = resolveApprovalTarget(args);
      const requiresApproval = isApprovalRequired({
        toolName: args.toolName,
        action: args.action,
        capabilities: target.actionDefinition.capabilities,
        safeForRetry: target.actionDefinition.safeForRetry,
        requiresActiveDrawing: target.actionDefinition.requiresActiveDrawing,
      });
      return successResult({
        status: requiresApproval ? "approval_required" : "ready",
        toolName: args.toolName,
        action: args.action,
        capabilities: target.actionDefinition.capabilities,
        safeForRetry: target.actionDefinition.safeForRetry,
        instruction: requiresApproval
          ? "Call civil3d_request_approval with the same toolName, action, and parameters before execution."
          : "This action may be executed directly.",
      });
    } catch (error) {
      return errorResult("civil3d_preview_action", error);
    }
  };

  const requestHandler = async (rawArgs: Record<string, unknown>) => {
    try {
      const args = rawArgs as unknown as {
        toolName: string;
        action: string;
        parameters: JsonObject;
        ttlSeconds?: number;
      };
      const target = resolveApprovalTarget(args);
      const receipt = await approvalPolicy.requestApproval(
        {
          toolName: args.toolName,
          action: args.action,
          capabilities: target.actionDefinition.capabilities,
          safeForRetry: target.actionDefinition.safeForRetry,
          requiresActiveDrawing: target.actionDefinition.requiresActiveDrawing,
        },
        args.parameters,
        (args.ttlSeconds ?? 300) * 1000
      );

      return successResult({
        status: "approved",
        toolName: args.toolName,
        action: args.action,
        ...receipt,
        instruction: "Retry the approved tool call with approvalToken and the identical parameters.",
      });
    } catch (error) {
      return errorResult("civil3d_request_approval", error);
    }
  };

  captureToolHandler("civil3d_preview_action", previewHandler);
  captureToolHandler("civil3d_request_approval", requestHandler);

  server.tool(
    "civil3d_preview_action",
    "Validates and previews whether a Civil 3D action requires approval before it can execute. This tool never changes the drawing.",
    {
      toolName: approvalInputShape.toolName,
      action: approvalInputShape.action,
      parameters: approvalInputShape.parameters,
    },
    previewHandler
  );

  server.tool(
    "civil3d_request_approval",
    "Issues a short-lived, single-use approval token for a specific destructive or ambiguous Civil 3D action in the active drawing.",
    approvalInputShape,
    requestHandler
  );
}
