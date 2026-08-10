import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const Point2DSchema = z.object({ x: z.number(), y: z.number() });
const Point3DSchema = z.object({ x: z.number(), y: z.number(), z: z.number() });

const SurfaceActionSchema = z.enum([
  "list",
  "get",
  "get_elevation",
  "get_statistics",
  "create",
  "delete",
  "add_points",
  "add_breakline",
  "add_boundary",
  "extract_contours",
  "compute_volume",
  "get_area_elevation_table",
  "compute_contour_volume",
  "close_contours_against_boundary",
  "delete_points",
  "list_triangles",
  "get_triangle_at_point",
  "paste_surface",
  "get_operations",
  "delete_boundary",
  "get_build_options",
  "set_build_options",
  "add_contour_data",
  "swap_edge",
  "minimize_flat_triangles",
  "minimize_convex_triangles",
  "delete_breakline",
  "delete_operation",
]);

const canonicalInputShape = {
  action: SurfaceActionSchema.describe("The surface operation to perform."),
  name: z.string().optional().describe("Surface name."),
  x: z.number().optional().describe("X coordinate (for get_elevation)."),
  y: z.number().optional().describe("Y coordinate (for get_elevation)."),
  points: z.array(Point3DSchema).optional().describe("Array of 3D points."),
  style: z.string().optional().describe("Surface style name."),
  layer: z.string().optional().describe("Layer name."),
  description: z.string().optional().describe("Description text."),
  breaklineType: z.enum(["standard", "wall", "proximity"]).optional(),
  boundaryType: z.enum(["show", "hide", "outer", "data_clip"]).optional(),
  boundaryPoints: z.array(Point2DSchema).optional().describe("Boundary polygon points."),
  minorInterval: z.number().optional().describe("Minor contour interval."),
  majorInterval: z.number().optional().describe("Major contour interval."),
  baseSurface: z.string().optional().describe("Base surface for volume calculation."),
  comparisonSurface: z.string().optional().describe("Comparison surface for volume calculation."),
  interval: z.number().optional().describe("Elevation/area sampling interval."),
  layerName: z.string().optional().describe("Layer containing contour polylines."),
  minElevation: z.number().optional().describe("Minimum elevation filter."),
  maxElevation: z.number().optional().describe("Maximum elevation filter."),
  outputCsvPath: z.string().optional().describe("Optional CSV output path."),
  contourLayerName: z.string().optional().describe("Layer containing open contour polylines."),
  boundaryLayerName: z.string().optional().describe("Layer containing the boundary polyline."),
  snapTolerance: z.number().optional().describe("Distance tolerance to merge chain endpoints."),
  outputLayerName: z.string().optional().describe("Layer to draw the closed contours into."),
  forceMinElevation: z.number().optional().describe("Lower bound of elevation range to force a tramo choice."),
  forceMaxElevation: z.number().optional().describe("Upper bound of elevation range to force a tramo choice."),
  forceTramo: z.enum(["tramo_corto", "tramo_largo"]).optional().describe("Force short or long boundary arc within the forced elevation range."),
  closeMethod: z.enum(["boundary", "straight", "fixedWindow"]).optional().describe("How to close open contour chains."),
  fixedPoint1X: z.number().optional().describe("Fixed window: first point X (closeMethod=fixedWindow)."),
  fixedPoint1Y: z.number().optional().describe("Fixed window: first point Y (closeMethod=fixedWindow)."),
  fixedPoint2X: z.number().optional().describe("Fixed window: second point X (closeMethod=fixedWindow)."),
  fixedPoint2Y: z.number().optional().describe("Fixed window: second point Y (closeMethod=fixedWindow)."),
  tolerance: z.number().optional().describe("XY matching tolerance for delete_points (default 0.01)."),
  pasteSurfaceName: z.string().optional().describe("Name of the surface to paste into this one (paste_surface)."),
  minimumTriangleArea: z.number().optional().describe("Minimum triangle area build option (set_build_options)."),
};

