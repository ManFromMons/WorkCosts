import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { extractClearedFrame, patchChangedLines, splitFrame } from "./frameDiffStdout.ts";

describe("frameDiffStdout", () => {
  it("extracts the frame after a modern clear-terminal prefix", () => {
    const frame = "hello\nworld\n";
    const raw = `\u001B[2J\u001B[3J\u001B[H${frame}`;
    assert.equal(extractClearedFrame(raw), frame);
  });

  it("returns null when the write is not a cleared frame", () => {
    assert.equal(extractClearedFrame("plain text"), null);
  });

  it("patches only the changed line", () => {
    const previous = ["aaa", "bbb", "ccc"];
    const next = ["aaa", "bX", "ccc"];
    const patch = patchChangedLines(previous, next);
    assert.ok(patch);
    assert.match(patch, /\u001B\[2;1H\u001B\[2KbX/);
    assert.doesNotMatch(patch, /\u001B\[1;1H/);
    assert.doesNotMatch(patch, /\u001B\[3;1H\u001B\[2K/);
  });

  it("falls back when every line changed", () => {
    assert.equal(patchChangedLines(["a", "b"], ["c", "d"]), null);
  });

  it("drops a trailing empty line from the frame", () => {
    assert.deepEqual(splitFrame("one\ntwo\n"), ["one", "two"]);
  });
});
