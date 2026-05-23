#!/usr/bin/env bash
set -euo pipefail

# ilspycmd targets .NET 8 but we only have .NET 9+ — allow roll-forward
export DOTNET_ROLL_FORWARD=LatestMajor

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME_DIR="${1:-}"

if [ -z "$GAME_DIR" ]; then
    STEAM_ROOT="/c/Program Files (x86)/Steam"
    VDF="$STEAM_ROOT/steamapps/libraryfolders.vdf"
    GAME_DIR=$(grep '"path"' "$VDF" 2>/dev/null \
        | sed 's/.*"\(.*\)".*/\1/' \
        | sed 's|\\\\|/|g' \
        | while read -r lib; do
            candidate="$lib/steamapps/common/Slay the Spire 2"
            [ -d "$candidate" ] && echo "$candidate" && break
          done || true)
    GAME_DIR="${GAME_DIR:-$STEAM_ROOT/steamapps/common/Slay the Spire 2}"
fi

DLL="$GAME_DIR/data_sts2_windows_x86_64/sts2.dll"

if [ ! -f "$DLL" ]; then
    echo "Error: could not find sts2.dll at $DLL"
    echo "Pass the game directory as an argument: bash scripts/decompile.sh \"/path/to/Slay the Spire 2\""
    exit 1
fi

HASH=$(sha256sum "$DLL" | awk '{print $1}')
STORED=$(cat "$REPO_ROOT/decompiled/.version" 2>/dev/null || echo "")

if [ "$HASH" = "$STORED" ]; then
    echo "sts2.dll unchanged ($HASH) — skipping decompile."
    exit 0
fi

echo "New version detected ($HASH). Decompiling sts2.dll..."
mkdir -p "$REPO_ROOT/decompiled"
ilspycmd "$DLL" --outputdir "$REPO_ROOT/decompiled/" --project
echo "$HASH" > "$REPO_ROOT/decompiled/.version"
echo "Done. Review decompiled/ and commit to record this version."
