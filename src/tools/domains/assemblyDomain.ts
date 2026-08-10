import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const AssemblyActionSchema = z.enum([
  "list",
  "get",
  "list_subassemblies",
  "get_subassembly_parameters",
  "set_subassembly_parameter",
  "delete",
  "create",
]);

const canonicalInputShape = {
  action: AssemblyActionSchema.describe("The assembly operation to perform."),
  name: z.string().optional().describe("Assembly name."),
  assemblyName: z.string().optional().describe("Assembly name (for subassembly parameter actions)."),
  subassemblyName: z.string().optional().describe("Subassembly name."),
  displayName: z.string().optional().describe("Parameter display name (set_subassembly_parameter)."),
  value: z.number().optional().describe("New parameter value (set_subassembly_parameter)."),
};

export const ASSEMBLY_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "assembly",
  actions: {
    list: {
      action: "list",
      inputSchema: z.object({ action: z.literal("list") }),
      capabilities: ["query"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listAssemblies"],
      execute: async () =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listAssemblies", {})
        ),
    },
    get: {
      action: "get",
      inputSchema: z.object({ action: z.literal("get"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getAssembly"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getAssembly", { name: args.name })
        ),
    },
    list_subassemblies: {
      action: "list_subassemblies",
      inputSchema: z.object({ action: z.literal("list_subassemblies"), name: z.string() }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listAssemblySubassemblies"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listAssemblySubassemblies", { name: args.name })
        ),
    },
    get_subassembly_parameters: {
      action: "get_subassembly_parameters",
      inputSchema: z.object({
        action: z.literal("get_subassembly_parameters"),
        assemblyName: z.string(),
        subassemblyName: z.string(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["getSubassemblyParameters"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("getSubassemblyParameters", {
            assemblyName: args.assemblyName,
            subassemblyName: args.subassemblyName,
          })
        ),
    },
    set_subassembly_parameter: {
      action: "set_subassembly_parameter",
      inputSchema: z.object({
        action: z.literal("set_subassembly_parameter"),
        assemblyName: z.string(),
        subassemblyName: z.string(),
        displayName: z.string(),
        value: z.number(),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["setSubassemblyParameter"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("setSubassemblyParameter", {
            assemblyName: args.assemblyName,
            subassemblyName: args.subassemblyName,
            displayName: args.displayName,
            value: args.value,
          })
        ),
    },
    delete: {
      action: "delete",
      inputSchema: z.object({ action: z.literal("delete"), name: z.string() }),
      capabilities: ["delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["deleteAssembly"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("deleteAssembly", { name: args.name })
        ),
    },
    create: {
      action: "create",
      inputSchema: z.object({ action: z.literal("create"), name: z.string().optional() }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createAssembly"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createAssembly", { name: args.name })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_assembly",
      displayName: "Civil 3D Assembly",
      description:
        "Manage Civil 3D corridor assemblies. Actions: list, get (by name), " +
        "list_subassemblies (by assembly name), get_subassembly_parameters (double/bool/string " +
        "parameters of a subassembly), set_subassembly_parameter (writes a double parameter by " +
        "displayName), delete (by name). Note: create is not yet implemented — it returns a " +
        "'planned' status until the Assembly factory API is verified against a live Civil 3D drawing.",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list",
        "get",
        "list_subassemblies",
        "get_subassembly_parameters",
        "set_subassembly_parameter",
        "delete",
        "create",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
