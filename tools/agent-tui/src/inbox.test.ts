import assert from "node:assert/strict";
import { describe, it } from "node:test";
import {
  cycleInboxStatus,
  parseInbox,
  setHeadingStatus,
  toggleCheckbox,
} from "./inbox.ts";

const sample = `# Feature implementation review

## Entries


## source-demo

- **Feature:** [docs/features/source-demo.md](source-demo.md)
- **Status:** ready-for-review
- **Last note:** Tests passed.

### Questions

_(none)_

### Deviations to scan

- [ ] Used an existing helper.

### Verify

- [x] Tests from the feature file passed
- [ ] Deviations accepted

## other-story

- **Feature:** [docs/features/other-story.md](other-story.md)
- **Status:** in-progress
- **Last note:** Started.

### Questions

_(none)_

### Deviations to scan

- [ ] Something else.

### Verify

- [ ] Tests from the feature file passed
- [ ] Deviations accepted
`;

describe("parseInbox", () => {
  it("splits kebab headings and reads status plus checkboxes", () => {
    const parsed = parseInbox(sample);
    assert.equal(parsed.headings.length, 2);
    assert.equal(parsed.headings[0].kebab, "source-demo");
    assert.equal(parsed.headings[0].status, "ready-for-review");
    assert.equal(parsed.headings[0].checkboxes.length, 3);
    assert.equal(parsed.headings[0].checkboxes[0].checked, false);
    assert.equal(parsed.headings[0].checkboxes[1].checked, true);
    assert.equal(parsed.headings[1].kebab, "other-story");
  });

  it("skips ## Entries", () => {
    const parsed = parseInbox(sample);
    assert.ok(!parsed.headings.some((h) => h.kebab === "entries"));
  });
});

describe("toggleCheckbox", () => {
  it("flips the selected box on the named heading only", () => {
    const next = toggleCheckbox(sample, "source-demo", 0);
    const parsed = parseInbox(next);
    const heading = parsed.headings[0];
    assert.equal(heading.checkboxes[0].checked, true);
    assert.equal(heading.checkboxes[2].checked, false);
    assert.equal(parsed.headings[1].checkboxes[0].checked, false);
  });

  it("throws when the heading is missing", () => {
    assert.throws(() => toggleCheckbox(sample, "nope", 0), /No inbox heading nope/);
  });

  it("throws when the checkbox index is missing", () => {
    assert.throws(() => toggleCheckbox(sample, "source-demo", 9), /No checkbox 9/);
  });
});

describe("setHeadingStatus", () => {
  it("replaces the Status line", () => {
    const next = setHeadingStatus(sample, "source-demo", "done");
    const parsed = parseInbox(next);
    assert.equal(parsed.headings[0].status, "done");
    assert.equal(parsed.headings[1].status, "in-progress");
    assert.match(next, /- \*\*Status:\*\* done/);
  });

  it("throws when the heading is missing", () => {
    assert.throws(() => setHeadingStatus(sample, "missing", "done"), /No inbox heading missing/);
  });

  it("throws when the heading has no Status line", () => {
    const bare = "## no-status\n\n- [ ] box\n";
    assert.throws(() => setHeadingStatus(bare, "no-status", "done"), /has no Status line/);
  });
});

describe("cycleInboxStatus", () => {
  it("walks in-progress, ready-for-review, done", () => {
    assert.equal(cycleInboxStatus("in-progress"), "ready-for-review");
    assert.equal(cycleInboxStatus("ready-for-review"), "done");
    assert.equal(cycleInboxStatus("done"), "in-progress");
  });

  it("maps blocked to resume", () => {
    assert.equal(cycleInboxStatus("blocked"), "resume");
    assert.equal(cycleInboxStatus("resume"), "in-progress");
  });
});
