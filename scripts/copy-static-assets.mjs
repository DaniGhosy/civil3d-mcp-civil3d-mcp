// Copies non-TS data files that tsc doesn't touch into build/, so runtime
// readFile() calls that resolve paths relative to the compiled .js file
// find them in the same place they exist under src/.
import { copyFileSync, existsSync, mkdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));

const assets = [
  { from: "src/standards/data/civil3d_framework_rules.json", to: "build/standards/data/civil3d_framework_rules.json" },
  { from: "autodesk_videos.json", to: "build/help/data/autodesk_videos.json" },
];

for (const asset of assets) {
  const fromPath = path.join(repoRoot, asset.from);
  const toPath = path.join(repoRoot, asset.to);
  if (!existsSync(fromPath)) {
    console.warn(`[copy-static-assets] Missing source asset: ${asset.from}`);
    continue;
  }
  mkdirSync(path.dirname(toPath), { recursive: true });
  copyFileSync(fromPath, toPath);
}
