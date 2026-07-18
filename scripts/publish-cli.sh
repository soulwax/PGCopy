#!/usr/bin/env bash
# File: scripts/publish-cli.sh
#
# Publishes the PostgresCopy CLI as a self-contained single-file executable
# for Linux or macOS. The desktop app is Windows-only (WinForms) and has no
# equivalent here — see scripts/publish-desktop.ps1 for that.
#
# Usage:
#   scripts/publish-cli.sh [runtime] [configuration]
#
# Examples:
#   scripts/publish-cli.sh                  # auto-detects linux-x64 / osx-x64 / osx-arm64
#   scripts/publish-cli.sh linux-arm64
#   scripts/publish-cli.sh osx-arm64 Debug

set -euo pipefail

runtime="${1:-}"
configuration="${2:-Release}"

if [ -z "$runtime" ]; then
    case "$(uname -s)" in
        Linux)
            runtime="linux-x64"
            ;;
        Darwin)
            if [ "$(uname -m)" = "arm64" ]; then
                runtime="osx-arm64"
            else
                runtime="osx-x64"
            fi
            ;;
        *)
            echo "Could not auto-detect a runtime identifier for $(uname -s). Pass one explicitly, e.g. linux-x64, linux-arm64, osx-x64, osx-arm64." >&2
            exit 1
            ;;
    esac
fi

if ! [[ "$runtime" =~ ^[A-Za-z0-9._-]+$ ]]; then
    echo "Runtime contains unsupported characters: $runtime" >&2
    exit 1
fi

if ! [[ "$configuration" =~ ^[A-Za-z0-9._-]+$ ]]; then
    echo "Configuration contains unsupported characters: $configuration" >&2
    exit 1
fi

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="$root/artifacts/PostgresCopy-cli-$runtime"

cd "$root"

dotnet publish src/PostgresCopy \
    --configuration "$configuration" \
    --runtime "$runtime" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    --output "$output"

echo "CLI published to $output"
