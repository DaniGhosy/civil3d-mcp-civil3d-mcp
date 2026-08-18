import { z } from "zod";
import { withApplicationConnection } from "../../utils/ConnectionManager.js";
import type { DomainToolDefinition } from "../domainRuntime.js";

const JobStartArgs = z.object({
  action: z.literal("start"),
  operation: z.string().min(1),
  parameters: z.record(z.unknown()).optional(),
});
const JobStatusArgs = z.object({ action: z.literal("status"), jobId: z.string().min(1) });
const JobCancelArgs = z.object({ action: z.literal("cancel"), jobId: z.string().min(1) });

export const JOB_DOMAIN_DEFINITION: DomainToolDefinition = {
  domain: "job",
  actions: {
    start: {
      action: "start",
      inputSchema: JobStartArgs,
      capabilities: ["manage", "generate"],
      requiresActiveDrawing: true,
      safeForRetry: false,
      pluginMethods: ["startJob"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) =>
          await c.sendCommand("startJob", {
            operation: args.operation,
            parameters: args.parameters ?? {},
          })
        ),
    },
    status: {
      action: "status",
      inputSchema: JobStatusArgs,
      capabilities: ["query", "inspect"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      pluginMethods: ["getJobStatus"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) => await c.sendCommand("getJobStatus", { jobId: args.jobId })),
    },
    cancel: {
      action: "cancel",
      inputSchema: JobCancelArgs,
      capabilities: ["manage"],
      requiresActiveDrawing: false,
      safeForRetry: true,
      pluginMethods: ["cancelJob"],
      execute: async (args: any) =>
        await withApplicationConnection(async (c) => await c.sendCommand("cancelJob", { jobId: args.jobId })),
    },
  },
  exposures: [
    {
      toolName: "civil3d_job",
      displayName: "Civil 3D Job",
      description:
        "Runs a long-running plugin operation in the background and lets you poll its status or " +
        "request cooperative cancellation, instead of blocking the MCP call until it finishes. " +
        "Actions: start (operation is any plugin method name already handled by the C# dispatcher " +
        "— e.g. 'qcReportGenerate' for a bulk QC report across a large drawing — plus the " +
        "'parameters' object that method expects; returns immediately with a jobId in state " +
        "'running'), status (poll by jobId — state is running/completed/failed/cancelled, with " +
        "progressPercent/currentPhase while running and result/warnings once terminal), cancel " +
        "(best-effort cooperative cancellation — Civil 3D host work already in flight when " +
        "cancellation is requested still commits, and is reported as completed with a warning " +
        "rather than silently discarded).",
      inputShape: {
        action: z.enum(["start", "status", "cancel"]),
        jobId: z.string().optional(),
        operation: z.string().optional(),
        parameters: z.record(z.unknown()).optional(),
      },
      supportedActions: ["start", "status", "cancel"],
      resolveAction: (rawArgs) => ({ action: String(rawArgs.action ?? ""), args: rawArgs }),
    },
  ],
};
