export type MultilineLayout = {
  lines: string[];
  cursorRow: number;
  cursorCol: number;
};

export function layoutMultiline(value: string, width: number, cursor: number): MultilineLayout {
  const w = Math.max(1, width);
  const lines: string[] = [""];
  let row = 0;
  let col = 0;
  let cursorRow = 0;
  let cursorCol = 0;
  const mark = (index: number) => {
    if (index === cursor) {
      cursorRow = row;
      cursorCol = col;
    }
  };
  const pushChar = (ch: string) => {
    if (ch === "\n") {
      lines.push("");
      row += 1;
      col = 0;
      return;
    }
    if (col >= w) {
      lines.push("");
      row += 1;
      col = 0;
    }
    lines[row] += ch;
    col += 1;
  };
  mark(0);
  for (let i = 0; i < value.length; i++) {
    pushChar(value[i] ?? "");
    mark(i + 1);
  }
  if (cursorCol >= w) {
    lines.push("");
    cursorRow += 1;
    cursorCol = 0;
  }
  return { lines, cursorRow, cursorCol };
}

export function rowColToIndex(value: string, width: number, row: number, col: number): number {
  const w = Math.max(1, width);
  let r = 0;
  let c = 0;
  for (let i = 0; i < value.length; i++) {
    if (r === row && c >= col) {
      return i;
    }
    const ch = value[i];
    if (ch === "\n") {
      if (r === row) {
        return i;
      }
      r += 1;
      c = 0;
      continue;
    }
    if (c >= w) {
      r += 1;
      c = 0;
    }
    if (r === row && c >= col) {
      return i;
    }
    c += 1;
  }
  return value.length;
}

export function clampCursor(value: string, cursor: number): number {
  if (cursor < 0) {
    return 0;
  }
  if (cursor > value.length) {
    return value.length;
  }
  return cursor;
}
