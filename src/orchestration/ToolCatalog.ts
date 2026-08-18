import { z } from "zod";
import { DOMAIN_DEFINITIONS } from "../tools/register.js";
import type { ToolDomain } from "../tools/toolMetadata.js";

export type OrchestratorIntent = string;

export interface ToolCatalogEntry {
  intent: OrchestratorIntent;
  title: string;
  domain: ToolDomain;
  toolName: string;
  action: string;
  keywords: string[];
  requiredFields: string[];
  description: string;
  buildToolArgs: (params: Record<string, unknown>) => Record<string, unknown>;
  source?: "explicit" | "derived";
}

function pickDefined(params: Record<string, unknown>, fields: string[]): Record<string, unknown> {
  return Object.fromEntries(fields.filter((field) => params[field] !== undefined).map((field) => [field, params[field]]));
}

function buildActionArgs(action: string, fields: string[] = [], defaults: Record<string, unknown> = {}) {
  return (params: Record<string, unknown>) => ({
    action,
    ...defaults,
    ...pickDefined(params, fields),
  });
}

/**
 * Hand-curated, high-value intents against this repo's REAL tool/action names — every domain
 * here uses the "one tool, action enum" convention (see CLAUDE.md), unlike source where most of
 * these were separate single-purpose tools (civil3d_surface_volume_calculate,
 * civil3d_workflow_project_startup, etc.). That's why every buildToolArgs below sets an explicit
 * `action` field — there is no bare "execute" tool to call.
 */
