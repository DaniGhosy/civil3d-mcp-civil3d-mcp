import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const LabelActionSchema = z.enum([
  "extract_text_entities",
  "extract_leader_annotations",
  "extract_dimensions",
  "match_text_to_nearby_geometry",
]);

const PointSchema = z.object({ x: z.number(), y: z.number(), z: z.number().optional() });

const PositionedItemSchema = z.object({
  handle: z.string(),
  label: z.string().optional().describe("Text or name to carry through into the match result."),
  position: PointSchema,
});

const canonicalInputShape = {
  action: LabelActionSchema.describe("The text/annotation-reading operation to perform."),
  layer: z.string().optional().describe("Layer name to filter results by (extract_text_entities, extract_dimensions)."),
  texts: z
    .array(PositionedItemSchema)
    .optional()
    .describe("Text items already extracted (e.g. from extract_text_entities) — match_text_to_nearby_geometry."),
  geometry: z
    .array(PositionedItemSchema)
    .optional()
    .describe("Geometry items already extracted (e.g. from civil3d_blocks) — match_text_to_nearby_geometry."),
  radius: z
    .number()
    .optional()
    .describe("Max distance (drawing units) to consider a text/geometry pair a match. Default 1.0 — match_text_to_nearby_geometry."),
};

function matchTextToNearbyGeometry(
  texts: { handle: string; label?: string; position: { x: number; y: number; z?: number } }[],
  geometry: { handle: string; label?: string; position: { x: number; y: number; z?: number } }[],
  radius: number
) {
  const distance = (a: { x: number; y: number }, b: { x: number; y: number }) =>
    Math.hypot(a.x - b.x, a.y - b.y);

  const matches: {
    textHandle: string;
    text?: string;
    geometryHandle: string;
    geometryLabel?: string;
    distance: number;
  }[] = [];
  const unmatchedTexts: string[] = [];

  for (const text of texts) {
    let closest: { handle: string; label?: string; position: { x: number; y: number } } | null = null;
    let closestDistance = Infinity;

    for (const geom of geometry) {
      const d = distance(text.position, geom.position);
      if (d <= radius && d < closestDistance) {
        closest = geom;
        closestDistance = d;
      }
    }

    if (closest) {
      matches.push({
        textHandle: text.handle,
        text: text.label,
        geometryHandle: closest.handle,
        geometryLabel: closest.label,
        distance: closestDistance,
      });
    } else {
      unmatchedTexts.push(text.handle);
    }
  }

  return { matches, unmatchedTexts };
}

export const LABEL_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "label",
  actions: {
    extract_text_entities: {
      action: "extract_text_entities",
      inputSchema: z.object({ action: z.literal("extract_text_entities"), layer: z.string().optional() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["extractTextEntities"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("extractTextEntities", { layer: args.layer })
        ),
    },
    extract_leader_annotations: {
      action: "extract_leader_annotations",
      inputSchema: z.object({ action: z.literal("extract_leader_annotations") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["extractLeaderAnnotations"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("extractLeaderAnnotations", {})
        ),
    },
    extract_dimensions: {
      action: "extract_dimensions",
      inputSchema: z.object({ action: z.literal("extract_dimensions"), layer: z.string().optional() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["extractDimensions"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("extractDimensions", { layer: args.layer })
        ),
    },
    match_text_to_nearby_geometry: {
      action: "match_text_to_nearby_geometry",
      inputSchema: z.object({
        action: z.literal("match_text_to_nearby_geometry"),
        texts: z.array(PositionedItemSchema),
        geometry: z.array(PositionedItemSchema),
        radius: z.number().optional(),
      }),
      capabilities: ["analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) =>
        matchTextToNearbyGeometry(args.texts, args.geometry, args.radius ?? 1.0),
    },
  },
  exposures: [
    {
      toolName: "civil3d_label",
      displayName: "Civil 3D Labels & Annotations",
      description:
        "Read text, leaders, and dimensions straight from the drawing — complements civil3d_blocks by " +
        "extracting the labels/notes/measurements near the symbols it counts. Actions: " +
        "extract_text_entities (DBText + MText, with position, optionally filtered by layer), " +
        "extract_leader_annotations (Leader and MLeader entities with their attached text, where " +
        "resolvable), extract_dimensions (Dimension entities — measurement, override text, position, " +
        "optionally filtered by layer). match_text_to_nearby_geometry is pure post-processing (does " +
        "NOT call the plugin, no active drawing required): pairs already-extracted text items with " +
        "the closest already-extracted geometry item within a radius (e.g. matching an outlet label " +
        "to the nearest civil3d_blocks insertion point) — items with nothing in range come back in " +
        "'unmatchedTexts' instead of being silently dropped.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "extract_text_entities",
        "extract_leader_annotations",
        "extract_dimensions",
        "match_text_to_nearby_geometry",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
