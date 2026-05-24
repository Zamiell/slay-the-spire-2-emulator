#!/usr/bin/env bash

set -euo pipefail # Exit on errors and undefined variables.

# Type-check Python
uvx ty check .

# Build C#
dotnet build src/Sts2Emulator.Tests/Sts2Emulator.Tests.csproj --configuration Release
