import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const SettingsObjectTypeSchema = z.enum([
  "general",
  "alignment",
  "profile",
  "profile_view",
  "corridor",
  "surface",
  "parcel",
  "pipe_network",
  "pressure_network",
  "point",
  "point_group",
  "assembly",
  "structure",
  "section",
  "sample_line",
  "catchment",
  "survey",
]);

const SettingsActionSchema = z.enum(["list_settings_tree", "set_feature_setting"]);

const canonicalInputShape = {
  action: SettingsActionSchema.describe("The settings operation to perform."),
  objectType: SettingsObjectTypeSchema.describe("Which feature/command settings root to read or write."),
  maxDepth: z.number().int().min(1).max(8).optional().describe("How many levels deep to walk nested settings groups (default 4) — list_settings_tree only."),
  settingPath: z.string().optional().describe("Dot-separated path to a leaf setting, e.g. 'Angle.Precision' — from a key path seen in list_settings_tree's output (set_feature_setting only)."),
  value: z.union([z.string(), z.number(), z.boolean()]).optional().describe("New value for the leaf setting (set_feature_setting only)."),
};

export const SETTINGS_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "settings",
  actions: {
    list_settings_tree: {
      action: "list_settings_tree",
      inputSchema: z.object({
        action: z.literal("list_settings_tree"),
        objectType: SettingsObjectTypeSchema,
        maxDepth: z.number().int().min(1).max(8).optional(),
      }),
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["listSettingsTree"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("listSettingsTree", {
            objectType: args.objectType,
            maxDepth: args.maxDepth,
          })
        ),
    },
    set_feature_setting: {
      action: "set_feature_setting",
      inputSchema: z.object({
        action: z.literal("set_feature_setting"),
        objectType: SettingsObjectTypeSchema,
        settingPath: z.string(),
        value: z.union([z.string(), z.number(), z.boolean()]),
      }),
      capabilities: ["edit"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["setFeatureSetting"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("setFeatureSetting", {
            objectType: args.objectType,
            settingPath: args.settingPath,
            value: args.value,
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_settings",
      displayName: "Civil 3D Feature Settings",
      description:
        "Read the Toolspace Settings tree (feature/command settings, e.g. right-click a " +
        "category in Toolspace > Settings > Edit Feature Settings) — separate from object " +
        "styles (use civil3d_style) and from drawing-level unit/zone settings (use " +
        "civil3d_drawing's settings action). list_settings_tree(objectType) resolves the real " +
        "settings root for that feature (Autodesk's SettingsRoot.GetSettings<T>() pattern) and " +
        "walks it recursively; each leaf setting comes back as {value: ...}, matching how the " +
        "Civil 3D API itself wraps every leaf (e.g. Alignment > Angle > Precision.Value). Some " +
        "objectType values are a runtime guess at the underlying .NET class name/namespace " +
        "(Autodesk's own docs don't give one consistent namespace across features) — an " +
        "unresolvable objectType returns a clear 'could not resolve' error rather than partial " +
        "or wrong data, never a silent empty tree. maxDepth (default 4) bounds recursion into " +
        "nested settings groups. set_feature_setting(objectType, settingPath, value) writes one " +
        "leaf: settingPath is the same dot-separated key path list_settings_tree's output shows " +
        "(e.g. 'Angle.Precision'); it must resolve to a leaf ({value: ...}) not a group, or the " +
        "call fails with a clear error instead of silently no-op'ing.",
      inputShape: canonicalInputShape,
      supportedActions: ["list_settings_tree", "set_feature_setting"],
      resolveAction: (rawArgs) => ({ action: String(rawArgs.action ?? ""), args: rawArgs }),
    },
  ],
};
