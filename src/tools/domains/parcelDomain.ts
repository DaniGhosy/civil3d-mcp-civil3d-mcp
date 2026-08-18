import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const ParcelActionSchema = z.enum(["list_sites", "list", "get", "delete", "create", "edit", "lot_line_adjust", "report"]);

const canonicalInputShape = {
  action: ParcelActionSchema.describe("The parcel operation to perform."),
  siteName: z.string().optional().describe("Parcel site name (filter for list, required for create/edit/lot_line_adjust/report)."),
  name: z.string().optional().describe("Parcel name (for get/delete)."),
  boundaryLayer: z.string().optional().describe("Layer containing closed boundary entities to convert into parcels (for create)."),
  parcelName: z.string().optional().describe("Parcel name (edit/lot_line_adjust)."),
  newName: z.string().optional().describe("New parcel name (edit)."),
  style: z.string().optional().describe("Parcel style name (edit)."),
  areaLabelStyle: z.string().optional().describe("Area label style name (edit)."),
  description: z.string().optional().describe("Parcel description (edit)."),
  targetAreaSqFt: z.number().optional().describe("Target area for the lot line slide, in square feet (lot_line_adjust)."),
  lotLineHandle: z.string().optional().describe("Handle of the specific lot line to slide (lot_line_adjust)."),
  tolerance: z.number().optional().describe("Convergence tolerance for the lot line slide (lot_line_adjust)."),
  parcelNames: z.array(z.string()).optional().describe("Filter to specific parcel names (report; omit for all)."),
  outputPath: z.string().optional().describe("CSV output path (report)."),
  overwrite: z.boolean().optional().describe("Overwrite an existing output file (report)."),
  includeCoordinates: z.boolean().optional().describe("Include boundary vertex coordinates (report)."),
  units: z.enum(["sqft", "acres", "sqm", "ha"]).optional().describe("Area units for the report (report)."),
};

export const PARCEL_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "parcel",
  actions: {
    list_sites: {
      action: "list_sites",
      inputSchema: z.object({ action: z.literal("list_sites") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listParcelSites"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listParcelSites", {})
        ),
    },
    list: {
      action: "list",
      inputSchema: z.object({
        action: z.literal("list"),
        siteName: z.string().optional(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listParcels"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listParcels", { siteName: args.siteName })
        ),
    },
    get: {
      action: "get",
      inputSchema: z.object({ action: z.literal("get"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getParcel"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getParcel", { name: args.name })
        ),
    },
    delete: {
      action: "delete",
      inputSchema: z.object({ action: z.literal("delete"), name: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteParcel"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteParcel", { name: args.name })
        ),
    },
    create: {
      action: "create",
      inputSchema: z.object({
        action: z.literal("create"),
        siteName: z.string().optional(),
        boundaryLayer: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createParcel"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createParcel", {
            siteName: args.siteName,
            boundaryLayer: args.boundaryLayer,
          })
        ),
    },
    edit: {
      action: "edit",
      inputSchema: z.object({
        action: z.literal("edit"),
        siteName: z.string(),
        parcelName: z.string(),
        newName: z.string().optional(),
        style: z.string().optional(),
        areaLabelStyle: z.string().optional(),
        description: z.string().optional(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["editParcel"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("editParcel", {
            siteName: args.siteName,
            parcelName: args.parcelName,
            newName: args.newName,
            style: args.style,
            areaLabelStyle: args.areaLabelStyle,
            description: args.description,
          })
        ),
    },
    lot_line_adjust: {
      action: "lot_line_adjust",
      inputSchema: z.object({
        action: z.literal("lot_line_adjust"),
        siteName: z.string(),
        parcelName: z.string(),
        targetAreaSqFt: z.number(),
        lotLineHandle: z.string().optional(),
        tolerance: z.number().optional(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["adjustParcelLotLine"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("adjustParcelLotLine", {
            siteName: args.siteName,
            parcelName: args.parcelName,
            targetAreaSqFt: args.targetAreaSqFt,
            lotLineHandle: args.lotLineHandle,
            tolerance: args.tolerance ?? 1.0,
          })
        ),
    },
    report: {
      action: "report",
      inputSchema: z.object({
        action: z.literal("report"),
        siteName: z.string(),
        parcelNames: z.array(z.string()).optional(),
        outputPath: z.string().optional(),
        overwrite: z.boolean().optional(),
        includeCoordinates: z.boolean().optional(),
        units: z.enum(["sqft", "acres", "sqm", "ha"]).optional(),
      }),
      capabilities: ["query", "generate", "export"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["reportParcels"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("reportParcels", {
            siteName: args.siteName,
            parcelNames: args.parcelNames,
            outputPath: args.outputPath,
            overwrite: args.overwrite ?? false,
            includeCoordinates: args.includeCoordinates ?? false,
            units: args.units ?? "sqft",
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_parcel",
      displayName: "Civil 3D Parcel",
      description:
        "Manage Civil 3D parcels. Actions: list_sites (parcel sites in the drawing), " +
        "list (parcels, optionally filtered by siteName), get (by name), delete (by name), " +
        "edit (rename/restyle/describe), lot_line_adjust (slide a lot line toward a target " +
        "area), report (CSV export of area/perimeter/style, optionally with boundary " +
        "coordinates). Note: create is not yet implemented — it returns a 'planned' status " +
        "until the parcel layout workflow's exact factory method is confirmed against a live " +
        "Civil 3D drawing.",
      inputShape: canonicalInputShape,
      supportedActions: ["list_sites", "list", "get", "delete", "create", "edit", "lot_line_adjust", "report"],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
