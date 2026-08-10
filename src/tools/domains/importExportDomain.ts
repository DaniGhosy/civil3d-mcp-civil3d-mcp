import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const ImportExportActionSchema = z.enum(["import_landxml", "export_surface_to_landxml", "export_to_shapefile"]);

const canonicalInputShape = {
  action: ImportExportActionSchema.describe("The import/export operation to perform."),
  filePath: z.string().optional().describe("Input or output file path."),
  surfaceName: z.string().optional().describe("Surface name (export_surface_to_landxml, export_to_shapefile)."),
};

export const IMPORT_EXPORT_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "project",
  actions: {
    import_landxml: {
      action: "import_landxml",
      inputSchema: z.object({ action: z.literal("import_landxml"), filePath: z.string() }),
      capabilities: ["import"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["importLandXml"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("importLandXml", { filePath: args.filePath })
        ),
    },
    export_surface_to_landxml: {
      action: "export_surface_to_landxml",
      inputSchema: z.object({
        action: z.literal("export_surface_to_landxml"),
        surfaceName: z.string(),
        filePath: z.string(),
      }),
      capabilities: ["export"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["exportSurfaceToLandXml"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("exportSurfaceToLandXml", {
            surfaceName: args.surfaceName,
            filePath: args.filePath,
          })
        ),
    },
    export_to_shapefile: {
      action: "export_to_shapefile",
      inputSchema: z.object({
        action: z.literal("export_to_shapefile"),
        surfaceName: z.string(),
        filePath: z.string(),
      }),
      capabilities: ["export"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["exportToShapefile"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("exportToShapefile", {
            surfaceName: args.surfaceName,
            filePath: args.filePath,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_import_export",
      displayName: "Civil 3D LandXML / GIS Import-Export",
      description:
        "Import/export Civil 3D data via LandXML or GIS formats. None of these are implemented " +
        "yet — all three return a 'planned' status. import_landxml: confirmed NOT possible via " +
        "the .NET API at all (Autodesk's own docs — COM-only or UI). export_surface_to_landxml: " +
        "no confirmed .NET export entry-point found (only a tangential settings type). " +
        "export_to_shapefile: needs AutoCAD Map 3D's core managed assembly, not clearly " +
        "identified among the satellite AcMap*Mgd.dll files already available. For plain-text " +
        "point import/export (not LandXML), use civil3d_point's import/export actions instead — " +
        "those are real.",
      inputShape: canonicalInputShape,
      supportedActions: ["import_landxml", "export_surface_to_landxml", "export_to_shapefile"],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
