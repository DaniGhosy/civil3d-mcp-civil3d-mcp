import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const GenericActionSchema = z.enum([
  "get_properties",
  "list_by_type",
  "resolve_location",
  "set_style_name",
  "delete_entity",
  "ensure_layer",
  "list_layers",
  "set_layer",
  "move_entity",
  "copy_entity",
  "rotate_entity",
  "get_entity_bounds",
]);

const LocationModeSchema = z.enum(["coordinates", "pick", "reference_object"]);

const canonicalInputShape = {
  action: GenericActionSchema.describe("The generic introspection operation to perform."),
  handle: z.string().optional().describe("AutoCAD entity handle (hex string), for get_properties."),
  objectType: z
    .string()
    .optional()
    .describe(
      "CLR type name of the object to find (e.g. 'Surface', 'Corridor', 'Parcel', 'Assembly', " +
        "'Line', 'Polyline'). Matches the object's own type or any of its base types, for list_by_type."
    ),
  layer: z.string().optional().describe("Optional layer filter, for list_by_type."),
  limit: z.number().optional().describe("Maximum number of matches to return, for list_by_type (default 200)."),
  mode: LocationModeSchema.optional().describe("Location resolution mode, for resolve_location."),
  x: z.number().optional().describe("X coordinate (mode=coordinates)."),
  y: z.number().optional().describe("Y coordinate (mode=coordinates)."),
  z: z.number().optional().describe("Z coordinate, optional (mode=coordinates, default 0)."),
  promptMessage: z.string().optional().describe("Message shown to the user when picking a point (mode=pick)."),
  referenceHandle: z.string().optional().describe("Handle of the reference object (mode=reference_object)."),
  offsetX: z.number().optional().describe("X offset from the reference object's position (mode=reference_object)."),
  offsetY: z.number().optional().describe("Y offset from the reference object's position (mode=reference_object)."),
  offsetZ: z.number().optional().describe("Z offset from the reference object's position (mode=reference_object)."),
  styleName: z.string().optional().describe("Style name to assign, for set_style_name."),
  layerName: z
    .string()
    .optional()
    .describe("Layer name, for ensure_layer (created if missing) and set_layer (must already exist)."),
  colorIndex: z.number().optional().describe("ACI color index (1-255), optional, for ensure_layer."),
  dx: z.number().optional().describe("X displacement, for move_entity/copy_entity."),
  dy: z.number().optional().describe("Y displacement, for move_entity/copy_entity."),
  dz: z.number().optional().describe("Z displacement, optional (default 0), for move_entity/copy_entity."),
  basePointX: z.number().optional().describe("Rotation base point X, for rotate_entity."),
  basePointY: z.number().optional().describe("Rotation base point Y, for rotate_entity."),
  angleDegrees: z.number().optional().describe("Rotation angle in degrees, for rotate_entity."),
};

