/**
 * Shared types and interfaces for the domain-based tool architecture.
 */

/** Tool capability tags for categorization */
export type ToolCapability =
  | "query"
  | "inspect"
  | "create"
  | "edit"
  | "delete"
  | "analyze"
  | "generate"
  | "manage"
  | "import"
  | "export";

/** Domain categories for Civil 3D objects */
export type ToolDomain =
  | "plugin"
  | "generic"
  | "drawing"
  | "surface"
  | "alignment"
  | "profile"
  | "corridor"
  | "section"
  | "pipe"
  | "point"
  | "grading"
  | "parcel"
  | "geometry"
  | "blocks"
  | "label"
  | "legend"
  | "shape_detection"
  | "plan_vision"
  | "quantity"
  | "survey"
  | "project"
  | "assembly"
  | "workflow"
  | "coordinate_system"
  | "detention"
  | "intersection"
  | "sight_distance"
  | "slope_analysis"
  | "hydrology"
  | "cost_estimation"
  | "superelevation"
  | "qc"
  | "style"
  | "quantity_takeoff"
  | "standards"
  | "job"
  | "docs";

/** Catalog entry describing a registered tool */
export interface ToolCatalogEntry {
  toolName: string;
  displayName: string;
  description: string;
  domain: ToolDomain;
  capabilities: ToolCapability[];
  operations?: string[];
  pluginMethods?: string[];
  requiresActiveDrawing: boolean;
  safeForRetry: boolean;
  status: "implemented" | "planned";
}
