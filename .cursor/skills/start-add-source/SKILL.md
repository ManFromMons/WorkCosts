---
name: start-add-source
description: Kickoff for adding a supplier website. Invoke with a source story id (source-halfords) or a product URL. URL starts an interactive confirm of at least three pages, then a story; a ready story implements (fixtures, tests, parser).
disable-model-invocation: true
---

# Start add source

Kickoff only. Planning a URL follows [confirm-samples.md](../add-product-source/confirm-samples.md). A ready story follows skill `add-product-source` and `implement-feature`.

## Which path

- **Ready story** (`source-halfords` or `docs/features/source-<host>.md` with **Status** `ready-for-agent` and three confirmed samples): implement. See Kickoff below.
- **URL, incomplete story, or draft:** do **not** implement. Check out **`Planning`**. Follow [confirm-samples.md](../add-product-source/confirm-samples.md) (ask the user to confirm Name and GBP price per page; collect **at least three** product URLs). Then `plan-feature` writes `docs/features/source-<host>.md`. Stop until they ask to implement.
- **Nothing named:** ask for a product URL. Do not guess a host.

## Kickoff (ready-for-agent story)

1. `git fetch origin`. Read the story, `AGENTS.md`, `docs/parsing/adding-a-source.md`, `docs/parsing/overview.md`, `docs/parsing/browser-session.md`.
2. Branch `feature/source-<host>-<Title>` from `origin/main`. Not `Planning`, not `main`.
3. Discover HttpClient vs Chromium; capture/trim **one fixture per sample URL**; write failing tests for Name and UnitPrice on **all** samples; then integrate detector/parser/fetch as in `add-product-source`.
4. Commit buildable units. `dotnet test WorkCosts.slnx --settings .runsettings`.
5. Inbox on `main` via `update-to-review` / `Update-ToReviewOnMain.ps1`. When tests pass, set that heading **Status** to `ready-for-review`. No PR until the heading is **Status** `done`.
