#!/usr/bin/env bash
set -euo pipefail

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repo_root=$(cd "$script_dir/.." && pwd)

project="$repo_root/benchmarks/DeltaECS.MicroBenchmarks/DeltaECS.MicroBenchmarks.csproj"
configuration="Release"
target_framework="net8.0"
method_pattern=""
benchmark_filter=""
benchmark_job="dry"
output=""
no_build=0

usage() {
    cat <<'EOF'
Usage:
  benchmarks/run-jit-disasm.sh --method <jit-pattern> [options]

Options:
  --method <pattern>       DOTNET_JitDisasm pattern (required)
  --filter <pattern>       BenchmarkDotNet filter; defaults to the method pattern
  --project <path>         Probe project; defaults to DeltaECS.MicroBenchmarks
  --configuration <name>   Build configuration; defaults to Release
  --framework <tfm>        Target framework; defaults to net8.0
  --job <name>             BenchmarkDotNet job; defaults to dry
  --output <path>          Output file; defaults to artifacts/jit-disasm/<pattern>.txt
  --no-build               Run the existing target DLL without building
  -h, --help               Show this help

Examples:
  benchmarks/run-jit-disasm.sh \
    --method 'Movement2ComponentsReverse' \
    --filter '*Movement2ComponentsReverse*'

  benchmarks/run-jit-disasm.sh \
    --method 'RunRead' \
    --project /private/tmp/deltaecs-dense-jit-probe/DenseJitProbe.csproj \
    --filter '*' --job dry
EOF
}

while (($# > 0)); do
    case "$1" in
        --method)
            [[ $# -ge 2 ]] || { echo "--method requires a value" >&2; exit 2; }
            method_pattern=$2
            shift 2
            ;;
        --filter)
            [[ $# -ge 2 ]] || { echo "--filter requires a value" >&2; exit 2; }
            benchmark_filter=$2
            shift 2
            ;;
        --project)
            [[ $# -ge 2 ]] || { echo "--project requires a value" >&2; exit 2; }
            project=$2
            shift 2
            ;;
        --configuration)
            [[ $# -ge 2 ]] || { echo "--configuration requires a value" >&2; exit 2; }
            configuration=$2
            shift 2
            ;;
        --framework)
            [[ $# -ge 2 ]] || { echo "--framework requires a value" >&2; exit 2; }
            target_framework=$2
            shift 2
            ;;
        --job)
            [[ $# -ge 2 ]] || { echo "--job requires a value" >&2; exit 2; }
            benchmark_job=$2
            shift 2
            ;;
        --output)
            [[ $# -ge 2 ]] || { echo "--output requires a value" >&2; exit 2; }
            output=$2
            shift 2
            ;;
        --no-build)
            no_build=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

[[ -n "$method_pattern" ]] || { echo "--method is required" >&2; usage >&2; exit 2; }
[[ -f "$project" ]] || { echo "Probe project not found: $project" >&2; exit 2; }

if [[ -z "$benchmark_filter" ]]; then
    benchmark_filter=$method_pattern
fi

project_dir=$(cd "$(dirname "$project")" && pwd)
project_name=$(basename "$project" .csproj)
dll="$project_dir/bin/$configuration/$target_framework/$project_name.dll"

if [[ -z "$output" ]]; then
    safe_pattern=$(printf '%s' "$method_pattern" | tr -c 'A-Za-z0-9._-' '_')
    output="$repo_root/artifacts/jit-disasm/${safe_pattern}.txt"
fi

if [[ "$output" != /* ]]; then
    output="$repo_root/$output"
fi

mkdir -p "$(dirname "$output")"

if ((no_build == 0)); then
    env NuGetAudit=false dotnet build "$project" -c "$configuration" --no-restore \
        --disable-build-servers -m:1 /p:UseSharedCompilation=false /p:NuGetAudit=false -v:minimal
fi

[[ -f "$dll" ]] || {
    echo "Target DLL not found: $dll" >&2
    echo "Build the probe first or remove --no-build." >&2
    exit 2
}

echo "JIT method pattern: $method_pattern"
echo "Benchmark filter: $benchmark_filter"
echo "Output: $output"

(
    cd "$project_dir"
    env NuGetAudit=false \
    RestoreIgnoreFailedSources=true \
    DOTNET_TieredCompilation=0 \
    DOTNET_ReadyToRun=0 \
    DOTNET_JitDisasm="$method_pattern" \
    DOTNET_JitDisasmDiffable=1 \
    dotnet "$dll" --filter "$benchmark_filter" --job "$benchmark_job"
) > "$output" 2>&1

echo "JIT disassembly written to $output"
