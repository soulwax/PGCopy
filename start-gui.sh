#!/usr/bin/env bash
set -euo pipefail

# File: start-gui.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$SCRIPT_DIR"

cd "$ROOT_DIR"
dotnet build src/PostgresCopy.Desktop/PostgresCopy.Desktop.csproj
dotnet run --no-build --project src/PostgresCopy.Desktop -- "$@"
