import { spawn } from "node:child_process";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { createLogger } from "./logger.js";

const log = createLogger("PythonBridge");

const PLAN_VISION_PYTHON = process.env.PLAN_VISION_PYTHON ?? "python";
const PLAN_VISION_TIMEOUT_MS = parseInt(process.env.PLAN_VISION_TIMEOUT ?? "120000", 10);

const PLAN_VISION_DIR = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "../../plan-vision"
);
const CLI_PATH = path.join(PLAN_VISION_DIR, "cli.py");

/**
 * Runs one plan-vision CLI command as a child process, mirroring the shape of
 * ConnectionManager.withApplicationConnection — a short-lived process per call instead of a
 * short-lived TCP connection per call, since there is no long-running Python server to keep a
 * connection open to.
 */
export function runPlanVisionCommand<T = unknown>(
  command: string,
  args: Record<string, unknown>
): Promise<T> {
  return new Promise((resolve, reject) => {
    const child = spawn(PLAN_VISION_PYTHON, [CLI_PATH, command], { cwd: PLAN_VISION_DIR });

    let stdout = "";
    let stderr = "";
    let settled = false;

    const timer = setTimeout(() => {
      if (settled) return;
      settled = true;
      child.kill();
      reject(
        new Error(`plan-vision command '${command}' timed out after ${PLAN_VISION_TIMEOUT_MS}ms.`)
      );
    }, PLAN_VISION_TIMEOUT_MS);

    child.stdout.on("data", (chunk) => (stdout += chunk.toString()));
    child.stderr.on("data", (chunk) => (stderr += chunk.toString()));

    child.on("error", (error) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);

      if ((error as NodeJS.ErrnoException).code === "ENOENT") {
        reject(
          new Error(
            `Could not run the Python interpreter '${PLAN_VISION_PYTHON}'. Install Python 3.10+ and ` +
              `the plan-vision dependencies (see plan-vision/README.md), or set the PLAN_VISION_PYTHON ` +
              `env var to the interpreter's full path.`
          )
        );
        return;
      }
      reject(error);
    });

    child.on("close", (exitCode) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);

      if (exitCode !== 0) {
        log.error("plan-vision command failed", { command, exitCode, stderr: stderr.trim() });
        reject(
          new Error(
            `plan-vision command '${command}' failed (exit ${exitCode}): ${
              stderr.trim() || "no error output"
            }`
          )
        );
        return;
      }

      try {
        resolve(JSON.parse(stdout.trim()) as T);
      } catch {
        reject(
          new Error(`plan-vision command '${command}' returned invalid JSON: ${stdout.trim()}`)
        );
      }
    });

    child.stdin.write(JSON.stringify(args));
    child.stdin.end();
  });
}
