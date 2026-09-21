## &lt;seq&gt;-&lt;kebab-case-name&gt;

- **Feature:** [docs/features/&lt;seq&gt;-&lt;kebab-case-name&gt;.md](&lt;seq&gt;-&lt;kebab-case-name&gt;.md)
- **Seq:** &lt;integer, matches the filename prefix and the story header&gt;
- **Status:** in-progress | blocked | resume | ready-for-review | done
- **Change set:** branch `feature/&lt;feature_code&gt;-&lt;Title&gt;` — PR url (open the PR at `ready-for-review`)
- **Last note:** one line. Not a substitute for **Work summary**.

### Work summary

Required at **Status** `ready-for-review` (and kept when the heading becomes `done`). Bullets of what landed: user-visible behaviour and technical pieces. Not a chat log. Not only **Last note**.

- …

### Questions

- [ ] Q1. *Assumption:* … → **Question:** …?
- [x] Q2. … **Answer:** …

Write `_(none)_` only when there are no questions.

### Deviations to scan

- [ ] Used existing `Type.Method` instead of a new helper named in the spec.

List every reuse, schema, branch-name, or spec mismatch. Write `_(none)_` only when there are none. Never hide a real deviation behind `_(none)_`.

### Verify

- [ ] Tests from the feature file passed
- [ ] Deviations accepted
