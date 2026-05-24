#!/usr/bin/env bash
set -euo pipefail

uvx ty check .
dotnet build src/Sts2Emulator.Tests/Sts2Emulator.Tests.csproj --configuration Release
