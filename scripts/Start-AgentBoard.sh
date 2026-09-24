#!/usr/bin/env bash
# Start the unpackaged GTK4 + libadwaita Agent board (Gir.Core).
set -euo pipefail
repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"
export WORKCOSTS_ROOT="$repo_root"
if ! pkg-config --exists gtk4 2>/dev/null && ! ldconfig -p 2>/dev/null | grep -q libgtk-4; then
  echo "GTK4 is required. Install gtk4 and libadwaita (Debian/Ubuntu: gir1.2-gtk-4.0 libadwaita-1-0)." >&2
  exit 1
fi
if ! ldconfig -p 2>/dev/null | grep -q libadwaita && ! pkg-config --exists libadwaita-1 2>/dev/null; then
  echo "libadwaita is required. Install libadwaita-1-0 / libadwaita. This is an unpackaged developer tool (not a Flatpak)." >&2
  exit 1
fi
dotnet run --project "$repo_root/tools/agent-board/AgentBoard/AgentBoard.csproj" --no-launch-profile
