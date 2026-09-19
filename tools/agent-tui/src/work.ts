export type WorkItem = {
  kebab: string;
  title: string;
  atMs: number;
  branch: string | null;
  current: boolean;
  source: "branch" | "commit" | "dirty";
};

const SKIP_STORY = /(?:^|\/)(to-review|.+-delivery)$/;

export function kebabFromFeaturePath(relPath: string): string | null {
  const normalized = relPath.replaceAll("\\", "/");
  const match = /(?:^|\/)docs\/features\/([^/]+)\.md$/i.exec(normalized);
  if (!match) {
    return null;
  }
  const name = match[1];
  if (SKIP_STORY.test(name) || name === "to-review" || name.endsWith("-delivery")) {
    return null;
  }
  return name;
}

export function matchKebabFromBranch(ref: string, kebabs: string[]): string | null {
  let short = ref.trim();
  short = short.replace(/^refs\/heads\//, "");
  short = short.replace(/^refs\/remotes\/origin\//, "");
  short = short.replace(/^origin\//, "");
  if (!short.startsWith("feature/")) {
    return null;
  }
  const rest = short.slice("feature/".length);
  const sorted = [...kebabs].sort((a, b) => b.length - a.length);
  for (const kebab of sorted) {
    if (rest === kebab || rest.startsWith(`${kebab}-`)) {
      return kebab;
    }
  }
  return null;
}

export function parseNameOnlyLog(text: string): Map<string, number> {
  const dates = new Map<string, number>();
  let currentMs: number | null = null;
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (!line) {
      continue;
    }
    const asDate = Date.parse(line);
    if (!Number.isNaN(asDate) && /^\d{4}-\d{2}-\d{2}/.test(line)) {
      currentMs = asDate;
      continue;
    }
    if (currentMs === null) {
      continue;
    }
    const kebab = kebabFromFeaturePath(line);
    if (!kebab || dates.has(kebab)) {
      continue;
    }
    dates.set(kebab, currentMs);
  }
  return dates;
}

export function parseFeatureRefLines(text: string, kebabs: string[]): Array<{ kebab: string; atMs: number; branch: string }> {
  const rows: Array<{ kebab: string; atMs: number; branch: string }> = [];
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (!line) {
      continue;
    }
    const tab = line.indexOf("\t");
    if (tab <= 0) {
      continue;
    }
    const atMs = Date.parse(line.slice(0, tab));
    if (Number.isNaN(atMs)) {
      continue;
    }
    const branch = line.slice(tab + 1).trim();
    const kebab = matchKebabFromBranch(branch, kebabs);
    if (!kebab) {
      continue;
    }
    rows.push({ kebab, atMs, branch: branch.replace(/^origin\//, "") });
  }
  return rows;
}

export function relativeTime(atMs: number, nowMs = Date.now()): string {
  const delta = Math.max(0, nowMs - atMs);
  const minute = 60_000;
  const hour = 60 * minute;
  const day = 24 * hour;
  if (delta < minute) {
    return "now";
  }
  if (delta < hour) {
    return `${Math.floor(delta / minute)}m`;
  }
  if (delta < 2 * day) {
    return `${Math.floor(delta / hour)}h`;
  }
  if (delta < 14 * day) {
    return `${Math.floor(delta / day)}d`;
  }
  return new Date(atMs).toISOString().slice(0, 10);
}

const ACTIVE_INBOX = new Set(["in-progress", "blocked", "resume", "ready-for-review"]);

export function isActiveWork(item: WorkItem, storyStatus: string | undefined, inboxStatus: string | undefined): boolean {
  if (item.branch || item.current || item.source === "dirty") {
    return true;
  }
  if (inboxStatus && ACTIVE_INBOX.has(inboxStatus)) {
    return true;
  }
  if (storyStatus && storyStatus !== "done") {
    return true;
  }
  return false;
}

export function mergeWorkItems(input: {
  commits: Map<string, number>;
  branches: Array<{ kebab: string; atMs: number; branch: string }>;
  dirty: Array<{ kebab: string; atMs: number }>;
  currentBranch: string;
  titles: Map<string, string>;
}): WorkItem[] {
  const byKebab = new Map<string, WorkItem>();
  const upsert = (partial: Omit<WorkItem, "title">) => {
    const existing = byKebab.get(partial.kebab);
    const title = input.titles.get(partial.kebab) ?? partial.kebab;
    if (!existing) {
      byKebab.set(partial.kebab, { ...partial, title });
      return;
    }
    if (partial.atMs >= existing.atMs) {
      existing.atMs = partial.atMs;
      existing.source = partial.source;
    }
    if (partial.branch) {
      existing.branch ??= partial.branch;
    }
    if (partial.current) {
      existing.current = true;
    }
  };

  for (const [kebab, atMs] of input.commits) {
    upsert({ kebab, atMs, branch: null, current: false, source: "commit" });
  }
  for (const row of input.branches) {
    upsert({
      kebab: row.kebab,
      atMs: row.atMs,
      branch: row.branch,
      current: matchKebabFromBranch(input.currentBranch, [row.kebab]) === row.kebab
        || input.currentBranch.replace(/^origin\//, "") === row.branch,
      source: "branch",
    });
  }
  for (const row of input.dirty) {
    upsert({ kebab: row.kebab, atMs: row.atMs, branch: null, current: false, source: "dirty" });
  }

  if (input.currentBranch.startsWith("feature/")) {
    const kebab = matchKebabFromBranch(input.currentBranch, [...byKebab.keys(), ...input.titles.keys()]);
    if (kebab) {
      const existing = byKebab.get(kebab);
      if (existing) {
        existing.current = true;
        existing.branch ??= input.currentBranch;
      }
    }
  }

  return [...byKebab.values()].sort((a, b) => b.atMs - a.atMs);
}
