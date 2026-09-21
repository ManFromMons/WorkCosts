import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { invertQueue, parseQueueOutput, parseSeqLookup } from "./queue.ts";

describe("parseQueueOutput", () => {
  it("reads tree rows and startable notes", () => {
    const items = parseQueueOutput(`Work queue (Seq / dependency tree)

[1] paste-html - Paste HTML  (done)

[5] product-extra-data - Product extra data  (done)
    +- [2] source-eurocarparts - Source Euro Car Parts  (done)
    -- [7] unsaved-changes-prompt - Unsaved changes prompt  (ready-for-agent)  [startable; inbox ready-for-review]
`);
    assert.equal(items.length, 4);
    assert.equal(items[0].kebab, "paste-html");
    assert.equal(items[2].seq, "2");
    assert.equal(items[3].startable, true);
    assert.equal(items[3].status, "ready-for-agent");
  });

  it("parses deeper tree prefixes that include pipes", () => {
    const items = parseQueueOutput("        |  +- [9] nested-story - Nested  (draft)\n");
    assert.equal(items.length, 1);
    assert.equal(items[0].kebab, "nested-story");
    assert.equal(items[0].seq, "9");
  });
});

describe("invertQueue", () => {
  it("puts the last known tree row first and keeps unknown Seq at the bottom", () => {
    const items = parseQueueOutput(`
[1] paste-html - Paste HTML  (done)
    +- [2] source-eurocarparts - Source Euro Car Parts  (done)
[11] 11-cars - Cars  (ready-for-agent)
[?] job-concept - Superseded  (unknown)
`);
    const inverted = invertQueue(items);
    assert.equal(inverted.map((item) => item.kebab).join(","), "11-cars,source-eurocarparts,paste-html,job-concept");
  });
});

describe("parseSeqLookup", () => {
  it("parses KEY=value lines", () => {
    const map = parseSeqLookup("SEQ=7\nKEBAB=unsaved-changes-prompt\nSTARTABLE=true\n");
    assert.equal(map.KEBAB, "unsaved-changes-prompt");
    assert.equal(map.STARTABLE, "true");
  });
});
