import { z } from "zod";
import { lookupFrameworkStandards } from "../../standards/FrameworkStandardsService.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const StandardsLookupActionSchema = z.enum(["lookup"]);

const canonicalInputShape = {
  action: StandardsLookupActionSchema.describe("The standards lookup operation to perform."),
  query: z.string().optional().describe("Free-text search query."),
  topic: z
    .string()
    .optional()
    .describe(
      "Topic shortcut with built-in synonym expansion. One of: templates, styles, layers, labels, plotting, textstyles, proposed_existing, pipe_networks, profile_section (any other value is still used as a plain search term)."
    ),
  tags: z.array(z.string()).optional().describe("Restrict to rules carrying any of these tags."),
  maxResults: z.number().int().min(1).max(20).optional().describe("Max rules to return, default 5."),
};

export const STANDARDS_LOOKUP_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "standards",
  actions: {
    lookup: {
      action: "lookup",
      inputSchema: z.object({
        action: z.literal("lookup"),
        query: z.string().optional(),
        topic: z.string().optional(),
        tags: z.array(z.string()).optional(),
        maxResults: z.number().int().min(1).max(20).optional(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      execute: async (args: any) =>
        await lookupFrameworkStandards({
          query: args.query,
          topic: args.topic,
          tags: args.tags,
          maxResults: args.maxResults,
        }),
    },
  },
  exposures: [
    {
      toolName: "civil3d_standards_lookup",
      displayName: "Civil 3D Standards Lookup",
      description:
        "Looks up Civil 3D CAD-standards guidance (template hierarchy, style management, " +
        "layer/label conventions, plotting) from a small curated rule set — pure keyword " +
        "search over 29 rules, no AI/LLM involved and no active drawing required. The rule " +
        "text was reconstructed from a PDF text extraction that had interleaved fragments " +
        "from adjacent bullets (a common column-bleed artifact) — it is a faithful paraphrase " +
        "of real Civil 3D CAD-management best practices, not a verbatim quote of the source " +
        "document. Actions: lookup (query and/or topic and/or tags, all optional and " +
        "combinable — omitting all three returns the full rule set up to maxResults, ranked " +
        "by each rule's own baseline relevance score).",
      inputShape: canonicalInputShape,
      supportedActions: ["lookup"],
      resolveAction: (rawArgs) => ({
        action: "lookup",
        args: rawArgs,
      }),
    },
  ],
};