const EXPLICIT_TOOL_CATALOG: ToolCatalogEntry[] = [
  {
    intent: "drawing_info",
    title: "Drawing information",
    domain: "drawing",
    toolName: "civil3d_drawing",
    action: "info",
    keywords: ["drawing", "drawing info", "drawing information", "project info", "what is in this drawing"],
    requiredFields: [],
    description: "Gets document-level information for the active drawing.",
    buildToolArgs: buildActionArgs("info"),
  },
  {
    intent: "list_surfaces",
    title: "List surfaces",
    domain: "surface",
    toolName: "civil3d_surface",
    action: "list",
    keywords: ["list surfaces", "show surfaces", "what surfaces", "surface list", "surfaces in drawing"],
    requiredFields: [],
    description: "Lists Civil 3D surfaces in the active drawing.",
    buildToolArgs: buildActionArgs("list"),
  },
  {
    intent: "get_surface",
    title: "Get surface details",
    domain: "surface",
    toolName: "civil3d_surface",
    action: "get",
    keywords: ["get surface", "show surface", "surface details", "surface info", "inspect surface"],
    requiredFields: ["name"],
    description: "Gets details for a named Civil 3D surface.",
    buildToolArgs: buildActionArgs("get", ["name"]),
  },
  {
    intent: "calculate_surface_volume",
    title: "Calculate surface volume",
    domain: "surface",
    toolName: "civil3d_surface",
    action: "volume_calculate",
    keywords: ["surface volume", "cut fill", "cut/fill", "compare surfaces", "surface volume between"],
    requiredFields: ["baseSurface", "comparisonSurface"],
    description: "Calculates cut/fill volume between two named Civil 3D surfaces.",
    buildToolArgs: buildActionArgs("volume_calculate", ["baseSurface", "comparisonSurface"]),
  },
  {
    intent: "generate_surface_volume_report",
    title: "Generate surface volume report",
    domain: "surface",
    toolName: "civil3d_surface",
    action: "volume_report",
    keywords: ["surface volume report", "volume report", "cut fill report", "surface comparison report"],
    requiredFields: ["baseSurface", "comparisonSurface"],
    description: "Generates a formatted cut/fill volume report comparing two named Civil 3D surfaces.",
    buildToolArgs: buildActionArgs("volume_report", ["baseSurface", "comparisonSurface"]),
  },
  {
    intent: "list_alignments",
    title: "List alignments",
    domain: "alignment",
    toolName: "civil3d_alignment",
    action: "list",
    keywords: ["list alignments", "show alignments", "what alignments", "alignment list", "alignments in drawing"],
    requiredFields: [],
    description: "Lists Civil 3D alignments in the active drawing.",
    buildToolArgs: buildActionArgs("list"),
  },
  {
    intent: "get_alignment",
    title: "Get alignment details",
    domain: "alignment",
    toolName: "civil3d_alignment",
    action: "get",
    keywords: ["get alignment", "show alignment", "alignment details", "alignment info", "inspect alignment"],
    requiredFields: ["name"],
    description: "Gets details for a named Civil 3D alignment.",
    buildToolArgs: buildActionArgs("get", ["name"]),
  },
  {
    intent: "list_profiles",
    title: "List profiles for alignment",
    domain: "profile",
    toolName: "civil3d_profile",
    action: "list",
    keywords: ["list profiles", "show profiles", "what profiles", "profile list", "profiles for alignment"],
    requiredFields: ["alignmentName"],
    description: "Lists profiles associated with a named alignment.",
    buildToolArgs: buildActionArgs("list", ["alignmentName"]),
  },
  {
    intent: "list_corridors",
    title: "List corridors",
    domain: "corridor",
    toolName: "civil3d_corridor",
    action: "list",
    keywords: ["list corridors", "show corridors", "what corridors", "corridor list", "corridors in drawing"],
    requiredFields: [],
    description: "Lists Civil 3D corridors in the active drawing.",
    buildToolArgs: buildActionArgs("list"),
  },
  {
    intent: "get_corridor",
    title: "Get corridor details",
    domain: "corridor",
    toolName: "civil3d_corridor",
    action: "get",
    keywords: ["get corridor", "show corridor", "corridor details", "corridor info", "inspect corridor"],
    requiredFields: ["name"],
    description: "Gets details for a named Civil 3D corridor.",
    buildToolArgs: buildActionArgs("get", ["name"]),
  },
  {
    intent: "rebuild_corridor",
    title: "Rebuild corridor",
    domain: "corridor",
    toolName: "civil3d_corridor",
    action: "rebuild",
    keywords: ["rebuild corridor", "update corridor", "refresh corridor"],
    requiredFields: ["name"],
    description: "Rebuilds a named Civil 3D corridor.",
    buildToolArgs: buildActionArgs("rebuild", ["name"]),
  },
  {
    intent: "list_pipe_networks",
    title: "List pipe networks",
    domain: "pipe",
    toolName: "civil3d_pipe",
    action: "list_networks",
    keywords: ["list pipe networks", "show pipe networks", "what pipe networks", "pipe network list"],
    requiredFields: [],
    description: "Lists Civil 3D gravity pipe networks in the active drawing.",
    buildToolArgs: buildActionArgs("list_networks"),
  },
  {
    intent: "workflow_project_startup",
    title: "Project startup workflow",
    domain: "workflow",
    toolName: "civil3d_workflow",
    action: "project_startup",
    keywords: ["project startup", "start project", "startup drawing", "new project workflow", "drawing startup workflow"],
    requiredFields: [],
    description: "Checks plugin health, inspects drawing readiness, and optionally creates or saves a startup drawing.",
    buildToolArgs: buildActionArgs("project_startup", ["templatePath", "save"]),
  },
  {
    intent: "workflow_drawing_readiness_audit",
    title: "Drawing readiness audit",
    domain: "workflow",
    toolName: "civil3d_workflow",
    action: "drawing_readiness_audit",
    keywords: ["drawing readiness audit", "drawing audit", "readiness check", "audit drawing readiness", "check drawing readiness"],
    requiredFields: [],
    description: "Runs a readiness audit across plugin health, drawing state, selection, and drawing standards.",
    buildToolArgs: buildActionArgs("drawing_readiness_audit", ["layerPrefix", "limit"]),
  },
  {
    intent: "workflow_surface_comparison_report",
    title: "Surface comparison report workflow",
    domain: "workflow",
    toolName: "civil3d_workflow",
    action: "surface_comparison_report",
    keywords: ["surface comparison workflow", "surface comparison report workflow", "compare surfaces with report", "workflow compare surfaces"],
    requiredFields: ["baseSurface", "comparisonSurface"],
    description: "Runs the structured surface comparison workflow and then generates a formatted report.",
    buildToolArgs: buildActionArgs("surface_comparison_report", ["baseSurface", "comparisonSurface"]),
  },
  {
    intent: "workflow_corridor_qc_report",
    title: "Corridor QC report workflow",
    domain: "workflow",
    toolName: "civil3d_workflow",
    action: "corridor_qc_report",
    keywords: ["corridor qc", "corridor qc report", "qc corridor", "corridor quality check"],
    requiredFields: ["corridorName"],
    description: "Runs a corridor QC check and optionally generates a consolidated report file.",
    buildToolArgs: buildActionArgs("corridor_qc_report", ["corridorName", "outputPath"]),
  },
  {
    intent: "workflow_qc_fix_and_verify",
    title: "QC fix and verify workflow",
    domain: "workflow",
    toolName: "civil3d_workflow",
    action: "qc_fix_and_verify",
    keywords: ["qc fix and verify", "fix drawing standards", "audit and fix standards", "verify drawing standards"],
    requiredFields: [],
    description: "Audits drawing standards, applies fixes, and re-audits to verify compliance.",
    buildToolArgs: buildActionArgs("qc_fix_and_verify", ["layerPrefix"]),
  },
  {
    intent: "standards_lookup",
    title: "Look up Civil 3D CAD standards",
    domain: "standards",
    toolName: "civil3d_standards_lookup",
    action: "lookup",
    keywords: ["standards", "cad standards", "best practice", "template hierarchy", "style hierarchy", "naming convention"],
    requiredFields: [],
    description: "Looks up Civil 3D CAD-standards guidance by topic, tag, or free-text query.",
    buildToolArgs: buildActionArgs("lookup", ["query"]),
  },
];

