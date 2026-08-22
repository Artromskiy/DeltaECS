#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

# Folder mode avoids MSBuild/Roslyn workspace discovery, which can hang on
# some macOS/.NET combinations. Keep the source roots explicit so generated
# output and benchmark artifacts are not scanned accidentally.
format_args=(
    whitespace
    --folder .
    --include src
    --include tests
    --include playground
    --include benchmarks
    --exclude ./obj
    --exclude ./bin
    --verbosity minimal
)
if [[ "${FORMAT_CHECK:-0}" == "1" ]]; then
    format_args+=(--verify-no-changes)
fi

timeout_seconds="${FORMAT_TIMEOUT_SECONDS:-60}"
if command -v perl >/dev/null 2>&1; then
    perl -e 'alarm shift; exec @ARGV' "$timeout_seconds" dotnet format "${format_args[@]}"
else
    dotnet format "${format_args[@]}"
fi
