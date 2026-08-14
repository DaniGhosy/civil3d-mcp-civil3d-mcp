import { z } from "zod";
import ExcelJS from "exceljs";
import type { DomainToolDefinition } from "../domainRuntime.js";

/**
 * Módulo E (parcial) del catálogo de lectura de planos: cuantificación y reportes. A diferencia
 * de todos los demás dominios, estas acciones no llaman al plugin — son post-proceso puro sobre
 * datos que el asistente ya extrajo con civil3d_blocks (o cualquier otra fuente). No requieren un
 * dibujo activo. Ver CLAUDE.md, "Filosofía: primitivas" para la nota sobre este patrón.
 */

const QuantityActionSchema = z.enum(["count_by_category", "generate_quantity_report"]);

const BlockCountSchema = z.object({
  name: z.string(),
  count: z.number(),
});

const CategoryMappingSchema = z.object({
  name: z.string(),
  category: z.string(),
});

const ReportItemSchema = z.object({
  category: z.string(),
  name: z.string(),
  count: z.number(),
});

const canonicalInputShape = {
  action: QuantityActionSchema.describe("The quantification operation to perform."),
  counts: z
    .array(BlockCountSchema)
    .optional()
    .describe("Block/symbol counts, e.g. from civil3d_blocks.list_block_definitions (count_by_category)."),
  categoryMap: z
    .array(CategoryMappingSchema)
    .optional()
    .describe("Symbol name -> category label mapping, e.g. from a legend (count_by_category)."),
  items: z
    .array(ReportItemSchema)
    .optional()
    .describe("Rows to export: category, symbol name, count (generate_quantity_report)."),
  outputPath: z
    .string()
    .optional()
    .describe("Absolute file path (.xlsx) to write the report to (generate_quantity_report)."),
};

function countByCategory(
  counts: { name: string; count: number }[],
  categoryMap: { name: string; category: string }[]
) {
  const nameToCategory = new Map(categoryMap.map((m) => [m.name.toLowerCase(), m.category]));
  const totals = new Map<string, number>();
  const uncategorized: { name: string; count: number }[] = [];

  for (const item of counts) {
    const category = nameToCategory.get(item.name.toLowerCase());
    if (!category) {
      uncategorized.push(item);
      continue;
    }
    totals.set(category, (totals.get(category) ?? 0) + item.count);
  }

  return {
    categories: [...totals.entries()].map(([category, total]) => ({ category, total })),
    uncategorized,
  };
}

async function generateQuantityReport(
  items: { category: string; name: string; count: number }[],
  outputPath: string
) {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet("BOQ");

  sheet.columns = [
    { header: "Categoría", key: "category", width: 30 },
    { header: "Símbolo", key: "name", width: 30 },
    { header: "Cantidad", key: "count", width: 12 },
  ];
  sheet.getRow(1).font = { bold: true };

  for (const item of items) {
    sheet.addRow(item);
  }

  await workbook.xlsx.writeFile(outputPath);

  return { success: true, outputPath, itemCount: items.length };
}

export const QUANTITY_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "quantity",
  actions: {
    count_by_category: {
      action: "count_by_category",
      inputSchema: z.object({
        action: z.literal("count_by_category"),
        counts: z.array(BlockCountSchema),
        categoryMap: z.array(CategoryMappingSchema),
      }),
      capabilities: ["analyze"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) => countByCategory(args.counts, args.categoryMap),
    },
    generate_quantity_report: {
      action: "generate_quantity_report",
      inputSchema: z.object({
        action: z.literal("generate_quantity_report"),
        items: z.array(ReportItemSchema),
        outputPath: z.string(),
      }),
      capabilities: ["export", "generate"],
      requiresActiveDrawing: false,
      safeForRetry: false,
      execute: async (args: any) => await generateQuantityReport(args.items, args.outputPath),
    },
  },
  exposures: [
    {
      toolName: "civil3d_quantity",
      displayName: "Civil 3D Quantity Reports",
      description:
        "Turn already-extracted block/symbol counts into budget-ready numbers. Pure post-processing " +
        "— does not read the drawing itself, does not require an active Civil 3D session. Actions: " +
        "count_by_category (groups raw block counts, e.g. from civil3d_blocks.list_block_definitions, " +
        "by a symbol-name-to-category mapping and sums totals; unmatched names come back in " +
        "'uncategorized' instead of being silently dropped), generate_quantity_report (exports a BOQ " +
        "table to an .xlsx file at a given absolute path).",
      inputShape: canonicalInputShape,
      supportedActions: ["count_by_category", "generate_quantity_report"],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
