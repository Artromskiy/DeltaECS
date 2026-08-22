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
jit_dump=0
checked_jit="${DELTAECS_CHECKED_JIT:-}"

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
  --jit-dump               Include the Debug/Checked JIT dump in the output
  --checked-jit <path>     Version-compatible Debug/Checked libclrjit used in an isolated runtime copy
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
        --jit-dump)
            jit_dump=1
            shift
            ;;
        --checked-jit)
            [[ $# -ge 2 ]] || { echo "--checked-jit requires a value" >&2; exit 2; }
            checked_jit=$2
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

if ((jit_dump == 1)); then
    [[ -n "$checked_jit" ]] || {
        echo "--jit-dump requires --checked-jit <path> or DELTAECS_CHECKED_JIT" >&2
        exit 2
    }
    [[ -f "$checked_jit" ]] || { echo "Checked JIT not found: $checked_jit" >&2; exit 2; }
fi

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
    env NuGetAudit=false RestoreIgnoreFailedSources=true \
        dotnet restore "$project" --ignore-failed-sources \
        --disable-build-servers -m:1 /p:NuGetAudit=false \
        /p:RestoreIgnoreFailedSources=true -v:minimal
    env NuGetAudit=false RestoreIgnoreFailedSources=true \
        dotnet build "$project" -c "$configuration" --no-restore \
        --disable-build-servers -m:1 /p:UseSharedCompilation=false \
        /p:NuGetAudit=false /p:RestoreIgnoreFailedSources=true -v:minimal
fi

[[ -f "$dll" ]] || {
    echo "Target DLL not found: $dll" >&2
    echo "Build the probe first or remove --no-build." >&2
    exit 2
}

echo "JIT method pattern: $method_pattern"
echo "Benchmark filter: $benchmark_filter"
echo "Output: $output"

dotnet_cli=$(command -v dotnet)
dotnet_root=""

prepare_checked_runtime() {
    local framework_major runtime_line runtime_version runtime_base source_root
    local isolated_root isolated_runtime directory

    framework_major=${target_framework#net}
    framework_major=${framework_major%%.*}
    runtime_line=$(dotnet --list-runtimes | awk -v major="$framework_major" '
        $1 == "Microsoft.NETCore.App" && $2 ~ ("^" major "\\.") { line = $0 }
        END { print line }
    ')
    [[ -n "$runtime_line" ]] || {
        echo "Microsoft.NETCore.App $framework_major.x was not found." >&2
        exit 2
    }

    runtime_version=$(printf '%s\n' "$runtime_line" | awk '{ print $2 }')
    runtime_base=$(printf '%s\n' "$runtime_line" | sed -E 's/^[^[]*\[([^]]+)\]$/\1/')
    source_root=$(cd "$runtime_base/../.." && pwd)
    isolated_root="$repo_root/artifacts/toolchains/jit-runtime-$runtime_version"
    isolated_runtime="$isolated_root/shared/Microsoft.NETCore.App/$runtime_version"

    mkdir -p "$isolated_root/host" "$isolated_root/shared/Microsoft.NETCore.App"
    if [[ ! -x "$isolated_root/dotnet" ]]; then
        cp "$source_root/dotnet" "$isolated_root/dotnet"
    fi
    if [[ ! -d "$isolated_root/host/fxr" ]]; then
        cp -R "$source_root/host/fxr" "$isolated_root/host/"
    fi
    if [[ ! -d "$isolated_runtime" ]]; then
        cp -R "$runtime_base/$runtime_version" "$isolated_runtime"
    fi

    # BenchmarkDotNet invokes the CLI to build its generated dry-run host. Keep
    # SDK assets shared, but keep CoreCLR and the Checked JIT isolated.
    for directory in sdk packs sdk-manifests templates metadata; do
        if [[ -e "$source_root/$directory" && ! -e "$isolated_root/$directory" ]]; then
            ln -s "$source_root/$directory" "$isolated_root/$directory"
        fi
    done

    cp "$checked_jit" "$isolated_runtime/libclrjit.dylib"
    dotnet_cli="$isolated_root/dotnet"
    dotnet_root="$isolated_root"
    echo "Checked JIT runtime: $isolated_root"
}

if ((jit_dump == 1)); then
    prepare_checked_runtime
fi

(
    cd "$project_dir"
    if ((jit_dump == 1)); then
        env NuGetAudit=false \
        RestoreIgnoreFailedSources=true \
        DOTNET_ROOT="$dotnet_root" \
        DOTNET_TieredCompilation=0 \
        DOTNET_ReadyToRun=0 \
        DOTNET_JitDump="$method_pattern" \
        DOTNET_JitDisasm="$method_pattern" \
        DOTNET_JitDisasmDiffable=1 \
        "$dotnet_cli" "$dll" --filter "$benchmark_filter" --job "$benchmark_job" \
        --cli "$dotnet_cli"
    else
        env NuGetAudit=false \
        RestoreIgnoreFailedSources=true \
        DOTNET_TieredCompilation=0 \
        DOTNET_ReadyToRun=0 \
        DOTNET_JitDisasm="$method_pattern" \
        DOTNET_JitDisasmDiffable=1 \
        dotnet "$dll" --filter "$benchmark_filter" --job "$benchmark_job"
    fi
) > "$output" 2>&1

echo "JIT disassembly written to $output"
