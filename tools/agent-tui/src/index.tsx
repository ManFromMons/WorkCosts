import React from "react";
import { render } from "ink";
import { App } from "./app.tsx";
import { createFrameDiffStdout } from "./frameDiffStdout.ts";
import { findRepoRoot } from "./repo.ts";

try {
  const repoRoot = process.env.WORKCOSTS_ROOT?.trim() || findRepoRoot();
  process.chdir(repoRoot);
  render(<App repoRoot={repoRoot} />, { stdout: createFrameDiffStdout(process.stdout) });
} catch (err) {
  const message = err instanceof Error ? err.message : String(err);
  console.error(message);
  process.exit(1);
}
