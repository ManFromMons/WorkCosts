export type InboxCheckbox = {
  index: number;
  checked: boolean;
  text: string;
};

export type InboxHeading = {
  kebab: string;
  block: string;
  status: string;
  checkboxes: InboxCheckbox[];
};

export type ParsedInbox = {
  prefix: string;
  headings: InboxHeading[];
};

const HEADING_RE = /^## ([a-z][a-z0-9-]*)$/;
const STATUS_RE = /^-\s+\*\*Status:\*\*\s*(.+?)\s*$/m;
const CHECKBOX_RE = /^-\s+\[([ xX])\]\s+(.*)$/;

const SKIP_HEADINGS = new Set(["entries"]);

export const INBOX_STATUS_CYCLE = ["in-progress", "ready-for-review", "done"] as const;

export function cycleInboxStatus(current: string): string {
  const i = INBOX_STATUS_CYCLE.indexOf(current as (typeof INBOX_STATUS_CYCLE)[number]);
  if (i >= 0) {
    return INBOX_STATUS_CYCLE[(i + 1) % INBOX_STATUS_CYCLE.length];
  }
  if (current === "blocked") {
    return "resume";
  }
  if (current === "resume") {
    return "in-progress";
  }
  return "in-progress";
}

export function parseCheckboxes(block: string): InboxCheckbox[] {
  const checkboxes: InboxCheckbox[] = [];
  for (const line of block.split(/\r?\n/)) {
    const match = CHECKBOX_RE.exec(line);
    if (!match) {
      continue;
    }
    checkboxes.push({
      index: checkboxes.length,
      checked: match[1].toLowerCase() === "x",
      text: match[2],
    });
  }
  return checkboxes;
}

export function parseHeadingBlock(kebab: string, block: string): InboxHeading {
  const statusMatch = STATUS_RE.exec(block);
  return {
    kebab,
    block: block.replace(/\s+$/, ""),
    status: statusMatch ? statusMatch[1].trim() : "",
    checkboxes: parseCheckboxes(block),
  };
}

export function parseInbox(markdown: string): ParsedInbox {
  const lines = markdown.split(/\r?\n/);
  const headings: InboxHeading[] = [];
  let prefixLines: string[] = [];
  let current: { kebab: string; lines: string[] } | null = null;

  const flush = () => {
    if (!current) {
      return;
    }
    headings.push(parseHeadingBlock(current.kebab, current.lines.join("\n")));
    current = null;
  };

  for (const line of lines) {
    const heading = HEADING_RE.exec(line.trim());
    if (heading && !SKIP_HEADINGS.has(heading[1])) {
      if (!current) {
        prefixLines = prefixLines.slice();
      }
      flush();
      current = { kebab: heading[1], lines: [line] };
      continue;
    }
    if (current) {
      current.lines.push(line);
    } else {
      prefixLines.push(line);
    }
  }
  flush();

  return {
    prefix: prefixLines.join("\n").replace(/\s+$/, ""),
    headings,
  };
}

export function serializeInbox(parsed: ParsedInbox): string {
  const parts = [parsed.prefix.replace(/\s+$/, "")];
  for (const heading of parsed.headings) {
    parts.push(heading.block.replace(/\s+$/, ""));
  }
  return `${parts.join("\n\n").replace(/\s+$/, "")}\n`;
}

export function findHeading(parsed: ParsedInbox, kebab: string): InboxHeading | undefined {
  return parsed.headings.find((h) => h.kebab === kebab);
}

export function toggleCheckbox(markdown: string, kebab: string, checkboxIndex: number): string {
  const parsed = parseInbox(markdown);
  const heading = findHeading(parsed, kebab);
  if (!heading) {
    throw new Error(`No inbox heading ${kebab}.`);
  }
  const box = heading.checkboxes[checkboxIndex];
  if (!box) {
    throw new Error(`No checkbox ${checkboxIndex} on ${kebab}.`);
  }

  let seen = -1;
  const nextBlock = heading.block
    .split(/\r?\n/)
    .map((line) => {
      const match = CHECKBOX_RE.exec(line);
      if (!match) {
        return line;
      }
      seen += 1;
      if (seen !== checkboxIndex) {
        return line;
      }
      const mark = match[1].toLowerCase() === "x" ? " " : "x";
      return `- [${mark}] ${match[2]}`;
    })
    .join("\n");

  heading.block = nextBlock;
  heading.checkboxes = parseCheckboxes(nextBlock);
  return serializeInbox(parsed);
}

export function setHeadingStatus(markdown: string, kebab: string, status: string): string {
  const parsed = parseInbox(markdown);
  const heading = findHeading(parsed, kebab);
  if (!heading) {
    throw new Error(`No inbox heading ${kebab}.`);
  }
  if (!STATUS_RE.test(heading.block)) {
    throw new Error(`Heading ${kebab} has no Status line.`);
  }
  heading.block = heading.block.replace(STATUS_RE, `- **Status:** ${status}`);
  heading.status = status;
  return serializeInbox(parsed);
}
