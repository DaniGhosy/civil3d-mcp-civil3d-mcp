import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const GeometryActionSchema = z.enum([
  "create_line",
  "create_polyline",
  "create_3d_polyline",
  "create_text",
  "create_mtext",
  "offset_lines_to_boundary",
  "cogo_inverse",
  "cogo_direction_distance",
  "cogo_traverse",
  "cogo_curve_solve",
]);

const CogoCourseSchema = z.object({
  bearingDegrees: z.number(),
  distance: z.number(),
  slope: z.number().optional().describe("Percent slope, for elevation change along the course."),
  description: z.string().optional(),
});

const canonicalInputShape = {
  action: GeometryActionSchema.describe("The geometry operation to perform."),
  startX: z.number().optional(),
  startY: z.number().optional(),
  startZ: z.number().optional(),
  endX: z.number().optional(),
  endY: z.number().optional(),
  endZ: z.number().optional(),
  vertices: z
    .array(z.object({ x: z.number(), y: z.number(), z: z.number().optional() }))
    .optional()
    .describe("Array of vertices for polyline creation."),
  closed: z.boolean().optional().describe("Whether the polyline is closed."),
  layer: z.string().optional().describe("Layer name."),
  text: z.string().optional().describe("Text content (for create_text/create_mtext)."),
  insertionX: z.number().optional().describe("Insertion point X."),
  insertionY: z.number().optional().describe("Insertion point Y."),
  height: z.number().optional().describe("Text height."),
  rotation: z.number().optional().describe("Rotation angle in degrees."),
  sourceLayer: z.string().optional().describe("Layer containing the Line entities to copy (offset_lines_to_boundary)."),
  boundaryLayer: z.string().optional().describe("Layer containing the closed boundary Polyline (offset_lines_to_boundary)."),
  directionDegrees: z.number().optional().describe("Advance direction in degrees, 0=+X, 90=+Y (offset_lines_to_boundary)."),
  spacing: z.number().optional().describe("Distance in meters between successive copies (offset_lines_to_boundary)."),
  maxCopies: z.number().optional().describe("Safety limit on number of iterations (offset_lines_to_boundary)."),
  x1: z.number().optional().describe("First point X (cogo_inverse)."),
  y1: z.number().optional().describe("First point Y (cogo_inverse)."),
  x2: z.number().optional().describe("Second point X (cogo_inverse)."),
  y2: z.number().optional().describe("Second point Y (cogo_inverse)."),
  fromX: z.number().optional().describe("Origin point X (cogo_direction_distance)."),
  fromY: z.number().optional().describe("Origin point Y (cogo_direction_distance)."),
  fromZ: z.number().optional().describe("Origin point Z (cogo_direction_distance)."),
  bearingDegrees: z.number().optional().describe("Bearing clockwise from North, degrees (cogo_direction_distance)."),
  distance: z.number().optional().describe("Distance along the bearing (cogo_direction_distance)."),
  slope: z.number().optional().describe("Percent slope for elevation change (cogo_direction_distance)."),
  courses: z.array(CogoCourseSchema).optional().describe("Ordered list of bearing/distance courses (cogo_traverse). Uses startX/startY/startZ above as the traverse start point."),
  isClosed: z.boolean().optional().describe("Whether the traverse is a closed loop, computes closure error (cogo_traverse)."),
  radius: z.number().optional().describe("Curve radius (cogo_curve_solve)."),
  deltaDegrees: z.number().optional().describe("Curve central angle, degrees (cogo_curve_solve)."),
  length: z.number().optional().describe("Curve arc length (cogo_curve_solve)."),
  tangent: z.number().optional().describe("Curve tangent length (cogo_curve_solve)."),
  chord: z.number().optional().describe("Curve chord length (cogo_curve_solve)."),
};

