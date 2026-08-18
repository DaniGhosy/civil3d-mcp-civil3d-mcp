import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const LabelActionSchema = z.enum([
  "extract_text_entities",
  "extract_leader_annotations",
  "extract_dimensions",
  "match_text_to_nearby_geometry",
  "list_label_styles",
  "list_labels",
  "add_label",
]);

const PointSchema = z.object({ x: z.number(), y: z.number(), z: z.number().optional() });

const PositionedItemSchema = z.object({
  handle: z.string(),
  label: z.string().optional().describe("Text or name to carry through into the match result."),
  position: PointSchema,
});

const LabelPointSchema = z.object({ x: z.number(), y: z.number() });

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
  objectType: z.string().optional().describe("Civil 3D object type owning the label, e.g. 'alignment', 'profile', 'surface', 'pipe', 'structure', 'pipe_network' (list_label_styles/list_labels/add_label)."),
  objectName: z.string().optional().describe("Name of the object owning the label (list_labels/add_label)."),
  labelType: z.string().optional().describe("Label kind: 'label_set', 'station', 'spot_elevation'/'spot' (surface), or plain/plan for pipe/structure (add_label)."),
  labelStyle: z.string().optional().describe("Label (set) style name; falls back to the first available style if omitted (add_label)."),
  station: z.number().optional().describe("Station value for 'station'-type labels (add_label)."),
  point: LabelPointSchema.optional().describe("XY location for surface 'spot_elevation' labels (add_label)."),
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
    list_label_styles: {
      action: "list_label_styles",
      inputSchema: z.object({ action: z.literal("list_label_styles"), objectType: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listLabelStyles"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listLabelStyles", { objectType: args.objectType })
        ),
    },
    list_labels: {
      action: "list_labels",
      inputSchema: z.object({
        action: z.literal("list_labels"),
        objectType: z.string(),
        objectName: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listLabels"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listLabels", { objectType: args.objectType, objectName: args.objectName })
        ),
    },
    add_label: {
      action: "add_label",
      inputSchema: z.object({
        action: z.literal("add_label"),
        objectType: z.string(),
        objectName: z.string(),
        labelType: z.string(),
        labelStyle: z.string().optional(),
        station: z.number().optional(),
        point: LabelPointSchema.optional(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addLabel"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addLabel", {
            objectType: args.objectType,
            objectName: args.objectName,
            labelType: args.labelType,
            labelStyle: args.labelStyle,
            station: args.station,
            point: args.point,
          })
        ),
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
        "'unmatchedTexts' instead of being silently dropped. Also manages Civil 3D object labels: " +
        "list_label_styles (label/label-set styles available for an objectType), list_labels " +
        "(labels currently attached to a named object), add_label (attach a label — supports " +
        "alignment/profile label_set and station labels, surface spot_elevation labels, and " +
        "basic pipe/structure labels; label object types are resolved via reflection since the " +
        "exact managed type name shifts between Civil 3D releases).",
      inputShape: canonicalInputShape,
      supportedActions: [
        "extract_text_entities",
        "extract_leader_annotations",
        "extract_dimensions",
        "match_text_to_nearby_geometry",
        "list_label_styles",
        "list_labels",
        "add_label",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
