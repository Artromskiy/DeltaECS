#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

project="benchmarks/DeltaECS.ArrayRefBenchmarks/DeltaECS.ArrayRefBenchmarks.csproj"

run_mode() {
  local framework="$1"
  local mode="$2"
  local runtime="$3"
  local assembly="benchmarks/DeltaECS.ArrayRefBenchmarks/bin/Release/$framework/DeltaECS.ArrayRefBenchmarks.dll"
  local build_log="${TMPDIR:-/tmp}/deltaecs-array-ref-${framework}-${mode}.log"
  local artifact_path="artifacts/array-ref-row-start-20260917/${framework}-${mode}-200k-70x100ms"
  local run_log="${TMPDIR:-/tmp}/deltaecs-array-ref-${framework}-${mode}-run.log"

  if ! env NuGetAudit=false RestoreIgnoreFailedSources=true \
    dotnet build "$project" -c Release -f "$framework" \
      --disable-build-servers -m:1 /p:UseSharedCompilation=false /t:Rebuild \
      "/p:DeltaECSArrayReferenceMode=$mode" -v:q >"$build_log" 2>&1; then
    cat "$build_log"
    return 1
  fi

  printf 'Built %s / %s\n' "$framework" "$mode"
  if ! "$runtime" "$assembly" \
    --amount 200000 \
    --filter '*ForEachT*' \
    --warmupCount 5 \
    --iterationCount 70 \
    --iterationTime 100 \
    --launchCount 1 \
    --artifacts "$artifact_path" >"$run_log" 2>&1; then
    tail -n 80 "$run_log"
    return 1
  fi

  local summary="$(rg '^\| ForEachT \|' "$run_log" | tail -n 1)"
  local retained="$(rg -o 'N = [0-9]+' "$run_log" | tail -n 1 | awk '{print $3}')"
  if [[ -z "$summary" || "$summary" == *'| NA '* || -z "$retained" || "$retained" -lt 50 ]]; then
    tail -n 80 "$run_log"
    printf 'Expected at least 50 retained measurements for %s / %s.\n' "$framework" "$mode" >&2
    return 1
  fi

  printf '%s (retained N=%s)\n' "$summary" "$retained"
}

for mode in Index Span Fixed; do
  run_mode netstandard2.1 "$mode" mono
done

for mode in Index Span Fixed UnsafeOffset; do
  run_mode net10.0 "$mode" dotnet
done
