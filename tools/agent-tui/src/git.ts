import fs from "node:fs/promises";
import path from "node:path";
import { runGit, runPowershell } from "./shell.ts";
import { invertQueue, parseQueueOutput, parseSeqLookup, type QueueItem } from "./queue.ts";
import {
  isActiveWork,
  kebabFromFeaturePath,
  mergeWorkItems,
  parseFeatureRefLines,
  parseNameOnlyLog,
  type WorkItem,
} from "./work.ts";

export type GitSnapshot = {
  branch: string;
  short: string;
  dirty: boolean;
  dirtyPaths: string[];
  mainVsPlanning: string;
};

function porcelainPath(line: string): string {
  const rest = line.slice(3).trim();
  const renamed = rest.split(" -> ").at(-1) ?? rest;
  return renamed.replace(/^"(.*)"$/, "$1").replaceAll("\\", "/");
}

export async function loadGitSnapshot(repoRoot: string): Promise<GitSnapshot> {
  const branch = (await runGit(["branch", "--show-current"], repoRoot)).stdout.trim() || "DETACHED";
  const status = await runGit(["status", "--porcelain", "-uall"], repoRoot);
  const dirtyPaths = status.stdout
    .split(/\r?\n/)
    .map((line) => line.trimEnd())
    .filter((line) => line.length > 0)
    .map(porcelainPath);
  const short = (await runGit(["status", "-sb"], repoRoot)).stdout.trim().split(/\r?\n/)[0] ?? branch;
  return {
    branch,
    short,
    dirty: dirtyPaths.length > 0,
    dirtyPaths,
    mainVsPlanning: await describeMainVsPlanning(repoRoot),
  };
}

async function refExists(repoRoot: string, ref: string): Promise<boolean> {
  const result = await runGit(["rev-parse", "--verify", "--quiet", ref], repoRoot);
  return result.code === 0;
}

async function describeMainVsPlanning(repoRoot: string): Promise<string> {
  const planning = (await refExists(repoRoot, "Planning"))
    ? "Planning"
    : (await refExists(repoRoot, "origin/Planning"))
      ? "origin/Planning"
      : null;
  if (!planning) {
    return "Planning: missing";
  }
  const result = await runGit(["rev-list", "--left-right", "--count", `origin/main...${planning}`], repoRoot);
  if (result.code !== 0) {
    return "main…Planning: ?";
  }
  const [behind, ahead] = result.stdout.trim().split(/\s+/);
  if (behind === "0" && ahead === "0") {
    return "origin/main = Planning";
  }
  return `Planning +${ahead ?? "?"}/-${behind ?? "?"} vs origin/main`;
}

export async function fetchOrigin(repoRoot: string): Promise<string> {
  const result = await runGit(["fetch", "origin"], repoRoot);
  return (result.stderr + result.stdout).trim();
}

export async function showOriginMainFile(repoRoot: string, relPath: string): Promise<string> {
  const result = await runGit(["show", `origin/main:${relPath.replaceAll("\\", "/")}`], repoRoot);
  if (result.code !== 0) {
    throw new Error(result.stderr.trim() || `git show origin/main:${relPath} failed`);
  }
  return result.stdout;
}

export async function readWorkingFile(repoRoot: string, relPath: string): Promise<string> {
  return fs.readFile(path.join(repoRoot, relPath), "utf8");
}

export async function writeWorkingFile(repoRoot: string, relPath: string, contents: string): Promise<void> {
  await fs.writeFile(path.join(repoRoot, relPath), contents, "utf8");
}

export async function loadQueue(repoRoot: string): Promise<{ raw: string; items: QueueItem[] }> {
  const result = await runPowershell("scripts/Get-FeatureQueue.ps1", [], repoRoot);
  if (result.code !== 0 && result.code !== 2) {
    throw new Error(result.stderr.trim() || result.stdout.trim() || `Get-FeatureQueue.ps1 exited ${result.code}`);
  }
  const raw = result.stdout;
  return { raw, items: invertQueue(parseQueueOutput(raw)) };
}

