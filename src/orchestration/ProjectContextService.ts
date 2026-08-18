import { withApplicationConnection } from "../utils/ConnectionManager.js";

export interface ProjectContext {
  drawingInfo: unknown | null;
  objectTypes: string[];
  selectedObjects: unknown[];
}

/**
 * Adapted from source: source's plugin exposes one consolidated getProjectContext RPC method.
 * This plugin doesn't have that (and adding one just to save two round trips wasn't worth a new
 * C# method) — instead this composes three calls this plugin already has and already uses
 * elsewhere (getDrawingInfo, listCivilObjectTypes, getSelectedCivilObjectsInfo — see
 * WorkflowCommands.DrawingReadinessAuditWorkflowAsync for the same trio), issued over one shared
 * connection. Each call is independently tolerant of failure (e.g. no active drawing) so a
 * partial context is still returned rather than the whole thing failing.
 */
export async function getProjectContext(selectedObjectLimit = 25): Promise<ProjectContext> {
  return withApplicationConnection(async (appClient) => {
    const [drawingInfo, objectTypes, selected] = await Promise.all([
      appClient.sendCommand("getDrawingInfo", {}).catch(() => null),
      appClient.sendCommand("listCivilObjectTypes", {}).catch(() => []),
      appClient.sendCommand("getSelectedCivilObjectsInfo", { limit: selectedObjectLimit }).catch(() => ({ objects: [] })),
    ]);

    const selectedObjects =
      selected && typeof selected === "object" && Array.isArray((selected as { objects?: unknown }).objects)
        ? (selected as { objects: unknown[] }).objects
        : [];

    return {
      drawingInfo: drawingInfo ?? null,
      objectTypes: Array.isArray(objectTypes) ? (objectTypes as string[]) : [],
      selectedObjects,
    };
  });
}
