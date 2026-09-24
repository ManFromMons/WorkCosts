#!/usr/bin/env bash
# Work queue tree (working-tree overlay on origin/main). Hides nothing here;
# the GTK board omits GNOME product-port slices and job-concept itself.
set -euo pipefail
REMOTE="${1:-origin}"
MAIN_BRANCH="${2:-main}"
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"
git fetch "$REMOTE" || true
echo "Work queue (Seq / dependency tree)"
echo "Source: working tree overlay on $REMOTE/$MAIN_BRANCH"
echo
# Prefer C# catalogue when the board is built; otherwise print paths.
if [[ -x "$repo_root/tools/agent-board/AgentBoard/bin/Debug/net9.0/AgentBoard" ]]; then
  echo "(Use the Agent board Queue pane for membership, sort, and hide-done.)"
fi
git ls-tree --name-only "$REMOTE/$MAIN_BRANCH" docs/features/ | while read -r path; do
  name="$(basename "$path")"
  [[ "$name" == to-review.md || "$name" == *-delivery.md ]] && continue
  [[ "$name" == *.md ]] || continue
  md="$(git show "$REMOTE/$MAIN_BRANCH:$path" 2>/dev/null || true)"
  seq="$(printf '%s\n' "$md" | sed -n 's/^- \*\*Seq:\*\*[[:space:]]*//p' | head -n1 | awk '{print $1}')"
  st="$(printf '%s\n' "$md" | sed -n 's/^- \*\*Status:\*\*[[:space:]]*//p' | head -n1 | awk '{print $1}')"
  kebab="${name%.md}"
  echo "[${seq:--}] $kebab  ($st)"
done
echo
echo "Next pickup (origin/main): $(bash "$repo_root/scripts/get-next-ready-feature.sh" "$REMOTE" "$MAIN_BRANCH")"
