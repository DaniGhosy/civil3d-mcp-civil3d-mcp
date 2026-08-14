import { z } from "zod";
import { readFile, writeFile } from "node:fs/promises";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

/**
 * Módulo C del catálogo de lectura de planos: simbología y leyenda. Solo read_legend_table llama
 * al plugin (lee la tabla cruda del dibujo, vía LegendCommands.cs). El resto — cruzar la leyenda
 * contra los bloques reales, compararlas, y persistir el diccionario símbolo→significado — es
 * post-proceso puro en TS, sin dibujo activo, mismo patrón que civil3d_quantity (ver CLAUDE.md).
 *
 * Módulo G ("persistencia y mejora continua") se pliega acá en vez de tener un dominio aparte:
 * train_symbol_signature/load_office_standard son el mismo patrón de lectura/escritura de JSON
 * local que export/import_symbol_library, solo que para firmas geométricas (consumidas por
 * civil3d_shape_detection.classify_geometry_by_signature) y para el estándar de oficina completo.
 */

const LegendActionSchema = z.enum([
  "read_legend_table",
  "build_symbol_dictionary",
  "compare_legend_vs_drawing",
  "export_symbol_library",
  "import_symbol_library",
  "train_symbol_signature",
  "load_office_standard",
]);

const SymbolDictionaryEntrySchema = z.object({
  blockName: z.string(),
  meaning: z.string(),
});

const GeometrySignatureSchema = z.object({
  name: z.string(),
  entityTypes: z.array(z.string()),
  description: z.string().optional(),
});

const OfficeStandardSchema = z.object({
  dictionary: z.array(SymbolDictionaryEntrySchema).optional(),
  categoryMap: z.array(z.object({ name: z.string(), category: z.string() })).optional(),
  signatures: z.array(GeometrySignatureSchema).optional(),
});

const canonicalInputShape = {
  action: LegendActionSchema.describe("The legend/symbology/signature-library operation to perform."),
  handle: z.string().optional().describe("Handle of a specific Table entity to read (read_legend_table). Omit to return every table in the drawing."),
  legendRows: z
    .array(z.array(z.string().nullable()))
    .optional()
    .describe("Raw rows from read_legend_table for the chosen legend table (build_symbol_dictionary)."),
  blockNames: z
    .array(z.string())
    .optional()
    .describe("Real block names from the drawing, e.g. from civil3d_blocks.list_block_definitions."),
  dictionary: z
    .array(SymbolDictionaryEntrySchema)
    .optional()
    .describe("A symbol -> meaning dictionary (compare_legend_vs_drawing, export_symbol_library)."),
  outputPath: z.string().optional().describe("Absolute file path (.json) to write the library to (export_symbol_library)."),
  inputPath: z.string().optional().describe("Absolute file path (.json) to load a previously exported library from (import_symbol_library)."),
  signature: GeometrySignatureSchema.optional().describe("A geometric signature to add or update, keyed by name (train_symbol_signature)."),
  libraryPath: z.string().optional().describe("Absolute file path (.json) of the signature library to upsert into (train_symbol_signature)."),
  standardPath: z.string().optional().describe("Absolute file path (.json) of a full office standard to load (load_office_standard)."),
};

function normalize(text: string): string {
  return text.toLowerCase().replace(/[\s_\-]+/g, "");
}

function buildSymbolDictionary(legendRows: (string | null)[][], blockNames: string[]) {
  const dictionary: { blockName: string; meaning: string }[] = [];
  const matchedRows = new Set<number>();
  const matchedBlocks = new Set<string>();

  blockNames.forEach((blockName) => {
    const normalizedBlock = normalize(blockName);

    const rowIndex = legendRows.findIndex((row, idx) => {
      if (matchedRows.has(idx)) return false;
      return row.some((cell) => {
        if (!cell) return false;
        const normalizedCell = normalize(cell);
        return normalizedCell.includes(normalizedBlock) || normalizedBlock.includes(normalizedCell);
      });
    });

    if (rowIndex === -1) return;

    const row = legendRows[rowIndex];
    const meaning = [...row].reverse().find((cell) => cell && cell.trim().length > 0) ?? "";

    dictionary.push({ blockName, meaning });
    matchedRows.add(rowIndex);
    matchedBlocks.add(blockName);
  });

  const unmatchedBlocks = blockNames.filter((name) => !matchedBlocks.has(name));
  const unmatchedLegendRows = legendRows.filter((_, idx) => !matchedRows.has(idx));

  return { dictionary, unmatchedBlocks, unmatchedLegendRows };
}

function compareLegendVsDrawing(
  dictionary: { blockName: string; meaning: string }[],
  blockNames: string[]
) {
  const dictionaryNames = new Set(dictionary.map((d) => d.blockName.toLowerCase()));
  const drawingNames = new Set(blockNames.map((n) => n.toLowerCase()));

  return {
    missingFromLegend: blockNames.filter((name) => !dictionaryNames.has(name.toLowerCase())),
    missingFromDrawing: dictionary
      .map((d) => d.blockName)
      .filter((name) => !drawingNames.has(name.toLowerCase())),
  };
}

async function exportSymbolLibrary(
  dictionary: { blockName: string; meaning: string }[],
  outputPath: string
) {
  await writeFile(outputPath, JSON.stringify({ dictionary }, null, 2), "utf-8");
  return { success: true, outputPath, entryCount: dictionary.length };
}

