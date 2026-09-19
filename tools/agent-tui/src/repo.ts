import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export function findRepoRoot(start?: string): string {
  let dir = path.resolve(start ?? process.cwd());
  for (;;) {
    if (fs.existsSync(path.join(dir, "WorkCosts.slnx")) && fs.existsSync(path.join(dir, "AGENTS.md"))) {
      return dir;
    }
    const parent = path.dirname(dir);
    if (parent === dir) {
      break;
    }
    dir = parent;
  }

  const fromHere = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
  if (fs.existsSync(path.join(fromHere, "WorkCosts.slnx"))) {
    return fromHere;
  }

  throw new Error("Could not find the WorkCosts repository root (WorkCosts.slnx + AGENTS.md).");
}