export async function loadPickup(repoRoot: string): Promise<string> {
  const result = await runPowershell("scripts/Get-NextReadyFeature.ps1", [], repoRoot);
  const line = result.stdout.trim().split(/\r?\n/).filter(Boolean).at(-1) ?? "QUEUE_EMPTY";
  return line.trim();
}

export async function lookupSeq(repoRoot: string, seq: number): Promise<Record<string, string>> {
  const result = await runPowershell("scripts/Get-FeatureQueue.ps1", ["-Seq", String(seq)], repoRoot);
  return parseSeqLookup(result.stdout);
}

export async function landToReview(repoRoot: string, message: string): Promise<{ stdout: string; stderr: string; code: number }> {
  const result = await runPowershell("scripts/Update-ToReviewOnMain.ps1", ["-Message", message], repoRoot);
  if (result.code !== 0) {
    throw new Error(result.stderr.trim() || result.stdout.trim() || `Update-ToReviewOnMain.ps1 exited ${result.code}`);
  }
  return result;
}

export async function mergePlanning(repoRoot: string): Promise<{ stdout: string; stderr: string; code: number }> {
  const result = await runPowershell("scripts/Merge-PlanningToMain.ps1", [], repoRoot);
  if (result.code !== 0) {
    throw new Error(result.stderr.trim() || result.stdout.trim() || `Merge-PlanningToMain.ps1 exited ${result.code}`);
  }
  return result;
}

export function otherDirtyPaths(paths: string[], inboxRel = "docs/features/to-review.md"): string[] {
  const normalized = inboxRel.replaceAll("\\", "/");
  return paths.filter((p) => p.replaceAll("\\", "/") !== normalized);
}

export async function loadRecentWork(
  repoRoot: string,
  input: {
    kebabs: string[];
    titles: Map<string, string>;
    storyStatus: Map<string, string>;
    inboxStatus: Map<string, string>;
    currentBranch: string;
    dirtyPaths: string[];
  },
): Promise<WorkItem[]> {
  const kebabs = [...new Set(input.kebabs)];
  try {
    const names = await fs.readdir(path.join(repoRoot, "docs", "features"));
    for (const name of names) {
      const kebab = kebabFromFeaturePath(`docs/features/${name}`);
      if (kebab) {
        kebabs.push(kebab);
      }
    }
  } catch {
    // no features directory
  }
  const uniqueKebabs = [...new Set(kebabs)];

  const [log, refs] = await Promise.all([
    runGit(["log", "--pretty=format:%cI", "--name-only", "--", "docs/features"], repoRoot),
    runGit(
      [
        "for-each-ref",
        "--sort=-committerdate",
        "--format=%(committerdate:iso-strict)%09%(refname:short)",
        "refs/heads/feature",
        "refs/remotes/origin/feature",
      ],
      repoRoot,
    ),
  ]);

  const dirty: Array<{ kebab: string; atMs: number }> = [];
  for (const rel of input.dirtyPaths) {
    const kebab = kebabFromFeaturePath(rel);
    if (!kebab) {
      continue;
    }
    try {
      const st = await fs.stat(path.join(repoRoot, rel.replaceAll("/", path.sep)));
      dirty.push({ kebab, atMs: st.mtimeMs });
    } catch {
      dirty.push({ kebab, atMs: Date.now() });
    }
  }

  const merged = mergeWorkItems({
    commits: parseNameOnlyLog(log.stdout),
    branches: parseFeatureRefLines(refs.stdout, uniqueKebabs),
    dirty,
    currentBranch: input.currentBranch,
    titles: input.titles,
  });

  return merged
    .filter((item) => isActiveWork(item, input.storyStatus.get(item.kebab), input.inboxStatus.get(item.kebab)))
    .slice(0, 20);
}
