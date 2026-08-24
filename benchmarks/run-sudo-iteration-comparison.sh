#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repo_root/benchmarks/DeltaECS.Benchmarks/DeltaECS.Benchmarks.csproj"
runner="$repo_root/benchmarks/DeltaECS.Benchmarks/bin/Release/net10.0/DeltaECS.Benchmarks.dll"

warmup_count=5
iteration_count=15
launch_count=1
filter='*'
artifact_root="$repo_root/artifacts/local-sudo-iteration-$(date +%Y%m%d-%H%M%S)"
skip_build=false

usage() {
    cat <<'EOF'
Usage: benchmarks/run-sudo-iteration-comparison.sh [options]

Runs the complete comparative iteration suite at elevated process priority.
The current suite compares DeltaECS, Arch, Friflo, DefaultEcs and LeoEcsLite.

Options:
  --warmups N       Warm-up iterations per benchmark (default: 5)
  --iterations N    Measurement iterations per benchmark (default: 15)
  --launches N      Isolated process launches (default: 1)
  --filter GLOB     BenchmarkDotNet filter (default: *)
  --artifacts PATH  Output directory
  --no-build        Reuse the existing Release benchmark build
  -h, --help        Show this help

The defaults target an approximately 25-35 minute run on an Apple M4 Pro.
Actual duration depends on the machine, runtime and thermal state.
EOF
}

require_positive_integer() {
    local name="$1"
    local value="$2"
    if [[ ! "$value" =~ ^[1-9][0-9]*$ ]]; then
        echo "$name must be a positive integer, got: $value" >&2
        exit 2
    fi
}

while (($# > 0)); do
    case "$1" in
        --warmups)
            warmup_count="${2:?missing value for --warmups}"
            shift 2
            ;;
        --iterations)
            iteration_count="${2:?missing value for --iterations}"
            shift 2
            ;;
        --launches)
            launch_count="${2:?missing value for --launches}"
            shift 2
            ;;
        --filter)
            filter="${2:?missing value for --filter}"
            shift 2
            ;;
        --artifacts)
            artifact_root="${2:?missing value for --artifacts}"
            shift 2
            ;;
        --no-build)
            skip_build=true
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown option: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

require_positive_integer "--warmups" "$warmup_count"
require_positive_integer "--iterations" "$iteration_count"
require_positive_integer "--launches" "$launch_count"

if [[ "$(id -u)" == 0 ]]; then
    echo "Run this script as your normal user; it requests sudo only for the measured process." >&2
    exit 2
fi

mkdir -p "$artifact_root"
artifact_root="$(cd "$artifact_root" && pwd)"

if [[ "$skip_build" == false ]]; then
    env NuGetAudit=false RestoreIgnoreFailedSources=true \
        dotnet build "$project" \
        -c Release \
        --disable-build-servers \
        -m:1 \
        /p:UseSharedCompilation=false \
        /p:NuGetAudit=false \
        -v:minimal
fi

if [[ ! -f "$runner" ]]; then
    echo "Benchmark runner not found: $runner" >&2
    echo "Run without --no-build first." >&2
    exit 1
fi

owner_uid="$(id -u)"
owner_gid="$(id -g)"
started_at="$(date '+%Y-%m-%d %H:%M:%S %z')"

cat <<EOF
[$started_at] Starting elevated comparative iteration benchmark
  ECS:         DeltaECS, Arch, Friflo, DefaultEcs, LeoEcsLite
  warmups:     $warmup_count
  iterations:  $iteration_count
  launches:    $launch_count
  filter:      $filter
  artifacts:   $artifact_root
EOF

sudo -v
sudo env \
    HOME="$HOME" \
    PATH="$PATH" \
    DOTNET_ROOT="${DOTNET_ROOT:-}" \
    NuGetAudit=false \
    RestoreIgnoreFailedSources=true \
    DELTAECS_REPO_ROOT="$repo_root" \
    DELTAECS_RUNNER="$runner" \
    DELTAECS_ARTIFACT_ROOT="$artifact_root" \
    DELTAECS_FILTER="$filter" \
    DELTAECS_WARMUPS="$warmup_count" \
    DELTAECS_ITERATIONS="$iteration_count" \
    DELTAECS_LAUNCHES="$launch_count" \
    DELTAECS_OWNER_UID="$owner_uid" \
    DELTAECS_OWNER_GID="$owner_gid" \
    bash -c '
        set -euo pipefail
        trap '\''chown -R "$DELTAECS_OWNER_UID:$DELTAECS_OWNER_GID" "$DELTAECS_ARTIFACT_ROOT"'\'' EXIT
        cd "$DELTAECS_REPO_ROOT"
        nice -n -20 dotnet "$DELTAECS_RUNNER" \
            iteration \
            --filter "$DELTAECS_FILTER" \
            --job Default \
            --warmupCount "$DELTAECS_WARMUPS" \
            --iterationCount "$DELTAECS_ITERATIONS" \
            --launchCount "$DELTAECS_LAUNCHES" \
            --exporters json csv markdown github \
            --artifacts "$DELTAECS_ARTIFACT_ROOT" \
            --combined-report "$DELTAECS_ARTIFACT_ROOT" \
            2>&1 | tee "$DELTAECS_ARTIFACT_ROOT/benchmark.log"
    '

echo "Results: $artifact_root"