export const GENERIC_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "generic",
  actions: {
    get_properties: {
      action: "get_properties",
      inputSchema: z.object({
        action: z.literal("get_properties"),
        handle: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getObjectProperties"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getObjectProperties", { handle: args.handle })
        ),
    },
    list_by_type: {
      action: "list_by_type",
      inputSchema: z.object({
        action: z.literal("list_by_type"),
        objectType: z.string(),
        layer: z.string().optional(),
        limit: z.number().optional(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listObjectsByType"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listObjectsByType", {
            objectType: args.objectType,
            layer: args.layer,
            limit: args.limit,
          })
        ),
    },
    resolve_location: {
      action: "resolve_location",
      inputSchema: z.object({
        action: z.literal("resolve_location"),
        mode: LocationModeSchema,
        x: z.number().optional(),
        y: z.number().optional(),
        z: z.number().optional(),
        promptMessage: z.string().optional(),
        referenceHandle: z.string().optional(),
        offsetX: z.number().optional(),
        offsetY: z.number().optional(),
        offsetZ: z.number().optional(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["resolveLocation"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("resolveLocation", {
            mode: args.mode,
            x: args.x,
            y: args.y,
            z: args.z,
            promptMessage: args.promptMessage,
            referenceHandle: args.referenceHandle,
            offsetX: args.offsetX,
            offsetY: args.offsetY,
            offsetZ: args.offsetZ,
          })
        ),
    },
    set_style_name: {
      action: "set_style_name",
      inputSchema: z.object({
        action: z.literal("set_style_name"),
        handle: z.string(),
        styleName: z.string(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["setObjectStyle"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("setObjectStyle", {
            handle: args.handle,
            styleName: args.styleName,
          })
        ),
    },
    delete_entity: {
      action: "delete_entity",
      inputSchema: z.object({
        action: z.literal("delete_entity"),
        handle: z.string(),
      }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteEntity"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) => await c.sendCommand("deleteEntity", { handle: args.handle })),
    },
    ensure_layer: {
      action: "ensure_layer",
      inputSchema: z.object({
        action: z.literal("ensure_layer"),
        layerName: z.string(),
        colorIndex: z.number().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["ensureLayer"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("ensureLayer", { layerName: args.layerName, colorIndex: args.colorIndex })
        ),
    },
    list_layers: {
      action: "list_layers",
      inputSchema: z.object({ action: z.literal("list_layers") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listLayers"],
      execute: async () => await withApplicationConnection(async (c) => await c.sendCommand("listLayers", {})),
    },
    set_layer: {
      action: "set_layer",
      inputSchema: z.object({
        action: z.literal("set_layer"),
        handle: z.string(),
        layerName: z.string(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["setLayer"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("setLayer", { handle: args.handle, layerName: args.layerName })
        ),
    },
    move_entity: {
      action: "move_entity",
      inputSchema: z.object({
        action: z.literal("move_entity"),
        handle: z.string(),
        dx: z.number(),
        dy: z.number(),
        dz: z.number().optional(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["moveEntity"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("moveEntity", { handle: args.handle, dx: args.dx, dy: args.dy, dz: args.dz })
        ),
    },
    copy_entity: {
      action: "copy_entity",
      inputSchema: z.object({
        action: z.literal("copy_entity"),
        handle: z.string(),
        dx: z.number(),
        dy: z.number(),
        dz: z.number().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["copyEntity"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("copyEntity", { handle: args.handle, dx: args.dx, dy: args.dy, dz: args.dz })
        ),
    },
    rotate_entity: {
      action: "rotate_entity",
      inputSchema: z.object({
        action: z.literal("rotate_entity"),
        handle: z.string(),
        basePointX: z.number(),
        basePointY: z.number(),
        angleDegrees: z.number(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["rotateEntity"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("rotateEntity", {
            handle: args.handle,
            basePointX: args.basePointX,
            basePointY: args.basePointY,
            angleDegrees: args.angleDegrees,
          })
        ),
    },
    get_entity_bounds: {
      action: "get_entity_bounds",
      inputSchema: z.object({
        action: z.literal("get_entity_bounds"),
        handle: z.string(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getEntityBounds"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) => await c.sendCommand("getEntityBounds", { handle: args.handle })),
    },
  },
  exposures: [
    {
      toolName: "civil3d_object",
      displayName: "Civil 3D Generic Object Introspection",
      description:
        "Generic primitives that work on ANY AutoCAD/Civil 3D object type via reflection, " +
        "for cases not covered by a domain-specific tool. Actions: get_properties (by handle, " +
        "reads all simple public properties of the object, including StyleName where present), " +
        "list_by_type (finds objects in ModelSpace whose type — or a base type — matches " +
        "objectType, e.g. 'Surface', 'Corridor', 'Parcel', 'Assembly', 'Line', 'Curve', or a " +
        "label type like 'PipeLabel'), resolve_location (resolves an X,Y,Z point from " +
        "mode='coordinates' (explicit x/y/z), mode='pick' (waits for the user to click a point " +
        "inside Civil 3D — blocks up to the configured command timeout), or " +
        "mode='reference_object' (position of an existing object by handle, plus optional " +
        "offsetX/Y/Z)), set_style_name (assigns a style by name to ANY object that exposes a " +
        "writable StyleName property — this is the generic style engine, and it works exactly " +
        "the same way on a main object like a Surface/Alignment/Parcel or on an already-created " +
        "label like a PipeLabel, so combined with list_by_type it also covers mass re-styling of " +
        "existing labels. It does not create new labels — label creation is type-specific per " +
        "domain). Combine resolve_location's resolved point with create_line/create_polyline/" +
        "create_3d_polyline (civil3d_geometry) or a COGO point create (civil3d_point) to place " +
        "any object anywhere. Note: get_properties on a Surface/TinSurface handle uses a curated " +
        "safe field set (Name, Handle, Layer, Type) instead of full reflection — confirmed in " +
        "live testing that open-ended reflection on a TinSurface can hang; other object types " +
        "still get full reflection. resolve_location(reference_object) explicitly rejects " +
        "Surface handles (no single meaningful anchor point) instead of silently returning a " +
        "meaningless offset. Entity-generic primitives (added post-Mes 9, for the test/cleanup " +
        "cycle): delete_entity (Entity.Erase), ensure_layer (create the layer if missing), " +
        "list_layers (name/colorIndex/isOff/isFrozen/isLocked — deliberately no 'in use' flag, " +
        "computing it would require scanning all ModelSpace entities per layer), set_layer " +
        "(errors if the layer doesn't exist — use ensure_layer first), move_entity/copy_entity " +
        "(displacement by dx/dy/dz), rotate_entity (about basePointX/Y by angleDegrees), " +
        "get_entity_bounds (GeometricExtents min/max, errors for entities with no displayable " +
        "geometry).",
      inputShape: canonicalInputShape,
      supportedActions: [
        "get_properties",
        "list_by_type",
        "resolve_location",
        "set_style_name",
        "delete_entity",
        "ensure_layer",
        "list_layers",
        "set_layer",
        "move_entity",
        "copy_entity",
        "rotate_entity",
        "get_entity_bounds",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
