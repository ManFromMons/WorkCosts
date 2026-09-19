import fs from "node:fs/promises";
import path from "node:path";

export type SkillInfo = {
  name: string;
  description: string;
  invokeOnly: boolean;
};

const INVOKE_ONLY = new Set(["start-implement", "start-add-source", "start-port"]);

const HANDBOOK_INVOKE_ONLY: SkillInfo[] = [
  { name: "start-implement", description: "Kick off a named ready story, Seq, or next in the queue.", invokeOnly: true },
  { name: "start-add-source", description: "URL → confirm ≥3 product pages, or implement source-<host>.", invokeOnly: true },
  { name: "start-port", description: "GNOME playbook slice (`/start-port gnome`).", invokeOnly: true },
];

export async function loadSkills(repoRoot: string): Promise<SkillInfo[]> {
  const dir = path.join(repoRoot, ".cursor", "skills");
  let names: string[] = [];
  try {
    names = await fs.readdir(dir);
  } catch {
    names = [];
  }

  const skills: SkillInfo[] = [];
  for (const name of names.sort()) {
    const skillPath = path.join(dir, name, "SKILL.md");
    try {
      const text = await fs.readFile(skillPath, "utf8");
      const fm = parseFrontmatter(text);
      skills.push({
        name: fm.name ?? name,
        description: (fm.description ?? firstLine(text)).replace(/\s+/g, " ").trim(),
        invokeOnly: INVOKE_ONLY.has(fm.name ?? name) || /disable-model-invocation:\s*true/.test(text),
      });
    } catch {
      skills.push({
        name,
        description: "",
        invokeOnly: INVOKE_ONLY.has(name),
      });
    }
  }

  const have = new Set(skills.map((s) => s.name));
  for (const extra of HANDBOOK_INVOKE_ONLY) {
    if (!have.has(extra.name)) {
      skills.push(extra);
    }
  }
  skills.sort((a, b) => a.name.localeCompare(b.name));
  return skills;
}

function firstLine(text: string): string {
  return text.split(/\r?\n/).find((line) => line.trim().length > 0) ?? "";
}

function parseFrontmatter(text: string): { name?: string; description?: string } {
  if (!text.startsWith("---")) {
    return {};
  }
  const end = text.indexOf("\n---", 3);
  if (end < 0) {
    return {};
  }
  const body = text.slice(4, end);
  const name = /^name:\s*(.+)$/m.exec(body)?.[1]?.trim();
  const descBlock = /(?:^|\n)description:\s*(?:>-\s*)?([\s\S]*?)(?=\n[a-zA-Z0-9_-]+:|\n*$)/.exec(`\n${body}`);
  let description = descBlock?.[1]?.trim();
  if (description?.startsWith("|")) {
    description = description.replace(/^\|\s*/, "").replace(/\n/g, " ");
  }
  description = description?.replace(/\n/g, " ");
  return { name, description };
}
