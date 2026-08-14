import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

/**
 * Módulo D del catálogo de lectura de planos: geometría cruda / heurística, para símbolos
 * dibujados a mano sin bloque. No hay exactitud garantizada acá — se trabaja con probabilidad y
 * nivel de confianza. classify_geometry_by_signature es la única acción TS-only (post-proceso
 * puro, sin plugin, sin dibujo activo); las otras tres leen geometría real del dibujo.
 */

const ShapeDetectionActionSchema = z.enum([
  "detect_parallel_line_pairs",
  "group_entities_by_proximity",
  "get_entity_extended_data",
  "classify_geometry_by_signature",
]);

const SignatureSchema = z.object({
  name: z.string(),
  entityTypes: z.array(z.string()),
  description: z.string().optional(),
});

const canonicalInputShape = {
  action: ShapeDetectionActionSchema.describe("The raw-geometry-reading operation to perform."),
  layer: z.string().optional().describe("Layer name to filter by (detect_parallel_line_pairs, group_entities_by_proximity)."),
  angleToleranceDegrees: z.number().optional().describe("Max angle difference to consider two lines parallel. Default 2.0 (detect_parallel_line_pairs)."),
  maxDistance: z.number().optional().describe("Max perpendicular distance between a parallel line pair. Default 1.0 (detect_parallel_line_pairs)."),
  radius: z.number().optional().describe("Max distance between entity centers to group them together (group_entities_by_proximity)."),
  handle: z.string().optional().describe("Entity handle to read XDATA from (get_entity_extended_data)."),
  appName: z.string().optional().describe("Restrict to one registered application's XDATA. Omit to return every application (get_entity_extended_data)."),
  entityTypes: z
    .array(z.string())
    .optional()
    .describe("Entity type names of an already-extracted group, e.g. from group_entities_by_proximity (classify_geometry_by_signature)."),
  signatures: z
    .array(SignatureSchema)
    .optional()
    .describe("Known geometric signatures to compare against, e.g. from civil3d_legend.load_office_standard (classify_geometry_by_signature)."),
};

function classifyGeometryBySignature(
  entityTypes: string[],
  signatures: { name: string; entityTypes: string[]; description?: string }[]
) {
  const observed = new Set(entityTypes.map((t) => t.toLowerCase()));

  const matches = signatures
    .map((sig) => {
      const expected = new Set(sig.entityTypes.map((t) => t.toLowerCase()));
      const intersection = [...observed].filter((t) => expected.has(t)).length;
      const union = new Set([...observed, ...expected]).size;
      const confidence = union === 0 ? 0 : intersection / union;
      return { name: sig.name, description: sig.description, confidence };
    })
    .sort((a, b) => b.confidence - a.confidence);

  return { matches, bestMatch: matches.find((m) => m.confidence > 0) ?? null };
}

export const SHAPE_DETECTION_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "shape_detection",
  actions: {
    detect_parallel_line_pairs: {
      action: "detect_parallel_line_pairs",
      inputSchema: z.object({
        action: z.literal("detect_parallel_line_pairs"),
        layer: z.string().optional(),
        angleToleranceDegrees: z.number().optional(),
        maxDistance: z.number().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["detectParallelLinePairs"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("detectParallelLinePairs", {
            layer: args.layer,
            angleToleranceDegrees: args.angleToleranceDegrees,
            maxDistance: args.maxDistance,
          })
        ),
    },
    group_entities_by_proximity: {
      action: "group_entities_by_proximity",
      inputSchema: z.object({
        action: z.literal("group_entities_by_proximity"),
        layer: z.string().optional(),
        radius: z.number(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["groupEntitiesByProximity"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("groupEntitiesByProximity", { layer: args.layer, radius: args.radius })
        ),
    },
    get_entity_extended_data: {
      action: "get_entity_extended_data",
      inputSchema: z.object({
        action: z.literal("get_entity_extended_data"),
        handle: z.string(),
        appName: z.string().optional(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getEntityExtendedData"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getEntityExtendedData", { handle: args.handle, appName: args.appName })
        ),
    },
    classify_geometry_by_signature: {
      action: "classify_geometry_by_signature",
      inputSchema: z.object({
        action: z.literal("classify_geometry_by_signature"),
        entityTypes: z.array(z.string()),
        signatures: z.array(SignatureSchema),
      }),
      capabilities: ["analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) => classifyGeometryBySignature(args.entityTypes, args.signatures),
    },
  },
  exposures: [
    {
      toolName: "civil3d_shape_detection",
      displayName: "Civil 3D Raw Shape Detection",
      description:
        "Read hand-drawn geometry that isn't a block — lines, circles, loose segments — with " +
        "probability instead of the exactness civil3d_blocks gives for real blocks. Actions: " +
        "detect_parallel_line_pairs (finds Line pairs at a roughly constant perpendicular distance " +
        "— the typical signature of a hand-drawn pipe), group_entities_by_proximity (clusters loose " +
        "entities within a radius into single symbolic units via transitive adjacency, so a 3+ piece " +
        "symbol groups even if not every pair is within range of every other), get_entity_extended_data " +
        "(reads XDATA — DBObject.XData/GetXDataForApplication — grouped by registered application, " +
        "omit appName for every application on the object). classify_geometry_by_signature is pure " +
        "post-processing (does NOT call the plugin, no active drawing required): scores an " +
        "already-extracted group's entity types against a catalog of known signatures (e.g. from " +
        "civil3d_legend.load_office_standard) by Jaccard similarity, returning every match ranked by " +
        "confidence — this is probabilistic pattern matching against a hand-maintained signature " +
        "catalog, not trained machine learning.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "detect_parallel_line_pairs",
        "group_entities_by_proximity",
        "get_entity_extended_data",
        "classify_geometry_by_signature",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
