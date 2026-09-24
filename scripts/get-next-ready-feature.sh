#!/usr/bin/env bash
# Next ready-for-agent kebab on origin/main, or QUEUE_EMPTY.
set -euo pipefail

REMOTE="${1:-origin}"
MAIN_BRANCH="${2:-main}"
PREFIX="docs/features/"

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"
git fetch "$REMOTE"
tree="$REMOTE/$MAIN_BRANCH"

header() {
  local md="$1" label="$2"
  printf '%s\n' "$md" | sed -n "s/^- \*\*${label}:\*\*[[:space:]]*//p" | head -n1 | awk '{print $1}'
}

kebab_from_id() {
  local id="$1" name="$2"
  if [[ "$id" =~ docs/features/([^.\`[:space:]]+)\.md ]]; then
    echo "${BASH_REMATCH[1]}"
  else
    echo "${name%.md}"
  fi
}

declare -A STATUS SEQ DEPS

while IFS= read -r path; do
  [[ -z "$path" ]] && continue
  name="$(basename "$path")"
  [[ "$name" == to-review.md || "$name" == *-delivery.md || "$name" != *.md ]] && continue
  md="$(git show "$tree:$path" 2>/dev/null || true)"
  [[ -z "$md" ]] && continue
  id="$(printf '%s\n' "$md" | sed -n 's/^- \*\*Id:\*\*[[:space:]]*//p' | head -n1)"
  kebab="$(kebab_from_id "$id" "$name")"
  seq="$(header "$md" Seq)"
  [[ "$seq" =~ ^[0-9]+$ ]] || seq=999999
  st="$(header "$md" Status)"
  dep_raw="$(printf '%s\n' "$md" | sed -n 's/^- \*\*Depends-on:\*\*[[:space:]]*//p' | head -n1)"
  STATUS["$kebab"]="$st"
  SEQ["$kebab"]="$seq"
  DEPS["$kebab"]="$dep_raw"
done < <(git ls-tree --name-only "$tree" "$PREFIX")

eligible=()
for kebab in "${!STATUS[@]}"; do
  [[ "${STATUS[$kebab]}" == ready-for-agent ]] || continue
  blocked=0
  raw="${DEPS[$kebab]}"
  if [[ -n "$raw" && "$raw" != none && "$raw" != $'\u2014' ]]; then
    IFS=',' read -ra parts <<<"$raw"
    for part in "${parts[@]}"; do
      token="$(echo "$part" | xargs | tr -d '`')"
      [[ -z "$token" || "$token" == none ]] && continue
      dep="$token"
      if [[ "$token" == docs/features/* ]]; then
        dep="${token#docs/features/}"
        dep="${dep%.md}"
      fi
      if [[ -z "${STATUS[$dep]+x}" || "${STATUS[$dep]}" != done ]]; then
        blocked=1
        break
      fi
    done
  fi
  [[ "$blocked" -eq 0 ]] && eligible+=("${SEQ[$kebab]} $kebab")
done

if ((${#eligible[@]} == 0)); then
  echo QUEUE_EMPTY
  exit 0
fi
printf '%s\n' "${eligible[@]}" | sort -n | head -n1 | awk '{print $2}'
