---
name: start-add-source
description: Kickoff for adding a supplier website. Invoke with a source story id (source-halfords) or a product URL to plan then implement. Discovers fetch, writes fixtures/tests, then integrates the parser.
disable-model-invocation: true
---

# Start add source

Kickoff only. Then follow skill `add-product-source` and `implement-feature`.

## Which story

- If the user named `docs/features/source-<host>.md` (or `source-halfords`), use that file. It must be `ready-for-agent` (URL + expected Name + GBP price).
- If they gave a **URL** but no story, stop and use `plan-feature`: copy [template.md](../add-product-source/template.md) to `docs/features/source-<host>.md`, fill parameters, assign **Seq**, leave **Status** `draft` until Name and price are in the file. Do not implement in that planning pass unless they already supplied those expected values.
- If they named nothing, do not guess a host. Ask for a product URL.

## Kickoff (ready-for-agent story)

1. `git fetch origin`. Read the story, `AGENTS.md`, `docs/parsing/adding-a-source.md`, `docs/parsing/overview.md`, `docs/parsing/browser-session.md`.
2. Branch `feature/source-<host>-<Title>` from `origin/main`. Not `Planning`, not `main`.
3. Discover HttpClient vs Chromium; capture/trim fixture; write failing tests for Name and UnitPrice; then integrate detector/parser/fetch as in `add-product-source`.
4. Commit buildable units. `dotnet test WorkCosts.slnx --settings .runsettings`.
5. Inbox on `main` via `update-to-review` / `Update-ToReviewOnMain.ps1`. No PR until that heading is **Status** `done`.
