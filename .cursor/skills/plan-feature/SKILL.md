---
name: plan-feature
description: Plan a Will I DIY? feature into a source-of-truth markdown file under docs/features/. Use when the user wants to plan, spec, or design a feature, add product behaviour, or go through objectives, requirements, layout, and workflow before coding.
---

# Plan a feature

Planning produces a **feature**, not a chat summary. The source of truth is one file:

`docs/features/<kebab-case-name>.md`

Copy the structure from [template.md](template.md). Do not implement the feature until the user asks and that file is complete enough for an unsupervised agent. Implementation is skill `implement-feature`, not this skill.

## Workflow

1. Read `AGENTS.md`, `docs/layout-grammar.md`, and any screen/parsing docs the feature touches. Search the codebase for existing types, controls, and services.
2. Create or update `docs/features/<name>.md`. Fill every section in the template. Name files after the user-facing feature (`paste-html`, `zip-export-import`), not tickets.
3. **Assumptions become questions.** If you assumed a control, class, merge rule, or shortcut, write a numbered question that states the assumption. Stop and wait for answers before treating the spec as done.
4. After answers, rewrite the feature file. Remove resolved questions. List only leftover mechanical defaults under **Accepted defaults**.
5. Completeness bar: another agent could implement from this file **with no further questions** and get a predictable, acceptable result.

One feature = one file. Two features = two files (plan them in one session if the user wants both, still write two sources of truth).

## Required sections (in order)

Use the headings in [template.md](template.md):

- **Objectives** — what success is; what is out of scope
- **User requirements** — observable behaviour, including empty/error/cancel
- **Layout** — regions, size classes (regular vs stack), existing grammar (`docs/layout-grammar.md`, `docs/screens/`)
- **Workflow** — step-by-step, including keyboard and which dialog vs sheet
- **Technical design** — exact reuse vs new types (see below)
- **Tests** — names and assertions
- **Open questions** — only unanswered items, each tied to an assumption
- **Accepted defaults** — choices you made that do not change UX/architecture
- **Implementation notes for an agent** — files to touch, order of work, what not to do

## Technical design rules

Specify in detail where it matters:

- Name existing **classes, interfaces, user controls, pages, helpers** to call (`ProductImagePicker`, `DialogHelper`, `IProductPageParser`, `WebCacheStore`, `MarkdownEditor`, …).
- If behaviour already exists, **do not duplicate it**. Prefer constructor injection / existing static helpers / `App.Database` as the project already does. Say how the new code is wired.
- Say when to **create** a user control, service, or interface — and when a method on an existing type is enough.
- Call out DI vs new instance explicitly (this app often uses `new DatabaseService()` / `App.Database`; do not invent a DI container unless the feature requires it).
- Windows WinUI is the first implementation target unless the feature file says otherwise. GNOME/iPad notes belong in **Ports**, not as a second spec.

## Do not

- Implement during planning unless the user explicitly says to build it.
- Leave “TBD” in layout or workflow.
- Invent a second product or a new navigation destination without a question.
- Put the source of truth only in chat or `PLANNING.md`.
