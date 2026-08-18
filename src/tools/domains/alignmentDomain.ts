import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const AlignmentActionSchema = z.enum([
  "list",
  "get",
  "create",
  "delete",
  "station_to_point",
  "point_to_station",
  "list_superelevation_curves",
  "list_superelevation_critical_stations",
  "list_design_speeds",
  "add_tangent",
  "add_curve",
  "add_spiral",
  "delete_entity",
  "set_station_equation",
  "get_station_offset",
  "offset_create",
  "widen_transition",
]);

const canonicalInputShape = {
  action: AlignmentActionSchema.describe("The alignment operation to perform."),
  name: z.string().optional().describe("Alignment name."),
  station: z.number().optional().describe("Station value along the alignment."),
  offset: z.number().optional().describe("Offset from the alignment."),
  x: z.number().optional().describe("X coordinate (for point_to_station)."),
  y: z.number().optional().describe("Y coordinate (for point_to_station)."),
  polylineHandle: z.string().optional().describe("Handle of an existing polyline to create alignment from."),
  style: z.string().optional().describe("Alignment style name."),
  layer: z.string().optional().describe("Layer name."),
  labelSet: z.string().optional().describe("Alignment label set style name (offset_create/widen_transition)."),
  startX: z.number().optional().describe("Tangent/spiral start X (add_tangent/add_spiral)."),
  startY: z.number().optional().describe("Tangent/spiral start Y (add_tangent/add_spiral)."),
  endX: z.number().optional().describe("Tangent end X (add_tangent)."),
  endY: z.number().optional().describe("Tangent end Y (add_tangent)."),
  passThroughX: z.number().optional().describe("Curve pass-through X (add_curve)."),
  passThroughY: z.number().optional().describe("Curve pass-through Y (add_curve)."),
  radius: z.number().positive().optional().describe("Curve radius (add_curve)."),
  spiralType: z.enum(["clothoid", "cubic", "biquadratic"]).optional().describe("Spiral type (add_spiral)."),
  startRadius: z.number().optional().describe("Spiral start radius (add_spiral)."),
  endRadius: z.number().optional().describe("Spiral end radius (add_spiral)."),
  length: z.number().positive().optional().describe("Spiral length (add_spiral)."),
  entityIndex: z.number().int().min(0).optional().describe("Zero-based entity index (delete_entity)."),
  rawStation: z.number().optional().describe("Raw (measured) station (set_station_equation)."),
  nominalStation: z.number().optional().describe("Nominal station after the equation (set_station_equation)."),
  offsetName: z.string().optional().describe("Name for the new offset alignment (offset_create/widen_transition)."),
  side: z.enum(["left", "right"]).optional().describe("Widening side (widen_transition)."),
  startStation: z.number().optional().describe("Widening transition start station (widen_transition)."),
  endStation: z.number().optional().describe("Widening transition end station (widen_transition)."),
  startOffset: z.number().optional().describe("Widening transition start offset (widen_transition)."),
  endOffset: z.number().optional().describe("Widening transition end offset (widen_transition)."),
};