export const SURFACE_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "surface",
  actions: {
    list: {
      action: "list",
      inputSchema: z.object({ action: z.literal("list") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSurfaces"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSurfaces", {})
        ),
    },
    get: {
      action: "get",
      inputSchema: z.object({ action: z.literal("get"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSurface"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getSurface", { name: args.name })
        ),
    },
    get_elevation: {
      action: "get_elevation",
      inputSchema: z.object({
        action: z.literal("get_elevation"),
        name: z.string(),
        x: z.number(),
        y: z.number(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSurfaceElevation"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getSurfaceElevation", {
            name: args.name,
            x: args.x,
            y: args.y,
          })
        ),
    },
    get_statistics: {
      action: "get_statistics",
      inputSchema: z.object({
        action: z.literal("get_statistics"),
        name: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSurfaceStatistics"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getSurfaceStatistics", { name: args.name })
        ),
    },
    create: {
      action: "create",
      inputSchema: z.object({
        action: z.literal("create"),
        name: z.string(),
        style: z.string().optional(),
        layer: z.string().optional(),
        description: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createSurface"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createSurface", {
            name: args.name,
            style: args.style,
            layer: args.layer,
            description: args.description,
          })
        ),
    },
    delete: {
      action: "delete",
      inputSchema: z.object({
        action: z.literal("delete"),
        name: z.string(),
      }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteSurface"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteSurface", { name: args.name })
        ),
    },
    add_points: {
      action: "add_points",
      inputSchema: z.object({
        action: z.literal("add_points"),
        name: z.string(),
        points: z.array(Point3DSchema),
        description: z.string().optional(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addSurfacePoints"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addSurfacePoints", {
            name: args.name,
            points: args.points,
            description: args.description,
          })
        ),
    },
    add_breakline: {
      action: "add_breakline",
      inputSchema: z.object({
        action: z.literal("add_breakline"),
        name: z.string(),
        breaklineType: z.enum(["standard", "wall", "proximity"]),
        points: z.array(Point3DSchema),
        description: z.string().optional(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addSurfaceBreakline"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addSurfaceBreakline", {
            name: args.name,
            breaklineType: args.breaklineType,
            points: args.points,
            description: args.description,
          })
        ),
    },
    add_boundary: {
      action: "add_boundary",
      inputSchema: z.object({
        action: z.literal("add_boundary"),
        name: z.string(),
        boundaryType: z.enum(["show", "hide", "outer", "data_clip"]),
        boundaryPoints: z.array(Point2DSchema),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addSurfaceBoundary"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addSurfaceBoundary", {
            name: args.name,
            boundaryType: args.boundaryType,
            points: args.boundaryPoints,
          })
        ),
    },
    extract_contours: {
      action: "extract_contours",
      inputSchema: z.object({
        action: z.literal("extract_contours"),
        name: z.string(),
        minorInterval: z.number(),
        majorInterval: z.number(),
      }),
      capabilities: ["generate"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["extractSurfaceContours"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("extractSurfaceContours", {
            name: args.name,
            minorInterval: args.minorInterval,
            majorInterval: args.majorInterval,
          })
        ),
    },
    compute_volume: {
      action: "compute_volume",
      inputSchema: z.object({
        action: z.literal("compute_volume"),
        baseSurface: z.string(),
        comparisonSurface: z.string(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["computeSurfaceVolume"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("computeSurfaceVolume", {
            baseSurface: args.baseSurface,
            comparisonSurface: args.comparisonSurface,
          })
        ),
    },
    get_area_elevation_table: {
      action: "get_area_elevation_table",
      inputSchema: z.object({
        action: z.literal("get_area_elevation_table"),
        name: z.string(),
        interval: z.number().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSurfaceAreaElevationTable"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getSurfaceAreaElevationTable", {
            name: args.name,
            interval: args.interval,
          })
        ),
    },
    compute_contour_volume: {
      action: "compute_contour_volume",
      inputSchema: z.object({
        action: z.literal("compute_contour_volume"),
        layerName: z.string(),
        minElevation: z.number().optional(),
        maxElevation: z.number().optional(),
        interval: z.number().optional(),
        outputCsvPath: z.string().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["computeContourVolume"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("computeContourVolume", {
            layerName: args.layerName,
            minElevation: args.minElevation,
            maxElevation: args.maxElevation,
            interval: args.interval,
            outputCsvPath: args.outputCsvPath,
          })
        ),
    },
    close_contours_against_boundary: {
      action: "close_contours_against_boundary",
      inputSchema: z.object({
        action: z.literal("close_contours_against_boundary"),
        contourLayerName: z.string(),
        boundaryLayerName: z.string(),
        minElevation: z.number().optional(),
        maxElevation: z.number().optional(),
        snapTolerance: z.number().optional(),
        outputCsvPath: z.string().optional(),
        outputLayerName: z.string().optional(),
        forceMinElevation: z.number().optional(),
        forceMaxElevation: z.number().optional(),
        forceTramo: z.enum(["tramo_corto", "tramo_largo"]).optional(),
        closeMethod: z.enum(["boundary", "straight", "fixedWindow"]).optional(),
        fixedPoint1X: z.number().optional(),
        fixedPoint1Y: z.number().optional(),
        fixedPoint2X: z.number().optional(),
        fixedPoint2Y: z.number().optional(),
      }),
      capabilities: ["edit", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["closeContoursAgainstBoundary"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("closeContoursAgainstBoundary", {
            contourLayerName: args.contourLayerName,
            boundaryLayerName: args.boundaryLayerName,
            minElevation: args.minElevation,
            maxElevation: args.maxElevation,
            snapTolerance: args.snapTolerance,
            outputCsvPath: args.outputCsvPath,
            outputLayerName: args.outputLayerName,
            forceMinElevation: args.forceMinElevation,
            forceMaxElevation: args.forceMaxElevation,
            forceTramo: args.forceTramo,
            closeMethod: args.closeMethod,
            fixedPoint1X: args.fixedPoint1X,
            fixedPoint1Y: args.fixedPoint1Y,
            fixedPoint2X: args.fixedPoint2X,
            fixedPoint2Y: args.fixedPoint2Y,
          })
        ),
    },
    delete_points: {
      action: "delete_points",
      inputSchema: z.object({
        action: z.literal("delete_points"),
        name: z.string(),
        points: z.array(Point2DSchema).optional(),
        tolerance: z.number().optional(),
      }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteSurfacePoints"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteSurfacePoints", {
            name: args.name,
            points: args.points,
            tolerance: args.tolerance,
          })
        ),
    },
    list_triangles: {
      action: "list_triangles",
      inputSchema: z.object({
        action: z.literal("list_triangles"),
        name: z.string(),
        limit: z.number().optional(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSurfaceTriangles"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSurfaceTriangles", { name: args.name, limit: args.limit })
        ),
    },
    get_triangle_at_point: {
      action: "get_triangle_at_point",
      inputSchema: z.object({
        action: z.literal("get_triangle_at_point"),
        name: z.string(),
        x: z.number(),
        y: z.number(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSurfaceTriangleAtPoint"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getSurfaceTriangleAtPoint", { name: args.name, x: args.x, y: args.y })
        ),
    },
    paste_surface: {
      action: "paste_surface",
      inputSchema: z.object({
        action: z.literal("paste_surface"),
        name: z.string(),
        pasteSurfaceName: z.string(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["pasteSurface"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("pasteSurface", { name: args.name, pasteSurfaceName: args.pasteSurfaceName })
        ),
    },
    get_operations: {
      action: "get_operations",
      inputSchema: z.object({ action: z.literal("get_operations"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSurfaceOperations"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getSurfaceOperations", { name: args.name })
        ),
    },
    delete_boundary: {
      action: "delete_boundary",
      inputSchema: z.object({ action: z.literal("delete_boundary"), name: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteSurfaceBoundary"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteSurfaceBoundary", { name: args.name })
        ),
    },
    get_build_options: {
      action: "get_build_options",
      inputSchema: z.object({ action: z.literal("get_build_options"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSurfaceBuildOptions"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getSurfaceBuildOptions", { name: args.name })
        ),
    },
    set_build_options: {
      action: "set_build_options",
      inputSchema: z.object({
        action: z.literal("set_build_options"),
        name: z.string(),
        minimumTriangleArea: z.number().optional(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["setSurfaceBuildOptions"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("setSurfaceBuildOptions", {
            name: args.name,
            minimumTriangleArea: args.minimumTriangleArea,
          })
        ),
    },
    add_contour_data: {
      action: "add_contour_data",
      inputSchema: z.object({ action: z.literal("add_contour_data"), name: z.string() }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["addSurfaceContourData"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("addSurfaceContourData", { name: args.name })
        ),
    },
    swap_edge: {
      action: "swap_edge",
      inputSchema: z.object({ action: z.literal("swap_edge"), name: z.string() }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["swapSurfaceEdge"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("swapSurfaceEdge", { name: args.name })
        ),
    },
    minimize_flat_triangles: {
      action: "minimize_flat_triangles",
      inputSchema: z.object({ action: z.literal("minimize_flat_triangles"), name: z.string() }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["minimizeFlatTriangles"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("minimizeFlatTriangles", { name: args.name })
        ),
    },
    minimize_convex_triangles: {
      action: "minimize_convex_triangles",
      inputSchema: z.object({ action: z.literal("minimize_convex_triangles"), name: z.string() }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["minimizeConvexTriangles"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("minimizeConvexTriangles", { name: args.name })
        ),
    },
    delete_breakline: {
      action: "delete_breakline",
      inputSchema: z.object({ action: z.literal("delete_breakline"), name: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteSurfaceBreakline"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteSurfaceBreakline", { name: args.name })
        ),
    },
    delete_operation: {
      action: "delete_operation",
      inputSchema: z.object({ action: z.literal("delete_operation"), name: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteSurfaceOperation"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteSurfaceOperation", { name: args.name })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_surface",
      displayName: "Civil 3D Surface",
      description:
        "Manage Civil 3D surfaces. Actions: list, get (by name), get_elevation (at X,Y), " +
        "get_statistics, create, delete, add_points, add_breakline (real only for " +
        "breaklineType='standard' — 'wall'/'proximity' return 'planned', their real .NET " +
        "signatures are unconfirmed), add_boundary (real, all 4 boundaryType values), " +
        "compute_volume (between two surfaces), " +
        "get_area_elevation_table (cumulative area-below-elevation from a TIN), " +
        "compute_contour_volume (volume from drawn contour polylines on a layer), " +
        "close_contours_against_boundary (close open contour chains against a boundary polyline; " +
        "picks the tramo that yields a non-self-intersecting polygon first, falling back to " +
        "minimum area only when both or neither candidate is simple — handles irregular/hairpin " +
        "boundaries automatically, forceTramo/fixedWindow/straight remain as manual overrides), " +
        "delete_points (remove TIN vertices, all or filtered by XY), list_triangles, " +
        "get_triangle_at_point (direct TIN mesh access), paste_surface, get_build_options " +
        "(reads the surface's real build settings generically via reflection). Note: " +
        "extract_contours, get_operations, delete_boundary, set_build_options, add_contour_data, " +
        "swap_edge, minimize_flat_triangles, minimize_convex_triangles, delete_breakline, and " +
        "delete_operation are not yet implemented — either no real API member was found in " +
        "this codebase's research, or the compiler confirmed the guessed member name/signature " +
        "doesn't exist (extract_contours: TinSurface.ExtractMinorContours/ExtractMajorContours " +
        "take a SurfaceExtractionSettingsType + ContourSmoothingType + smoothFactor, not a raw " +
        "interval/layer pair — real usage of that settings object is unresearched; see " +
        "get_build_options to discover the real build-options property names first). " +
        "They return a 'planned' status until confirmed against a live Civil 3D drawing. There " +
        "is also no API-level toggle for automatic vs manual surface rebuild (confirmed absent " +
        "— only Corridor exposes that).",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list",
        "get",
        "get_elevation",
        "get_statistics",
        "create",
        "delete",
        "add_points",
        "add_breakline",
        "add_boundary",
        "extract_contours",
        "compute_volume",
        "get_area_elevation_table",
        "compute_contour_volume",
        "close_contours_against_boundary",
        "delete_points",
        "list_triangles",
        "get_triangle_at_point",
        "paste_surface",
        "get_operations",
        "delete_boundary",
        "get_build_options",
        "set_build_options",
        "add_contour_data",
        "swap_edge",
        "minimize_flat_triangles",
        "minimize_convex_triangles",
        "delete_breakline",
        "delete_operation",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
