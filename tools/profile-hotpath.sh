#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/DeltaECS.Profiling/DeltaECS.Profiling.csproj"
exec dotnet run --project "$PROJECT" -c Release -- "$@"
