#!/usr/bin/env bash

set -euo pipefail # Exit on errors and undefined variables.

# Check Python formatting
uv run black . --check --target-version py314

# Type-check Python
uv run ty check .

# Build C#
dotnet build src/Sts2Emulator.Tests/Sts2Emulator.Tests.csproj --configuration Release
