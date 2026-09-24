#!/usr/bin/env bash
# Rebase Planning onto main, squash, fast-forward main, push both.
# Never merge-commit onto main. Never force-push main. Never uses cmd.exe.
set -euo pipefail

PLANNING_BRANCH="${PLANNING_BRANCH:-Planning}"
MAIN_BRANCH="${MAIN_BRANCH:-main}"
REMOTE="${REMOTE:-origin}"
MESSAGE="${1:-}"

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

if [[ -n "$(git status --porcelain)" ]]; then
  echo "Working tree is not clean. Commit or stash before merging $PLANNING_BRANCH into $MAIN_BRANCH." >&2
  exit 1
fi

git fetch "$REMOTE"
to_review="docs/features/to-review.md"
saved="$(mktemp "${TMPDIR:-/tmp}/workcosts-main-toreview-XXXXXX.md")"
had=0

git checkout "$MAIN_BRANCH"
if git rev-parse --verify --quiet "$REMOTE/$MAIN_BRANCH" >/dev/null; then
  git merge --ff-only "$REMOTE/$MAIN_BRANCH"
fi
if git cat-file -e "HEAD:$to_review" 2>/dev/null; then
  cp "$to_review" "$saved"
  had=1
fi

git checkout "$PLANNING_BRANCH"
if ! git rebase "$MAIN_BRANCH"; then
  echo "Rebase failed. Aborting rebase." >&2
  git rebase --abort || true
  exit 1
fi

ahead="$(git rev-list --count "$MAIN_BRANCH"..HEAD)"
echo "$PLANNING_BRANCH is $ahead commit(s) ahead of $MAIN_BRANCH after rebase."
if [[ "$ahead" -gt 1 ]]; then
  if [[ -z "$MESSAGE" ]]; then
    MESSAGE="Apply Planning branch."
  fi
  git reset --soft "$MAIN_BRANCH"
  git commit -m "$MESSAGE"
  ahead=1
elif [[ "$ahead" -eq 1 && -n "$MESSAGE" ]]; then
  git commit --amend -m "$MESSAGE"
fi

git checkout "$MAIN_BRANCH"
if [[ "$ahead" -eq 0 ]]; then
  echo "Nothing to merge."
else
  git merge --ff-only "$PLANNING_BRANCH"
fi

if [[ "$had" -eq 1 ]]; then
  mkdir -p "$(dirname "$to_review")"
  cp "$saved" "$to_review"
  git add -- "$to_review"
  if ! git diff --cached --quiet -- "$to_review"; then
    git commit -m "Keep docs/features/to-review.md on main."
  fi
fi
rm -f "$saved"

echo "Pushing $MAIN_BRANCH (no force)..."
git push "$REMOTE" "$MAIN_BRANCH"
echo "Pushing $PLANNING_BRANCH (--force-with-lease)..."
git push --force-with-lease "$REMOTE" "$PLANNING_BRANCH"
git status -sb
