import { z } from "zod";
import { runPlanVisionCommand } from "../../utils/PythonBridge.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

/**
 * Módulo F del catálogo de lectura de planos: PDF o imagen escaneada, sin un dibujo vivo en
 * Civil3D. Único dominio del repo que no habla con el plugin C# — 5 de sus 6 acciones son
 * subprocesos Python (plan-vision/, ver su README), y calibrate_scale_from_dimension es
 * matemática pura en TS (no necesita CV). Todas las acciones tienen requiresActiveDrawing: false.
 *
 * Nivel de confianza, no exactitud garantizada — visión clásica con OpenCV (contornos + template
 * matching multi-escala/multi-rotación), no un modelo entrenado. Ver plan-vision/README.md.
 */

const PlanVisionActionSchema = z.enum([
  "rasterize_pdf_page",
  "extract_legend_templates",
  "train_symbol_template",
  "detect_symbols_cv",
  "ocr_extract_labels",
  "calibrate_scale_from_dimension",
]);

const PixelPointSchema = z.object({ x: z.number(), y: z.number() });
const RegionSchema = z.object({ x: z.number(), y: z.number(), width: z.number(), height: z.number() });

const canonicalInputShape = {
  action: PlanVisionActionSchema.describe("The scanned-plan-reading operation to perform."),
  pdfPath: z.string().optional().describe("Absolute path to the source PDF (rasterize_pdf_page)."),
  page: z.number().optional().describe("Zero-based page index to rasterize (rasterize_pdf_page)."),
  outputPath: z.string().optional().describe("Absolute path to write the rasterized PNG to (rasterize_pdf_page)."),
  dpi: z.number().optional().describe("Rasterization resolution. Default 300 (rasterize_pdf_page)."),
  legendImagePath: z.string().optional().describe("Absolute path to an already-cropped legend image (extract_legend_templates)."),
  libraryPath: z
    .string()
    .optional()
    .describe("Absolute path to the local template library directory (extract_legend_templates, train_symbol_template, detect_symbols_cv)."),
  imagePath: z.string().optional().describe("Absolute path to an image (train_symbol_template source crop, detect_symbols_cv, ocr_extract_labels target)."),
  name: z.string().optional().describe("Symbol name to store the template under (train_symbol_template)."),
  region: RegionSchema.optional().describe("Pixel region to crop from imagePath. Omit to use the whole image (train_symbol_template)."),
  matchThreshold: z.number().optional().describe("Minimum normalized-correlation confidence to count as a match. Default 0.75 (detect_symbols_cv)."),
  scales: z.array(z.number()).optional().describe("Scale factors to sweep. Default ±15% in 5% steps (detect_symbols_cv)."),
  rotations: z.array(z.number()).optional().describe("Rotation angles in degrees to sweep. Default [0, 90, 180, 270] (detect_symbols_cv)."),
  minConfidence: z.number().optional().describe("Minimum OCR confidence (0-100) to keep a result. Default 0 (extract_legend_templates, ocr_extract_labels)."),
  pixelPointA: PixelPointSchema.optional().describe("First pixel point of a known dimension (calibrate_scale_from_dimension)."),
  pixelPointB: PixelPointSchema.optional().describe("Second pixel point of a known dimension (calibrate_scale_from_dimension)."),
  realDistance: z.number().optional().describe("The real-world distance between pixelPointA and pixelPointB, in whatever unit you want the result expressed in (calibrate_scale_from_dimension)."),
};

function calibrateScaleFromDimension(
  pixelPointA: { x: number; y: number },
  pixelPointB: { x: number; y: number },
  realDistance: number
) {
  const pixelDistance = Math.hypot(pixelPointB.x - pixelPointA.x, pixelPointB.y - pixelPointA.y);
  if (pixelDistance === 0) {
    throw new Error("pixelPointA and pixelPointB must be different points.");
  }

  return {
    pixelDistance,
    realDistance,
    unitsPerPixel: realDistance / pixelDistance,
    pixelsPerUnit: pixelDistance / realDistance,
  };
}

