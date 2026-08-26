#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/DeltaECS.Profiling/DeltaECS.Profiling.csproj"
MOVEMENT4_ARGUMENT="--movement4"

profile_properties=()
for argument in "$@"; do
  if [[ "$argument" == "$MOVEMENT4_ARGUMENT" ]]; then
    profile_properties+=("-p:EnableEcsProfiling=true")
    break
  fi
done

exec dotnet run --project "$PROJECT" -c Release "${profile_properties[@]}" -- "$@"
