#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/DeltaECS.Profiling/DeltaECS.Profiling.csproj"
MOVEMENT4_ARGUMENT="--movement4"

enable_ecs_profiling=false
for argument in "$@"; do
  if [[ "$argument" == "$MOVEMENT4_ARGUMENT" ]]; then
    enable_ecs_profiling=true
    break
  fi
done

if [[ "$enable_ecs_profiling" == true ]]; then
  exec dotnet run --project "$PROJECT" -c Release -p:EnableEcsProfiling=true -- "$@"
fi

exec dotnet run --project "$PROJECT" -c Release -- "$@"
