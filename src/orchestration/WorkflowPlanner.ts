import type { RouteParams, RouteResult } from "./IntentRouter.js";
import type { ProjectContext } from "./ProjectContextService.js";

export interface WorkflowStep {
  title: string;
  toolName: string;
  action: string;
  requiredFields: string[];
  status: "ready" | "blocked";
}

export interface WorkflowPlan {
  intent: string;
  summary: string;
  steps: WorkflowStep[];
  advice: string[];
}

/**
 * Deliberately much simpler than source's WorkflowPlanner.ts (~20 hand-written narrative
 * branches, one per specific intent — e.g. "corridor readiness" emits 3 hardcoded steps: list
 * alignments, list surfaces, list profiles). That multi-step narration made sense in source
 * because each step there was its own separate MCP tool call. In this repo every matched intent
 * — including the civil3d_workflow ones (project_startup, drawing_readiness_audit, etc.) — is
 * already a single tool+action call; the multi-step composition happens server-side (see
 * WorkflowCommands.cs, ported in the earlier engineering batches). So there's nothing left here
 * to narrate into separate steps: the plan is always "call this one tool action", and the value
 * this file adds is just checking readiness (missing fields) and surfacing a couple of generic,
 * context-aware warnings.
 */
export function buildWorkflowPlan(routed: RouteResult, params: RouteParams, projectContext: ProjectContext | null): WorkflowPlan {
  const { match, missingFields } = routed;

  const step: WorkflowStep = {
    title: match.title,
    toolName: match.toolName,
    action: match.action,
    requiredFields: match.requiredFields,
    status: missingFields.length === 0 ? "ready" : "blocked",
  };

  const advice: string[] = [];

  if (missingFields.length > 0) {
    advice.push(`Missing required field(s): ${missingFields.join(", ")}. Supply them and retry, or reference an object by name in the request text.`);
  }

  if (!projectContext) {
    advice.push("Could not read the active drawing's project context (no connection to the Civil 3D plugin) — parameter inference from the current selection was skipped.");
  } else if (match.toolName === "civil3d_corridor" && match.action === "rebuild") {
    const hasSurfaces = projectContext.objectTypes.includes("Surface");
    const hasAlignments = projectContext.objectTypes.includes("Alignment");
    if (!hasSurfaces || !hasAlignments) {
      advice.push(
        `This drawing currently has ${hasAlignments ? "" : "no alignments and "}${hasSurfaces ? "" : "no surfaces"} — a corridor rebuild typically needs both a baseline alignment and target surfaces.`.replace("  ", " ")
      );
    }
  }

  return {
    intent: match.intent,
    summary: `Run '${match.title}' via ${match.toolName} (action: ${match.action}).`,
    steps: [step],
    advice,
  };
}