export const GEOMETRY_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "geometry",
  actions: {
    create_line: {
      action: "create_line",
      inputSchema: z.object({
        action: z.literal("create_line"),
        startX: z.number(),
        startY: z.number(),
        startZ: z.number().optional(),
        endX: z.number(),
        endY: z.number(),
        endZ: z.number().optional(),
        layer: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createLineSegment"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createLineSegment", {
            startX: args.startX,
            startY: args.startY,
            startZ: args.startZ,
            endX: args.endX,
            endY: args.endY,
            endZ: args.endZ,
            layer: args.layer,
          })
        ),
    },
    create_polyline: {
      action: "create_polyline",
      inputSchema: z.object({
        action: z.literal("create_polyline"),
        vertices: z.array(z.object({ x: z.number(), y: z.number() })),
        closed: z.boolean().optional(),
        layer: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createPolyline"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createPolyline", {
            vertices: args.vertices,
            closed: args.closed ?? false,
            layer: args.layer,
          })
        ),
    },
    create_3d_polyline: {
      action: "create_3d_polyline",
      inputSchema: z.object({
        action: z.literal("create_3d_polyline"),
        vertices: z.array(z.object({ x: z.number(), y: z.number(), z: z.number() })),
        closed: z.boolean().optional(),
        layer: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["create3dPolyline"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("create3dPolyline", {
            vertices: args.vertices,
            closed: args.closed ?? false,
            layer: args.layer,
          })
        ),
    },
    create_text: {
      action: "create_text",
      inputSchema: z.object({
        action: z.literal("create_text"),
        text: z.string(),
        insertionX: z.number(),
        insertionY: z.number(),
        height: z.number().optional(),
        rotation: z.number().optional(),
        layer: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createText"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createText", {
            text: args.text,
            insertionX: args.insertionX,
            insertionY: args.insertionY,
            height: args.height ?? 1.0,
            rotation: args.rotation ?? 0,
            layer: args.layer,
          })
        ),
    },
    create_mtext: {
      action: "create_mtext",
      inputSchema: z.object({
        action: z.literal("create_mtext"),
        text: z.string(),
        insertionX: z.number(),
        insertionY: z.number(),
        height: z.number().optional(),
        layer: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createMText"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createMText", {
            text: args.text,
            insertionX: args.insertionX,
            insertionY: args.insertionY,
            height: args.height ?? 1.0,
            layer: args.layer,
          })
        ),
    },
    offset_lines_to_boundary: {
      action: "offset_lines_to_boundary",
      inputSchema: z.object({
        action: z.literal("offset_lines_to_boundary"),
        sourceLayer: z.string(),
        boundaryLayer: z.string(),
        directionDegrees: z.number(),
        spacing: z.number().optional(),
        maxCopies: z.number().optional(),
      }),
      capabilities: ["create", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["offsetLinesToBoundary"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("offsetLinesToBoundary", {
            sourceLayer: args.sourceLayer,
            boundaryLayer: args.boundaryLayer,
            directionDegrees: args.directionDegrees,
            spacing: args.spacing,
            maxCopies: args.maxCopies,
          })
        ),
    },
    cogo_inverse: {
      action: "cogo_inverse",
      inputSchema: z.object({
        action: z.literal("cogo_inverse"),
        x1: z.number(),
        y1: z.number(),
        x2: z.number(),
        y2: z.number(),
      }),
      capabilities: ["analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      pluginMethods: ["cogoInverse"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("cogoInverse", { x1: args.x1, y1: args.y1, x2: args.x2, y2: args.y2 })
        ),
    },
    cogo_direction_distance: {
      action: "cogo_direction_distance",
      inputSchema: z.object({
        action: z.literal("cogo_direction_distance"),
        fromX: z.number(),
        fromY: z.number(),
        fromZ: z.number().optional(),
        bearingDegrees: z.number(),
        distance: z.number(),
        slope: z.number().optional(),
      }),
      capabilities: ["analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      pluginMethods: ["cogoDirectionDistance"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("cogoDirectionDistance", {
            fromX: args.fromX,
            fromY: args.fromY,
            fromZ: args.fromZ,
            bearingDegrees: args.bearingDegrees,
            distance: args.distance,
            slope: args.slope,
          })
        ),
    },
    cogo_traverse: {
      action: "cogo_traverse",
      inputSchema: z.object({
        action: z.literal("cogo_traverse"),
        startX: z.number(),
        startY: z.number(),
        startZ: z.number().optional(),
        courses: z.array(CogoCourseSchema).min(1),
        isClosed: z.boolean().optional(),
      }),
      capabilities: ["analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      pluginMethods: ["cogoTraverse"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("cogoTraverse", {
            startX: args.startX,
            startY: args.startY,
            startZ: args.startZ,
            courses: args.courses,
            isClosed: args.isClosed ?? false,
          })
        ),
    },
    cogo_curve_solve: {
      action: "cogo_curve_solve",
      inputSchema: z.object({
        action: z.literal("cogo_curve_solve"),
        radius: z.number().optional(),
        deltaDegrees: z.number().optional(),
        length: z.number().optional(),
        tangent: z.number().optional(),
        chord: z.number().optional(),
      }),
      capabilities: ["analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      pluginMethods: ["cogoCurveSolve"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("cogoCurveSolve", {
            radius: args.radius,
            deltaDegrees: args.deltaDegrees,
            length: args.length,
            tangent: args.tangent,
            chord: args.chord,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_geometry",
      displayName: "Civil 3D Geometry",
      description:
        "Create basic AutoCAD geometry in Civil 3D. Actions: create_line (from start/end XYZ), " +
        "create_polyline (from 2D vertices), create_3d_polyline (from 3D vertices), " +
        "create_text, create_mtext, offset_lines_to_boundary (repeatedly copy/trim lines " +
        "against a closed boundary polyline at regular intervals). Also includes pure COGO " +
        "(coordinate geometry) math that needs no active drawing: cogo_inverse (bearing+distance " +
        "between two points), cogo_direction_distance (project a point from bearing+distance), " +
        "cogo_traverse (solve a chain of bearing/distance courses, with closure error if " +
        "isClosed), cogo_curve_solve (solve a horizontal curve from any two of " +
        "radius/deltaDegrees/length/tangent/chord).",
      inputShape: canonicalInputShape,
      supportedActions: [
        "create_line",
        "create_polyline",
        "create_3d_polyline",
        "create_text",
        "create_mtext",
        "offset_lines_to_boundary",
        "cogo_inverse",
        "cogo_direction_distance",
        "cogo_traverse",
        "cogo_curve_solve",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
