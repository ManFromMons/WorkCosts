export type QueueItem = {
  seq: string;
  kebab: string;
  title: string;
  status: string;
  notes: string;
  startable: boolean;
  raw: string;
  indent: string;
};

const STORY_LINE =
  /^(?<indent>.*?)\s*\[(?<seq>\d+|\?)\]\s+(?<kebab>\S+)\s+-\s+(?<title>.+?)\s+\((?<status>[^)]+)\)(?<notes>.*)$/;

export function parseQueueOutput(text: string): QueueItem[] {
  const items: QueueItem[] = [];
  for (const line of text.split(/\r?\n/)) {
    const match = STORY_LINE.exec(line);
    if (!match || !match.groups) {
      continue;
    }
    const notes = match.groups.notes.trim();
    items.push({
      seq: match.groups.seq,
      kebab: match.groups.kebab,
      title: match.groups.title.trim(),
      status: match.groups.status.trim(),
      notes,
      startable: /\[startable/i.test(notes) || /\bstartable\b/i.test(notes),
      raw: line,
      indent: match.groups.indent,
    });
  }
  return items;
}

export function parseSeqLookup(text: string): Record<string, string> {
  const map: Record<string, string> = {};
  for (const line of text.split(/\r?\n/)) {
    const eq = line.indexOf("=");
    if (eq <= 0) {
      continue;
    }
    map[line.slice(0, eq)] = line.slice(eq + 1);
  }
  return map;
}