async function importSymbolLibrary(inputPath: string) {
  const raw = await readFile(inputPath, "utf-8");
  const parsed = JSON.parse(raw);
  const dictionary = z.array(SymbolDictionaryEntrySchema).parse(parsed.dictionary);
  return { dictionary, entryCount: dictionary.length };
}

async function trainSymbolSignature(
  signature: { name: string; entityTypes: string[]; description?: string },
  libraryPath: string
) {
  let signatures: { name: string; entityTypes: string[]; description?: string }[] = [];
  try {
    const raw = await readFile(libraryPath, "utf-8");
    signatures = z.array(GeometrySignatureSchema).parse(JSON.parse(raw).signatures ?? []);
  } catch (error: any) {
    if (error?.code !== "ENOENT") throw error;
  }

  const existingIndex = signatures.findIndex((s) => s.name === signature.name);
  if (existingIndex >= 0) {
    signatures[existingIndex] = signature;
  } else {
    signatures.push(signature);
  }

  await writeFile(libraryPath, JSON.stringify({ signatures }, null, 2), "utf-8");
  return { success: true, libraryPath, signatureCount: signatures.length };
}

async function loadOfficeStandard(standardPath: string) {
  const raw = await readFile(standardPath, "utf-8");
  return OfficeStandardSchema.parse(JSON.parse(raw));
}

export const LEGEND_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "legend",
  actions: {
    read_legend_table: {
      action: "read_legend_table",
      inputSchema: z.object({ action: z.literal("read_legend_table"), handle: z.string().optional() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["readLegendTable"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("readLegendTable", { handle: args.handle })
        ),
    },
    build_symbol_dictionary: {
      action: "build_symbol_dictionary",
      inputSchema: z.object({
        action: z.literal("build_symbol_dictionary"),
        legendRows: z.array(z.array(z.string().nullable())),
        blockNames: z.array(z.string()),
      }),
      capabilities: ["analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) => buildSymbolDictionary(args.legendRows, args.blockNames),
    },
    compare_legend_vs_drawing: {
      action: "compare_legend_vs_drawing",
      inputSchema: z.object({
        action: z.literal("compare_legend_vs_drawing"),
        dictionary: z.array(SymbolDictionaryEntrySchema),
        blockNames: z.array(z.string()),
      }),
      capabilities: ["analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) => compareLegendVsDrawing(args.dictionary, args.blockNames),
    },
    export_symbol_library: {
      action: "export_symbol_library",
      inputSchema: z.object({
        action: z.literal("export_symbol_library"),
        dictionary: z.array(SymbolDictionaryEntrySchema),
        outputPath: z.string(),
      }),
      capabilities: ["export"],
      requiresActiveDrawing: false,
      safeForRetry: false,
      execute: async (args: any) => await exportSymbolLibrary(args.dictionary, args.outputPath),
    },
    import_symbol_library: {
      action: "import_symbol_library",
      inputSchema: z.object({
        action: z.literal("import_symbol_library"),
        inputPath: z.string(),
      }),
      capabilities: ["import"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) => await importSymbolLibrary(args.inputPath),
    },
    train_symbol_signature: {
      action: "train_symbol_signature",
      inputSchema: z.object({
        action: z.literal("train_symbol_signature"),
        signature: GeometrySignatureSchema,
        libraryPath: z.string(),
      }),
      capabilities: ["manage"],
      requiresActiveDrawing: false,
      safeForRetry: false,
      execute: async (args: any) => await trainSymbolSignature(args.signature, args.libraryPath),
    },
    load_office_standard: {
      action: "load_office_standard",
      inputSchema: z.object({
        action: z.literal("load_office_standard"),
        standardPath: z.string(),
      }),
      capabilities: ["import"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) => await loadOfficeStandard(args.standardPath),
    },
  },
  exposures: [
    {
      toolName: "civil3d_legend",
      displayName: "Civil 3D Legend & Symbology",
      description:
        "Learn a drawing's own symbol -> meaning dictionary from its legend table instead of assuming " +
        "a fixed office standard. Actions: read_legend_table (raw rows of a Table entity — a specific " +
        "one by handle, or every table in the drawing when handle is omitted, so the caller picks which " +
        "one is the legend); build_symbol_dictionary, compare_legend_vs_drawing, export_symbol_library, " +
        "import_symbol_library are pure post-processing (do NOT call the plugin, no active drawing " +
        "required): build_symbol_dictionary cross-references legend rows against real block names (e.g. " +
        "from civil3d_blocks.list_block_definitions) by normalized substring match, surfacing anything " +
        "unmatched on both sides instead of dropping it; compare_legend_vs_drawing is a QA pass finding " +
        "symbols present in one but not the other; export/import_symbol_library persist a validated " +
        "dictionary as local JSON so it can be reused across drawings from the same office without " +
        "re-reading the legend every time. train_symbol_signature upserts one geometric signature " +
        "(name + expected entity types) into a local JSON library by name, for use with " +
        "civil3d_shape_detection.classify_geometry_by_signature — not machine learning, a hand-curated " +
        "catalog you grow one entry at a time. load_office_standard reads a single JSON file combining " +
        "a symbol dictionary, a category map, and a signature catalog, so a whole office convention can " +
        "be loaded once at the start of an analysis instead of rebuilding it per drawing.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "read_legend_table",
        "build_symbol_dictionary",
        "compare_legend_vs_drawing",
        "export_symbol_library",
        "import_symbol_library",
        "train_symbol_signature",
        "load_office_standard",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
