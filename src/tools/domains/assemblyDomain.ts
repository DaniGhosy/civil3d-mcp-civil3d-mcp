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
  "create_subassembly",
  "edit",
]);

const canonicalInputShape = {
  action: AssemblyActionSchema.describe("The assembly operation to perform."),
  name: z.string().optional().describe("Assembly name."),
  assemblyName: z.string().optional().describe("Assembly name (for subassembly parameter actions)."),
  subassemblyName: z.string().optional().describe("Subassembly name."),
  displayName: z.string().optional().describe("Parameter display name (set_subassembly_parameter)."),
  value: z.number().optional().describe("New parameter value (set_subassembly_parameter)."),
  insertX: z.number().optional().describe("Insertion X for a new empty assembly (create)."),
  insertY: z.number().optional().describe("Insertion Y for a new empty assembly (create)."),
  description: z.string().optional().describe("Assembly description (create)."),
  assemblyType: z.string().optional().describe("Assembly type, e.g. 'Undivided' (create)."),
  subassemblyType: z.string().optional().describe("Stock subassembly catalog name (create_subassembly)."),
  side: z.enum(["Left", "Right", "Both"]).optional().describe("Side to attach the subassembly (create_subassembly)."),
  parameters: z.record(z.union([z.number(), z.string(), z.boolean()])).optional().describe("Subassembly parameters by display name (create_subassembly/edit)."),
  delete: z.boolean().optional().describe("Delete the named subassembly instead of editing it (edit)."),
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
      inputSchema: z.object({
        action: z.literal("create"),
        name: z.string(),
        insertX: z.number(),
        insertY: z.number(),
        description: z.string().optional(),
        assemblyType: z.string().optional(),
      }),
      capabilities: ["create"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createAssembly"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createAssembly", {
            name: args.name,
            insertX: args.insertX,
            insertY: args.insertY,
            description: args.description,
            assemblyType: args.assemblyType ?? "Undivided",
          })
        ),
    },
    create_subassembly: {
      action: "create_subassembly",
      inputSchema: z.object({
        action: z.literal("create_subassembly"),
        assemblyName: z.string(),
        subassemblyType: z.string(),
        side: z.enum(["Left", "Right", "Both"]),
        parameters: z.record(z.union([z.number(), z.string(), z.boolean()])).optional(),
      }),
      capabilities: ["create", "edit"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["createSubassembly"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("createSubassembly", {
            assemblyName: args.assemblyName,
            subassemblyType: args.subassemblyType,
            side: args.side,
            parameters: args.parameters ?? {},
          })
        ),
    },
    edit: {
      action: "edit",
      inputSchema: z.object({
        action: z.literal("edit"),
        assemblyName: z.string(),
        subassemblyName: z.string().optional(),
        delete: z.boolean().optional(),
        parameters: z.record(z.union([z.number(), z.string(), z.boolean()])).optional(),
      }),
      capabilities: ["edit", "delete"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["editAssembly"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("editAssembly", {
            assemblyName: args.assemblyName,
            subassemblyName: args.subassemblyName,
            delete: args.delete ?? false,
            parameters: args.parameters ?? {},
          })
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
        "displayName), delete (by name), create (empty assembly baseline), create_subassembly " +
        "(imports a stock catalog subassembly onto an existing assembly), edit (lists " +
        "subassemblies when subassemblyName is omitted, else deletes or edits parameters on one).",
      inputShape: canonicalInputShape,
      supportedActions: [
        "list",
        "get",
        "list_subassemblies",
        "get_subassembly_parameters",
        "set_subassembly_parameter",
        "delete",
        "create",
        "create_subassembly",
        "edit",
      ],
      resolveAction: (rawArgs) => ({
        action: String(rawArgs.action),
        args: rawArgs,
      }),
    },
  ],
};
