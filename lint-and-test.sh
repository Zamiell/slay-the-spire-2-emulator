#!/usr/bin/env bash

set -euo pipefail # Exit on errors and undefined variables.

START_SECONDS="$SECONDS"

# Check Python formatting
uv run black . --check --target-version py314

# Type-check Python
uv run ty check .

# Check C# formatting
dotnet csharpier check .

# Build C#
dotnet build src/Sts2Emulator.Tests/Sts2Emulator.Tests.csproj --configuration Release
dotnet publish src/Sts2Emulator/Sts2Emulator.csproj -c Release -r win-x64 --self-contained -o out --nologo

# Test Python
uv run python -m unittest tests/python/test_sts2_gym.py
uv run python scripts/train.py --check --run-env
uv run python scripts/evaluate.py --episodes 2 --run-env --policy first-valid --max-episode-steps 20

# Test the emulator
dotnet test src/Sts2Emulator.Tests/Sts2Emulator.Tests.csproj --nologo

ELAPSED_SECONDS="$((SECONDS - START_SECONDS))"
echo -e "\n$0 successfully completed in $ELAPSED_SECONDS seconds."
