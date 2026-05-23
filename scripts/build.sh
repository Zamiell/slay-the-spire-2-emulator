#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RUNTIME="${1:-win-x64}"   # override: bash scripts/build.sh linux-x64

echo "Building Sts2Emulator for $RUNTIME..."

dotnet publish "$REPO_ROOT/src/Sts2Emulator/Sts2Emulator.csproj" \
    -c Release \
    -r "$RUNTIME" \
    --self-contained \
    -o "$REPO_ROOT/out/"

echo "Output: $REPO_ROOT/out/"
ls "$REPO_ROOT/out/"