const ROUTE_PARAM_FIELDS = [
  "name",
  "alignmentName",
  "corridorName",
  "profileName",
  "surfaceName",
  "networkName",
  "groupName",
  "featureLineName",
  "criteriaName",
  "side",
  "projectFolder",
  "shortcutName",
  "shortcutType",
  "templatePath",
  "save",
  "saveAs",
  "limit",
  "layerPrefix",
  "pipeName",
  "structureName",
  "fittingName",
  "partName",
  "partsList",
  "targetType",
  "targetName",
  "startPoint",
  "endPoint",
  "position",
  "baseSurface",
  "comparisonSurface",
  "style",
  "layer",
  "labelSet",
  "filePath",
  "outputPath",
  "query",
];

function humanizeToken(token: string): string {
  return token.replace(/^civil3d_/i, "").replace(/_/g, " ").replace(/\s+/g, " ").trim();
}

function titleCase(text: string): string {
  return text
    .split(/\s+/)
    .filter(Boolean)
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(" ");
}

function buildActionSynonyms(action: string): string[] {
  switch (action) {
    case "list":
      return ["list", "show", "what"];
    case "get":
      return ["get", "show", "inspect"];
    case "create":
      return ["create", "make", "add", "new"];
    case "delete":
      return ["delete", "remove"];
    case "rebuild":
      return ["rebuild", "update", "refresh"];
    case "report":
      return ["report", "summary"];
    case "export":
      return ["export"];
    case "import":
      return ["import"];
    case "analyze":
      return ["analyze", "analysis", "check"];
    default:
      return [humanizeToken(action)];
  }
}

/**
 * Introspects the action's real Zod input schema to find required fields — more accurate than
 * source's hand-maintained per-tool/per-operation maps (~60 entries in ToolCatalog.ts there), and
 * never goes stale as domains change. "action" itself is excluded since it's always required and
 * always supplied by buildToolArgs, not something the router needs to extract from free text.
 */
function inferRequiredFields(inputSchema: z.ZodTypeAny): string[] {
  if (!(inputSchema instanceof z.ZodObject)) return [];
  const shape = inputSchema.shape as Record<string, z.ZodTypeAny>;
  return Object.entries(shape)
    .filter(([key, fieldSchema]) => key !== "action" && !fieldSchema.isOptional())
    .map(([key]) => key);
}

function buildDerivedKeywords(toolName: string, domain: string, displayName: string, action: string): string[] {
  const keywordSet = new Set<string>();
  const humanToolName = humanizeToken(toolName);
  const cleanDisplayName = displayName.replace(/^Civil 3D\s+/i, "").trim();
  const humanAction = humanizeToken(action);

  keywordSet.add(toolName);
  keywordSet.add(humanToolName);
  keywordSet.add(displayName);
  keywordSet.add(cleanDisplayName);
  keywordSet.add(domain);
  keywordSet.add(action);
  keywordSet.add(humanAction);
  keywordSet.add(`${domain} ${humanAction}`);
  keywordSet.add(`${cleanDisplayName} ${humanAction}`);

  for (const synonym of buildActionSynonyms(action)) {
    keywordSet.add(`${synonym} ${domain}`);
    keywordSet.add(`${synonym} ${cleanDisplayName}`);
  }

  return [...keywordSet].filter((keyword) => keyword.trim().length > 0);
}

function buildDerivedToolArgs(action: string) {
  return (params: Record<string, unknown>) => ({ action, ...pickDefined(params, ROUTE_PARAM_FIELDS) });
}

function buildDerivedRouteEntries(): ToolCatalogEntry[] {
  const explicitKeys = new Set(EXPLICIT_TOOL_CATALOG.map((entry) => `${entry.toolName}::${entry.action}`));
  const derivedEntries: ToolCatalogEntry[] = [];

  for (const definition of DOMAIN_DEFINITIONS) {
    for (const exposure of definition.exposures) {
      for (const action of exposure.supportedActions) {
        const key = `${exposure.toolName}::${action}`;
        if (explicitKeys.has(key)) continue;

        const actionDefinition = definition.actions[action];
        if (!actionDefinition) continue;

        const readableAction = titleCase(humanizeToken(action));
        const title = exposure.displayName.toLowerCase().includes(humanizeToken(action))
          ? exposure.displayName
          : `${exposure.displayName} ${readableAction}`;

        derivedEntries.push({
          intent: `tool:${exposure.toolName}:${action}`,
          title,
          domain: definition.domain,
          toolName: exposure.toolName,
          action,
          keywords: buildDerivedKeywords(exposure.toolName, definition.domain, exposure.displayName, action),
          requiredFields: inferRequiredFields(actionDefinition.inputSchema as unknown as z.ZodTypeAny),
          description: exposure.description,
          buildToolArgs: buildDerivedToolArgs(action),
          source: "derived",
        });
      }
    }
  }

  return derivedEntries;
}

export const TOOL_CATALOG: ToolCatalogEntry[] = [
  ...EXPLICIT_TOOL_CATALOG.map((entry) => ({ ...entry, source: "explicit" as const })),
  ...buildDerivedRouteEntries(),
];

export function findToolCatalogEntry(toolName: string, action?: string): ToolCatalogEntry | undefined {
  if (action) {
    return TOOL_CATALOG.find((entry) => entry.toolName === toolName && entry.action === action);
  }
  return TOOL_CATALOG.find((entry) => entry.toolName === toolName);
}
