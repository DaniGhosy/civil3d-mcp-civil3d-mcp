import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const SurveyActionSchema = z.enum([
  "list_figure_styles",
  "list_networks",
  "list_figures",
  "database_list",
  "figure_get",
  "observation_list",
]);

const canonicalInputShape = {
  action: SurveyActionSchema.describe("The survey operation to perform."),
  databaseName: z.string().optional().describe("Survey database name; filters list_networks/list_figures, resolves figure_get, required for observation_list."),
  name: z.string().optional().describe("Survey figure name (figure_get)."),
  networkName: z.string().optional().describe("Survey network name; filters observation_list."),
  observationType: z.enum(["all", "angles", "distances", "directions", "gps"]).optional().describe("Observation type filter (observation_list)."),
};

export const SURVEY_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "survey",
  actions: {
    list_figure_styles: {
      action: "list_figure_styles",
      inputSchema: z.object({ action: z.literal("list_figure_styles") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSurveyFigureStyles"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSurveyFigureStyles", {})
        ),
    },
    list_networks: {
      action: "list_networks",
      inputSchema: z.object({ action: z.literal("list_networks"), databaseName: z.string().optional() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSurveyNetworks"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSurveyNetworks", { databaseName: args.databaseName })
        ),
    },
    list_figures: {
      action: "list_figures",
      inputSchema: z.object({ action: z.literal("list_figures"), databaseName: z.string().optional() }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSurveyFigures"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSurveyFigures", { databaseName: args.databaseName })
        ),
    },
    database_list: {
      action: "database_list",
      inputSchema: z.object({ action: z.literal("database_list") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSurveyDatabases"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSurveyDatabases", {})
        ),
    },
    figure_get: {
      action: "figure_get",
      inputSchema: z.object({
        action: z.literal("figure_get"),
        name: z.string(),
        databaseName: z.string().optional(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSurveyFigure"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getSurveyFigure", { name: args.name, databaseName: args.databaseName })
        ),
    },
    observation_list: {
      action: "observation_list",
      inputSchema: z.object({
        action: z.literal("observation_list"),
        databaseName: z.string(),
        networkName: z.string().optional(),
        observationType: z.enum(["all", "angles", "distances", "directions", "gps"]).optional(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSurveyObservations"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSurveyObservations", {
            databaseName: args.databaseName,
            networkName: args.networkName,
            observationType: args.observationType ?? "all",
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_survey",
      displayName: "Civil 3D Survey",
      description:
        "Read Civil 3D survey data. Actions: list_figure_styles, list_networks, list_figures " +
        "(all optionally filtered by databaseName), database_list, figure_get (returns full " +
        "vertex data for one figure), observation_list (raw field-book observations, requires " +
        "databaseName, optional networkName/observationType filter). Access goes through " +
        "CivilDocument.SurveyDocument via reflection, not the GetSurveyNetworkIds()/" +
        "GetSurveyFigureIds() names (confirmed absent by the compiler). Creating survey " +
        "databases/figures, network adjustment, and LandXML import into a survey database are " +
        "not documented anywhere in the managed API — use the Survey toolspace workflow for those.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list_figure_styles",
        "list_networks",
        "list_figures",
        "database_list",
        "figure_get",
        "observation_list",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
