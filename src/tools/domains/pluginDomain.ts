import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const PluginActionSchema = z.enum(["health", "health_verbose"]);

const canonicalInputShape = {
  action: PluginActionSchema.optional().describe(
    "Which health check to run. Defaults to 'health' (basic connectivity) if omitted."
  ),
};

export const PLUGIN_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "plugin",
  actions: {
    health: {
      action: "health",
      inputSchema: z.object({ action: z.literal("health").optional() }),
      capabilities: ["query"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      pluginMethods: ["getCivil3DHealth"],
      execute: async () =>
        await withApplicationConnection(async (appClient) =>
          await appClient.sendCommand("getCivil3DHealth", {})
        ),
    },
    health_verbose: {
      action: "health_verbose",
      inputSchema: z.object({ action: z.literal("health_verbose") }),
      capabilities: ["query"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      pluginMethods: ["getCivil3DHealthVerbose"],
      execute: async () =>
        await withApplicationConnection(async (appClient) =>
          await appClient.sendCommand("getCivil3DHealthVerbose", {})
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_health",
      displayName: "Civil 3D Health Check",
      description:
        "Checks if the Civil 3D MCP plugin is running and responsive. " +
        "Use this to verify connectivity before performing other operations. " +
        "Actions: health (basic — the default when no action is given), health_verbose " +
        "(adds plugin assembly version, build date, active drawing name, open document count).",
      inputShape: canonicalInputShape,
      supportedActions: ["health", "health_verbose"],
      resolveAction: (rawArgs) => ({
        action: rawArgs.action ? String(rawArgs.action) : "health",
        args: rawArgs,
      }),
    },
  ],
};
