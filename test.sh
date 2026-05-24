#!/usr/bin/env bash

set -euo pipefail # Exit on errors and undefined variables.

uv run python -m unittest tests/python/test_sts2_gym.py
uv run python scripts/train.py --check --run-env
uv run python scripts/evaluate.py --episodes 2 --run-env --policy first-valid

dotnet test "src/Sts2Emulator.Tests/Sts2Emulator.Tests.csproj" --nologo
dotnet publish "src/Sts2Emulator/Sts2Emulator.csproj" -c Release -r win-x64 --self-contained -o "out" --nologo