export const ALIGNMENT_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "alignment",
  actions: {
    list: {
      action: "list",
      inputSchema: z.object({ action: z.literal("list") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listAlignments"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listAlignments", {})
        ),
    },
    get: {
      action: "get",
      inputSchema: z.object({ action: z.literal("get"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getAlignment"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getAlignment", { name: args.name })
        ),
    },
    create: {
      action: "create",
      inputSchema: z.object({
        action: z.literal("create"),
        name: z.string(),
        polylineHandle: z.string().optional(),
        style: z.string().optional(),
        layer: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createAlignment"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createAlignment", {
            name: args.name,
            polylineHandle: args.polylineHandle,
            style: args.style,
            layer: args.layer,
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
      pluginMethods: ["deleteAlignment"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteAlignment", { name: args.name })
        ),
    },
    station_to_point: {
      action: "station_to_point",
      inputSchema: z.object({
        action: z.literal("station_to_point"),
        name: z.string(),
        station: z.number(),
        offset: z.number().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["alignmentStationToPoint"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("alignmentStationToPoint", {
            name: args.name,
            station: args.station,
            offset: args.offset ?? 0,
          })
        ),
    },
    point_to_station: {
      action: "point_to_station",
      inputSchema: z.object({
        action: z.literal("point_to_station"),
        name: z.string(),
        x: z.number(),
        y: z.number(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["alignmentPointToStation"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("alignmentPointToStation", {
            name: args.name,
            x: args.x,
            y: args.y,
          })
        ),
    },
    list_superelevation_curves: {
      action: "list_superelevation_curves",
      inputSchema: z.object({ action: z.literal("list_superelevation_curves"), name: z.string() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSuperelevationCurves"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSuperelevationCurves", { name: args.name })
        ),
    },
    list_superelevation_critical_stations: {
      action: "list_superelevation_critical_stations",
      inputSchema: z.object({ action: z.literal("list_superelevation_critical_stations"), name: z.string() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSuperelevationCriticalStations"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSuperelevationCriticalStations", { name: args.name })
        ),
    },
    list_design_speeds: {
      action: "list_design_speeds",
      inputSchema: z.object({ action: z.literal("list_design_speeds"), name: z.string() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listDesignSpeeds"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listDesignSpeeds", { name: args.name })
        ),
    },
    add_tangent: {
      action: "add_tangent",
      inputSchema: z.object({
        action: z.literal("add_tangent"),
        name: z.string(),
        startX: z.number(),
        startY: z.number(),
        endX: z.number(),
        endY: z.number(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["alignmentAddTangent"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("alignmentAddTangent", {
            alignmentName: args.name,
            startX: args.startX,
            startY: args.startY,
            endX: args.endX,
            endY: args.endY,
          })
        ),
    },
    add_curve: {
      action: "add_curve",
      inputSchema: z.object({
        action: z.literal("add_curve"),
        name: z.string(),
        passThroughX: z.number(),
        passThroughY: z.number(),
        radius: z.number().positive(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["alignmentAddCurve"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("alignmentAddCurve", {
            alignmentName: args.name,
            passThroughX: args.passThroughX,
            passThroughY: args.passThroughY,
            radius: args.radius,
          })
        ),
    },
    add_spiral: {
      action: "add_spiral",
      inputSchema: z.object({
        action: z.literal("add_spiral"),
        name: z.string(),
        spiralType: z.enum(["clothoid", "cubic", "biquadratic"]).optional(),
        startX: z.number(),
        startY: z.number(),
        startRadius: z.number(),
        endRadius: z.number(),
        length: z.number().positive(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["alignmentAddSpiral"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("alignmentAddSpiral", {
            alignmentName: args.name,
            spiralType: args.spiralType ?? "clothoid",
            startX: args.startX,
            startY: args.startY,
            startRadius: args.startRadius,
            endRadius: args.endRadius,
            length: args.length,
          })
        ),
    },
    delete_entity: {
      action: "delete_entity",
      inputSchema: z.object({
        action: z.literal("delete_entity"),
        name: z.string(),
        entityIndex: z.number().int().min(0),
      }),
      capabilities: ["edit", "delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["alignmentDeleteEntity"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("alignmentDeleteEntity", {
            alignmentName: args.name,
            entityIndex: args.entityIndex,
          })
        ),
    },
    set_station_equation: {
      action: "set_station_equation",
      inputSchema: z.object({
        action: z.literal("set_station_equation"),
        name: z.string(),
        rawStation: z.number(),
        nominalStation: z.number(),
      }),
      capabilities: ["edit", "manage"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["alignmentSetStationEquation"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("alignmentSetStationEquation", {
            alignmentName: args.name,
            rawStation: args.rawStation,
            nominalStation: args.nominalStation,
          })
        ),
    },
    get_station_offset: {
      action: "get_station_offset",
      inputSchema: z.object({
        action: z.literal("get_station_offset"),
        name: z.string(),
        x: z.number(),
        y: z.number(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["alignmentGetStationOffset"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("alignmentGetStationOffset", {
            alignmentName: args.name,
            x: args.x,
            y: args.y,
          })
        ),
    },
    offset_create: {
      action: "offset_create",
      inputSchema: z.object({
        action: z.literal("offset_create"),
        name: z.string(),
        offsetName: z.string(),
        offset: z.number(),
        style: z.string().optional(),
        layer: z.string().optional(),
        labelSet: z.string().optional(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["alignmentOffsetCreate"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("alignmentOffsetCreate", {
            alignmentName: args.name,
            offsetName: args.offsetName,
            offset: args.offset,
            style: args.style,
            layer: args.layer,
            labelSet: args.labelSet,
          })
        ),
    },
    widen_transition: {
      action: "widen_transition",
      inputSchema: z.object({
        action: z.literal("widen_transition"),
        name: z.string(),
        side: z.enum(["left", "right"]),
        startStation: z.number(),
        endStation: z.number(),
        startOffset: z.number(),
        endOffset: z.number(),
        offsetName: z.string().optional(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["alignmentWidenTransition"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("alignmentWidenTransition", {
            alignmentName: args.name,
            side: args.side,
            startStation: args.startStation,
            endStation: args.endStation,
            startOffset: args.startOffset,
            endOffset: args.endOffset,
            offsetName: args.offsetName,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_alignment",
      displayName: "Civil 3D Alignment",
      description:
        "Manage Civil 3D alignments (horizontal geometry). Actions: list, get (by name), " +
        "create (from polyline), delete, station_to_point (convert station+offset to X,Y), " +
        "point_to_station (convert X,Y to station+offset), list_superelevation_curves, " +
        "list_superelevation_critical_stations, list_design_speeds (each item's fields are " +
        "serialized generically via reflection since their exact property names weren't " +
        "verified against a live Civil 3D drawing), add_tangent, add_curve (returns a capability " +
        "error — Civil 3D's managed API needs an explicit direction not in this schema), " +
        "add_spiral (returns a capability error — the AddFixedSpiral overloads need a previous " +
        "entity id), delete_entity, set_station_equation, get_station_offset, offset_create " +
        "(constant-offset alignment), widen_transition (returns a capability error — Civil 3D " +
        "does not expose variable-offset/widening alignment creation in the managed API).",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list",
        "get",
        "create",
        "delete",
        "station_to_point",
        "point_to_station",
        "list_superelevation_curves",
        "list_superelevation_critical_stations",
        "list_design_speeds",
        "add_tangent",
        "add_curve",
        "add_spiral",
        "delete_entity",
        "set_station_equation",
        "get_station_offset",
        "offset_create",
        "widen_transition",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
