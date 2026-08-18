import { McpServer, ResourceTemplate } from "@modelcontextprotocol/sdk/server/mcp.js";
import { getReportResource, listReportResources } from "./reportResourceStore.js";

/**
 * civil3d://reports/{reportId} — exposes cached report/export results (see
 * reportResourceStore.ts / domainRuntime.ts's executeExposure). Scoped to just this one resource
 * for now; source's mcpResources.ts also has a tool-catalog resource and a safety-guidance
 * markdown resource built on toolManifest.ts/tool_catalog.ts concepts this repo doesn't have yet
 * — those belong with the orchestrate/docs work, not this file.
 */
export function registerMcpResources(server: McpServer): void {
  server.resource(
    "civil3d-generated-report",
    new ResourceTemplate("civil3d://reports/{reportId}", {
      list: async () => ({
        resources: listReportResources().map((report) => ({
          name: report.name,
          uri: report.uri,
          description: "Retained structured output from a Civil 3D report or export action.",
          mimeType: "application/json",
          size: report.size,
        })),
      }),
      complete: {
        reportId: async () => listReportResources().map((report) => report.id),
      },
    }),
    {
      description: "Bounded, short-lived structured results from report and export actions.",
      mimeType: "application/json",
    },
    async (uri, variables) => {
      const reportId = String(variables.reportId ?? "");
      const report = getReportResource(reportId);
      if (!report) {
        throw new Error(`Civil 3D report resource '${reportId}' was not found or has expired.`);
      }
      return {
        contents: [{ uri: uri.href, mimeType: "application/json", text: report.text }],
      };
    }
  );
}
