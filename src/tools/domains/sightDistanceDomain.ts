import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const SightDistanceActionSchema = z.enum(["calculate", "stopping_distance_check"]);

const canonicalInputShape = {
  action: SightDistanceActionSchema.describe("The sight distance operation to perform."),
  designSpeed: z.number().optional().describe("Design speed."),
  speedUnits: z.enum(["kmh", "mph"]).optional().describe("Units for designSpeed."),
  sightDistanceType: z.enum(["stopping", "passing", "decision"]).optional().describe("Type of sight distance to calculate."),
  grade: z.number().optional().describe("Grade percentage (signed)."),
  frictionCoefficient: z.number().optional().describe("Override friction coefficient (else AASHTO table lookup)."),
  perceptionReactionTime: z.number().optional().describe("Perception-reaction time in seconds."),
  standard: z.enum(["AASHTO", "FHWA", "HCM"]).optional().describe("Design standard."),
  alignmentName: z.string().optional().describe("Alignment name to check against (optional)."),
  profileName: z.string().optional().describe("Profile name to check against (optional)."),
  checkStation: z.number().optional().describe("Station to check K-value compliance at."),
  stationStart: z.number().optional().describe("Start station (for stopping_distance_check)."),
  stationEnd: z.number().optional().describe("End station (for stopping_distance_check)."),
  stationInterval: z.number().optional().describe("Station sampling interval."),
};

export const SIGHT_DISTANCE_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "sight_distance",
  actions: {
    calculate: {
      action: "calculate",
      inputSchema: z.object({
        action: z.literal("calculate"),
        designSpeed: z.number().positive(),
        speedUnits: z.enum(["kmh", "mph"]).optional(),
        sightDistanceType: z.enum(["stopping", "passing", "decision"]),
        grade: z.number().optional(),
        frictionCoefficient: z.number().positive().optional(),
        perceptionReactionTime: z.number().positive().optional(),
        standard: z.enum(["AASHTO", "FHWA", "HCM"]).optional(),
        alignmentName: z.string().optional(),
        profileName: z.string().optional(),
        checkStation: z.number().optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["calculateSightDistance"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("calculateSightDistance", {
            designSpeed: args.designSpeed,
            speedUnits: args.speedUnits ?? "kmh",
            sightDistanceType: args.sightDistanceType,
            grade: args.grade ?? 0,
            frictionCoefficient: args.frictionCoefficient ?? null,
            perceptionReactionTime: args.perceptionReactionTime ?? 2.5,
            standard: args.standard ?? "AASHTO",
            alignmentName: args.alignmentName ?? null,
            profileName: args.profileName ?? null,
            checkStation: args.checkStation ?? null,
          })
        ),
    },
    stopping_distance_check: {
      action: "stopping_distance_check",
      inputSchema: z.object({
        action: z.literal("stopping_distance_check"),
        alignmentName: z.string(),
        profileName: z.string(),
        designSpeed: z.number().positive(),
        stationStart: z.number().optional(),
        stationEnd: z.number().optional(),
        stationInterval: z.number().positive().optional(),
        standard: z.enum(["AASHTO", "FHWA"]).optional(),
      }),
      capabilities: ["query", "analyze"],
      requiresActiveDrawing: true,
      safeForRetry: true,
      pluginMethods: ["checkStoppingDistance"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("checkStoppingDistance", {
            alignmentName: args.alignmentName,
            profileName: args.profileName,
            designSpeed: args.designSpeed,
            stationStart: args.stationStart ?? null,
            stationEnd: args.stationEnd ?? null,
            stationInterval: args.stationInterval ?? 25,
            standard: args.standard ?? "AASHTO",
          })
        ),
    },
  },
  exposures: [
    {
      toolName: "civil3d_sight_distance",
      displayName: "Civil 3D Sight Distance",
      description: "Calculates and checks AASHTO sight distance compliance (stopping/passing/decision) through a single domain tool.",
      inputShape: canonicalInputShape,
      supportedActions: ["calculate", "stopping_distance_check"],
      resolveAction: (rawArgs) => ({ action: String(rawArgs.action ?? ""), args: rawArgs }),
    },
  ],
};