export const PLAN_VISION_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "plan_vision",
  actions: {
    rasterize_pdf_page: {
      action: "rasterize_pdf_page",
      inputSchema: z.object({
        action: z.literal("rasterize_pdf_page"),
        pdfPath: z.string(),
        page: z.number(),
        outputPath: z.string(),
        dpi: z.number().optional(),
      }),
      capabilities: ["import"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) =>
        await runPlanVisionCommand("rasterize_pdf_page", {
          pdfPath: args.pdfPath,
          page: args.page,
          outputPath: args.outputPath,
          dpi: args.dpi,
        }),
    },
    extract_legend_templates: {
      action: "extract_legend_templates",
      inputSchema: z.object({
        action: z.literal("extract_legend_templates"),
        legendImagePath: z.string(),
        libraryPath: z.string(),
        minConfidence: z.number().optional(),
      }),
      capabilities: ["analyze", "import"],
      requiresActiveDrawing: false,
      safeForRetry: false,
      execute: async (args: any) =>
        await runPlanVisionCommand("extract_legend_templates", {
          legendImagePath: args.legendImagePath,
          libraryPath: args.libraryPath,
          minConfidence: args.minConfidence,
        }),
    },
    train_symbol_template: {
      action: "train_symbol_template",
      inputSchema: z.object({
        action: z.literal("train_symbol_template"),
        imagePath: z.string(),
        name: z.string(),
        libraryPath: z.string(),
        region: RegionSchema.optional(),
      }),
      capabilities: ["manage"],
      requiresActiveDrawing: false,
      safeForRetry: false,
      execute: async (args: any) =>
        await runPlanVisionCommand("train_symbol_template", {
          imagePath: args.imagePath,
          name: args.name,
          libraryPath: args.libraryPath,
          region: args.region,
        }),
    },
    detect_symbols_cv: {
      action: "detect_symbols_cv",
      inputSchema: z.object({
        action: z.literal("detect_symbols_cv"),
        imagePath: z.string(),
        libraryPath: z.string(),
        matchThreshold: z.number().optional(),
        scales: z.array(z.number()).optional(),
        rotations: z.array(z.number()).optional(),
      }),
      capabilities: ["analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) =>
        await runPlanVisionCommand("detect_symbols_cv", {
          imagePath: args.imagePath,
          libraryPath: args.libraryPath,
          matchThreshold: args.matchThreshold,
          scales: args.scales,
          rotations: args.rotations,
        }),
    },
    ocr_extract_labels: {
      action: "ocr_extract_labels",
      inputSchema: z.object({
        action: z.literal("ocr_extract_labels"),
        imagePath: z.string(),
        minConfidence: z.number().optional(),
      }),
      capabilities: ["query"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) =>
        await runPlanVisionCommand("ocr_extract_labels", {
          imagePath: args.imagePath,
          minConfidence: args.minConfidence,
        }),
    },
    calibrate_scale_from_dimension: {
      action: "calibrate_scale_from_dimension",
      inputSchema: z.object({
        action: z.literal("calibrate_scale_from_dimension"),
        pixelPointA: PixelPointSchema,
        pixelPointB: PixelPointSchema,
        realDistance: z.number(),
      }),
      capabilities: ["analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) =>
        calibrateScaleFromDimension(args.pixelPointA, args.pixelPointB, args.realDistance),
    },
  },
  exposures: [
    {
      toolName: "civil3d_plan_vision",
      displayName: "Civil 3D Plan Vision (scanned plans)",
      description:
        "Read a PDF or scanned image plan when there's no live Civil3D drawing — the only tool in " +
        "this server that never touches the plugin, all actions have requiresActiveDrawing: false. " +
        "Confidence-level detection over pixels (classical OpenCV computer vision), not the exactness " +
        "civil3d_blocks gives for real blocks — depends on scan quality. Requires Python 3.10+ and " +
        "Tesseract OCR installed on the machine (see plan-vision/README.md). Actions: rasterize_pdf_page " +
        "(PDF page → PNG via PyMuPDF), extract_legend_templates (the recommended way to start: OCR an " +
        "already-cropped legend image once and auto-build a whole named symbol-template library from " +
        "it — no need to train each symbol by hand; rows it can't isolate cleanly come back in " +
        "unresolvedRows for train_symbol_template to fix), train_symbol_template (manual add/correct " +
        "one template by name in the same library), detect_symbols_cv (matches every template in the " +
        "library against the full plan image across multiple scales and rotations — default rotations " +
        "0/90/180/270, the ones hand-drawn CAD symbols actually use — with non-max suppression per " +
        "symbol; a template with no internal contrast, e.g. a bad crop with no visible edge, is " +
        "mathematically degenerate for normalized template matching and comes back in " +
        "'skippedTemplates' with a reason instead of producing bogus 100%-confidence matches " +
        "everywhere), ocr_extract_labels (loose text labels outside the legend). " +
        "calibrate_scale_from_dimension is pure math in TS (no Python, no CV): converts a known " +
        "real-world distance between two pixel points into units-per-pixel / pixels-per-unit. " +
        "Detections from detect_symbols_cv feed civil3d_quantity.count_by_category/generate_quantity_report " +
        "the same way civil3d_blocks counts do.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "rasterize_pdf_page",
        "extract_legend_templates",
        "train_symbol_template",
        "detect_symbols_cv",
        "ocr_extract_labels",
        "calibrate_scale_from_dimension",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
