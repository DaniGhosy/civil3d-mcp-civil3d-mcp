import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const SurveyActionSchema = z.enum(["list_figure_styles", "list_networks", "list_figures"]);

const canonicalInputShape = {
  action: SurveyActionSchema.describe("The survey operation to perform."),
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
      inputSchema: z.object({ action: z.literal("list_networks") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSurveyNetworks"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSurveyNetworks", {})
        ),
    },
    list_figures: {
      action: "list_figures",
      inputSchema: z.object({ action: z.literal("list_figures") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSurveyFigures"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSurveyFigures", {})
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_survey",
      displayName: "Civil 3D Survey",
      description:
        "Read Civil 3D survey data. Actions: list_figure_styles (real, via " +
        "civilDoc.Styles.SurveyFigureStyles). Note: list_networks and list_figures are not yet " +
        "implemented — the compiler confirmed CivilDocument.GetSurveyNetworkIds()/" +
        "GetSurveyFigureIds() aren't the real accessor names, so they return a 'planned' status " +
        "until the real access chain (likely via a SurveyDatabaseManager or similar) is " +
        "confirmed against a live Civil 3D drawing. Importing raw field survey data is out of " +
        "scope — proprietary file format with no simple API found.",
      inputShape: canonicalInputShape,
      supportedActions: ["list_figure_styles", "list_networks", "list_figures"],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
