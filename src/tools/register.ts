import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerDomainTools } from "./domainRuntime.js";
import { PLUGIN_DOMAIN_DEFINITION } from "./domains/pluginDomain.js";
import { DRAWING_DOMAIN_DEFINITION } from "./domains/drawingDomain.js";
import { SURFACE_DOMAIN_DEFINITION } from "./domains/surfaceDomain.js";
import { ALIGNMENT_DOMAIN_DEFINITION } from "./domains/alignmentDomain.js";
import { PROFILE_DOMAIN_DEFINITION } from "./domains/profileDomain.js";
import { PROFILE_VIEW_DOMAIN_DEFINITION } from "./domains/profileViewDomain.js";
import { CORRIDOR_DOMAIN_DEFINITION } from "./domains/corridorDomain.js";
import { SAMPLE_LINE_DOMAIN_DEFINITION } from "./domains/sampleLineDomain.js";
import { SHEET_PRODUCTION_DOMAIN_DEFINITION } from "./domains/sheetProductionDomain.js";
import { PIPE_DOMAIN_DEFINITION } from "./domains/pipeDomain.js";
import { PRESSURE_PIPE_DOMAIN_DEFINITION } from "./domains/pressurePipeDomain.js";
import { POINT_DOMAIN_DEFINITION } from "./domains/pointDomain.js";
import { GEOMETRY_DOMAIN_DEFINITION } from "./domains/geometryDomain.js";
import { PARCEL_DOMAIN_DEFINITION } from "./domains/parcelDomain.js";
import { ASSEMBLY_DOMAIN_DEFINITION } from "./domains/assemblyDomain.js";
import { GENERIC_DOMAIN_DEFINITION } from "./domains/genericDomain.js";
import { GRADING_DOMAIN_DEFINITION } from "./domains/gradingDomain.js";
import { DATA_SHORTCUT_DOMAIN_DEFINITION } from "./domains/dataShortcutDomain.js";
import { SURVEY_DOMAIN_DEFINITION } from "./domains/surveyDomain.js";
import { IMPORT_EXPORT_DOMAIN_DEFINITION } from "./domains/importExportDomain.js";
import { BLOCKS_DOMAIN_DEFINITION } from "./domains/blocksDomain.js";
import { QUANTITY_DOMAIN_DEFINITION } from "./domains/quantityDomain.js";
import { LABEL_DOMAIN_DEFINITION } from "./domains/labelDomain.js";
import { LEGEND_DOMAIN_DEFINITION } from "./domains/legendDomain.js";
import { SHAPE_DETECTION_DOMAIN_DEFINITION } from "./domains/shapeDetectionDomain.js";
import { PLAN_VISION_DOMAIN_DEFINITION } from "./domains/planVisionDomain.js";

/**
 * All domain definitions — order matters for tool discovery.
 * Plugin/health first, then read-heavy domains, then write-heavy.
 */
const DOMAIN_DEFINITIONS = [
  PLUGIN_DOMAIN_DEFINITION,
  GENERIC_DOMAIN_DEFINITION,
  DRAWING_DOMAIN_DEFINITION,
  SURFACE_DOMAIN_DEFINITION,
  ALIGNMENT_DOMAIN_DEFINITION,
  PROFILE_DOMAIN_DEFINITION,
  PROFILE_VIEW_DOMAIN_DEFINITION,
  CORRIDOR_DOMAIN_DEFINITION,
  SAMPLE_LINE_DOMAIN_DEFINITION,
  SHEET_PRODUCTION_DOMAIN_DEFINITION,
  PIPE_DOMAIN_DEFINITION,
  PRESSURE_PIPE_DOMAIN_DEFINITION,
  POINT_DOMAIN_DEFINITION,
  PARCEL_DOMAIN_DEFINITION,
  ASSEMBLY_DOMAIN_DEFINITION,
  GRADING_DOMAIN_DEFINITION,
  DATA_SHORTCUT_DOMAIN_DEFINITION,
  SURVEY_DOMAIN_DEFINITION,
  IMPORT_EXPORT_DOMAIN_DEFINITION,
  BLOCKS_DOMAIN_DEFINITION,
  LABEL_DOMAIN_DEFINITION,
  LEGEND_DOMAIN_DEFINITION,
  SHAPE_DETECTION_DOMAIN_DEFINITION,
  PLAN_VISION_DOMAIN_DEFINITION,
  QUANTITY_DOMAIN_DEFINITION,
  GEOMETRY_DOMAIN_DEFINITION,
];

export async function registerTools(server: McpServer) {
  for (const definition of DOMAIN_DEFINITIONS) {
    registerDomainTools(server, definition);
  }
}
