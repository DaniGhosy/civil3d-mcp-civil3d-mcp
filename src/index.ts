import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerTools } from "./tools/register.js";
import { registerHelpResources, registerHelpTool } from "./tools/helpTool.js";
import { registerApprovalTool } from "./tools/approvalTool.js";
import { registerMcpResources } from "./tools/mcpResources.js";
import { createLogger } from "./utils/logger.js";

const log = createLogger("MCP");

const server = new McpServer({
  name: "civil3d-mcp",
  version: "1.0.0",
});

async function main() {
  await registerTools(server);
  registerHelpTool(server);
  registerHelpResources(server);
  registerApprovalTool(server);
  registerMcpResources(server);

  const transport = new StdioServerTransport();
  await server.connect(transport);
  log.info("Civil 3D MCP Server started");
}

main().catch((error) => {
  log.error("Error starting Civil 3D MCP Server", { error: String(error) });
  process.exit(1);
});
