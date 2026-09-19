const ESC = "\u001B[";
const CLEAR_MODERN = `${ESC}2J${ESC}3J${ESC}H`;
const CLEAR_LEGACY = `${ESC}2J${ESC}0f`;
const SYNC_BEGIN = `${ESC}?2026h`;
const SYNC_END = `${ESC}?2026l`;

export function extractClearedFrame(text: string): string | null {
  if (text.startsWith(CLEAR_MODERN)) {
    return text.slice(CLEAR_MODERN.length);
  }
  if (text.startsWith(CLEAR_LEGACY)) {
    return text.slice(CLEAR_LEGACY.length);
  }
  return null;
}

export function splitFrame(frame: string): string[] {
  const lines = frame.split("\n");
  if (lines.length > 0 && lines[lines.length - 1] === "") {
    lines.pop();
  }
  return lines;
}

export function patchChangedLines(previous: string[], next: string[]): string | null {
  if (previous.length !== next.length) {
    return null;
  }
  let patch = SYNC_BEGIN;
  let changed = 0;
  for (let i = 0; i < next.length; i++) {
    if (previous[i] === next[i]) {
      continue;
    }
    changed += 1;
    patch += `${ESC}${i + 1};1H${ESC}2K${next[i] ?? ""}`;
  }
  if (changed === 0) {
    return "";
  }
  if (changed === next.length) {
    return null;
  }
  patch += `${ESC}${next.length};1H${SYNC_END}`;
  return patch;
}

function chunkToString(chunk: unknown, encoding?: BufferEncoding | string): string {
  if (typeof chunk === "string") {
    return chunk;
  }
  if (Buffer.isBuffer(chunk)) {
    return chunk.toString((encoding as BufferEncoding | undefined) ?? "utf8");
  }
  return String(chunk);
}

export function createFrameDiffStdout(stream: NodeJS.WriteStream): NodeJS.WriteStream {
  const origWrite = stream.write.bind(stream) as NodeJS.WriteStream["write"];
  let previous: string[] | null = null;

  const write: NodeJS.WriteStream["write"] = ((
    chunk: unknown,
    encoding?: BufferEncoding | ((err?: Error | null) => void),
    cb?: (err?: Error | null) => void,
  ) => {
    const done = typeof encoding === "function" ? encoding : cb;
    const enc = typeof encoding === "string" ? encoding : undefined;
    const text = chunkToString(chunk, enc);
    const frame = extractClearedFrame(text);
    if (frame === null) {
      previous = null;
      if (typeof encoding === "function") {
        return origWrite(chunk as string | Uint8Array, encoding);
      }
      return origWrite(chunk as string | Uint8Array, encoding as BufferEncoding, cb);
    }
    const lines = splitFrame(frame);
    const patch = previous ? patchChangedLines(previous, lines) : null;
    previous = lines;
    if (patch === null) {
      if (typeof encoding === "function") {
        return origWrite(chunk as string | Uint8Array, encoding);
      }
      return origWrite(chunk as string | Uint8Array, encoding as BufferEncoding, cb);
    }
    if (patch === "") {
      done?.(null);
      return true;
    }
    return origWrite(patch, "utf8", done);
  }) as NodeJS.WriteStream["write"];

  return new Proxy(stream, {
    get(target, prop, receiver) {
      if (prop === "write") {
        return write;
      }
      const value = Reflect.get(target, prop, receiver);
      return typeof value === "function" ? (value as (...args: unknown[]) => unknown).bind(target) : value;
    },
  });
}
