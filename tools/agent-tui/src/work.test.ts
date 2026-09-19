import assert from "node:assert/strict";
import { describe, it } from "node:test";
import {
  isActiveWork,
  kebabFromFeaturePath,
  matchKebabFromBranch,
  mergeWorkItems,
  parseFeatureRefLines,
  parseNameOnlyLog,
  relativeTime,
} from "./work.ts";

describe("kebabFromFeaturePath", () => {
  it("reads story files and skips inbox plus delivery", () => {
    assert.equal(kebabFromFeaturePath("docs/features/paste-html.md"), "paste-html");
    assert.equal(kebabFromFeaturePath("docs/features/to-review.md"), null);
    assert.equal(kebabFromFeaturePath("docs/features/paste-html-delivery.md"), null);
  });
});

describe("matchKebabFromBranch", () => {
  it("picks the longest kebab prefix after feature/", () => {
    const kebabs = ["source-demon-tweeks", "source-demo", "paste-html"];
    assert.equal(matchKebabFromBranch("feature/source-demon-tweeks-Halfords", kebabs), "source-demon-tweeks");
    assert.equal(matchKebabFromBranch("origin/feature/paste-html-Paste-HTML", kebabs), "paste-html");
    assert.equal(matchKebabFromBranch("Planning", kebabs), null);
  });
});

describe("parseNameOnlyLog", () => {
  it("keeps the first (newest) commit date per kebab", () => {
    const map = parseNameOnlyLog(`2026-09-19T18:00:00+01:00
docs/features/unsaved-changes-prompt.md
docs/features/unsaved-changes-prompt-delivery.md

2026-09-01T10:00:00+01:00
docs/features/unsaved-changes-prompt.md
docs/features/paste-html.md
`);
    assert.equal(map.get("unsaved-changes-prompt"), Date.parse("2026-09-19T18:00:00+01:00"));
    assert.equal(map.get("paste-html"), Date.parse("2026-09-01T10:00:00+01:00"));
    assert.equal(map.has("unsaved-changes-prompt-delivery"), false);
  });
});

describe("parseFeatureRefLines", () => {
  it("maps branch lines to kebabs", () => {
    const rows = parseFeatureRefLines(
      "2026-09-19T12:00:00+01:00\tfeature/paste-html-Paste-HTML\n",
      ["paste-html"],
    );
    assert.equal(rows.length, 1);
    assert.equal(rows[0].kebab, "paste-html");
    assert.equal(rows[0].branch, "feature/paste-html-Paste-HTML");
  });
});

describe("mergeWorkItems", () => {
  it("sorts newest first and prefers dirty timestamps", () => {
    const items = mergeWorkItems({
      commits: new Map([["paste-html", Date.parse("2026-01-01T00:00:00Z")]]),
      branches: [{ kebab: "source-demo", atMs: Date.parse("2026-02-01T00:00:00Z"), branch: "feature/source-demo-X" }],
      dirty: [{ kebab: "paste-html", atMs: Date.parse("2026-03-01T00:00:00Z") }],
      currentBranch: "feature/source-demo-X",
      titles: new Map([["paste-html", "Paste HTML"], ["source-demo", "Demo"]]),
    });
    assert.equal(items[0].kebab, "paste-html");
    assert.equal(items[0].source, "dirty");
    assert.equal(items[1].kebab, "source-demo");
    assert.equal(items[1].current, true);
  });
});

describe("isActiveWork", () => {
  it("includes branches, dirty files, and unfinished inbox/story status", () => {
    const base = { kebab: "x", title: "x", atMs: 1, branch: null, current: false, source: "commit" as const };
    assert.equal(isActiveWork(base, "done", undefined), false);
    assert.equal(isActiveWork(base, "ready-for-agent", undefined), true);
    assert.equal(isActiveWork({ ...base, branch: "feature/x-Y" }, "done", "done"), true);
    assert.equal(isActiveWork({ ...base, source: "dirty" }, "done", "done"), true);
    assert.equal(isActiveWork(base, "done", "ready-for-review"), true);
  });
});

describe("relativeTime", () => {
  it("formats short deltas", () => {
    const now = Date.parse("2026-09-19T12:00:00Z");
    assert.equal(relativeTime(now - 30_000, now), "now");
    assert.equal(relativeTime(now - 5 * 60_000, now), "5m");
    assert.equal(relativeTime(now - 3 * 60 * 60_000, now), "3h");
    assert.equal(relativeTime(now - 3 * 24 * 60 * 60_000, now), "3d");
  });
});
