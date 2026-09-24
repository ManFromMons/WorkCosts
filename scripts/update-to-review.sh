#!/usr/bin/env bash
# Commit docs/features/to-review.md on main only, push main, return to the previous branch.
# Never force-pushes main. Never uses cmd.exe.
set -euo pipefail

MAIN_BRANCH="${1:-main}"
REMOTE="${2:-origin}"
MESSAGE="${3:-Update docs/features/to-review.md.}"
TO_REVIEW_REL="docs/features/to-review.md"

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

mapfile -t dirty < <(git status --porcelain -uall | awk '{print substr($0,4)}' | sed 's/.* -> //; s/"//g' | awk 'NF && !seen[$0]++')
unexpected=()
for p in "${dirty[@]+"${dirty[@]}"}"; do
  [[ "$p" == "$TO_REVIEW_REL" ]] && continue
  unexpected+=("$p")
done
if ((${#unexpected[@]})); then
  echo "Working tree has uncommitted code. Extra dirty paths: ${unexpected[*]}" >&2
  exit 1
fi
if ((${#dirty[@]} == 0)); then
  echo "Nothing to land. Edit $TO_REVIEW_REL (uncommitted), then run this script." >&2
  exit 1
fi
if [[ ! -f "$TO_REVIEW_REL" ]]; then
  echo "Expected payload at $TO_REVIEW_REL" >&2
  exit 1
fi

current="$(git branch --show-current)"
[[ -n "$current" ]] || { echo "Detached HEAD." >&2; exit 1; }

git fetch "$REMOTE"
payload="$(mktemp "${TMPDIR:-/tmp}/workcosts-toreview-XXXXXX.md")"
cp "$TO_REVIEW_REL" "$payload"

cleanup() { rm -f "$payload"; }
trap cleanup EXIT

if [[ "$current" != "$MAIN_BRANCH" ]]; then
  if git ls-files --error-unmatch -- "$TO_REVIEW_REL" >/dev/null 2>&1; then
    git checkout -- "$TO_REVIEW_REL"
  elif [[ -f "$TO_REVIEW_REL" ]]; then
    rm -f "$TO_REVIEW_REL"
  fi
  git checkout "$MAIN_BRANCH"
fi

if git rev-parse --verify --quiet "$REMOTE/$MAIN_BRANCH" >/dev/null; then
  git merge --ff-only "$REMOTE/$MAIN_BRANCH"
fi

mkdir -p "$(dirname "$TO_REVIEW_REL")"
cp "$payload" "$TO_REVIEW_REL"
git add -- "$TO_REVIEW_REL"
if git diff --cached --quiet -- "$TO_REVIEW_REL"; then
  echo "$TO_REVIEW_REL on $MAIN_BRANCH is already up to date."
  git reset HEAD -- "$TO_REVIEW_REL"
else
  git commit -m "$MESSAGE"
  echo "Pushing $MAIN_BRANCH (no force)..."
  git push "$REMOTE" "$MAIN_BRANCH"
fi

if [[ "$current" != "$MAIN_BRANCH" ]]; then
  git checkout "$current"
  if git ls-files --error-unmatch -- "$TO_REVIEW_REL" >/dev/null 2>&1; then
    git checkout -- "$TO_REVIEW_REL"
  elif [[ -f "$TO_REVIEW_REL" ]]; then
    rm -f "$TO_REVIEW_REL"
  fi
fi

echo "Done. Inbox is on $MAIN_BRANCH."
git status -sb
