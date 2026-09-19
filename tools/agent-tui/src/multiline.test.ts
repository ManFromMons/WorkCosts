import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { layoutMultiline, rowColToIndex } from "./multiline.ts";

describe("layoutMultiline", () => {
  it("wraps at width and tracks the cursor", () => {
    const layout = layoutMultiline("abcdefgh", 4, 6);
    assert.deepEqual(layout.lines, ["abcd", "efgh"]);
    assert.equal(layout.cursorRow, 1);
    assert.equal(layout.cursorCol, 2);
  });

  it("treats newline as a hard break", () => {
    const layout = layoutMultiline("ab\ncd", 8, 3);
    assert.deepEqual(layout.lines, ["ab", "cd"]);
    assert.equal(layout.cursorRow, 1);
    assert.equal(layout.cursorCol, 0);
  });

  it("maps a row/col back to an index", () => {
    assert.equal(rowColToIndex("abcdefgh", 4, 1, 2), 6);
    assert.equal(rowColToIndex("ab\ncd", 8, 1, 1), 4);
  });
});
