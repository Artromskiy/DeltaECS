#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root_dir"

# ErrorLog is resolved by Roslyn once per project.  A relative path therefore
# becomes project-relative and fails when the parent directory does not exist.
# Normalize it before invoking MSBuild so every project writes to one report.
error_log="${CODE_METRICS_ERROR_LOG:-artifacts/code-metrics/diagnostics.sarif}"
case "$error_log" in
    /*) ;;
    *) error_log="$root_dir/$error_log" ;;
esac
mkdir -p "$(dirname "$error_log")"

exec dotnet build DeltaECS.slnx \
    -c Release \
    --no-restore \
    --disable-build-servers \
    -m:1 \
    /p:UseSharedCompilation=false \
    /p:AnalysisMode=AllEnabledByDefault \
    "/p:ErrorLog=$error_log,version=2.1" \
    "$@"
