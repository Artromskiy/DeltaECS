#!/usr/bin/env bash
set -euo pipefail

project_dir="${1:-}"
mode="${2:-}"
if [[ -z "$project_dir" || ( -n "$mode" && "$mode" != "--check" ) || $# -gt 2 ]]; then
    echo "Usage: $0 <project-directory> [--check]" >&2
    exit 2
fi

project_dir="$(cd "$project_dir" && pwd -P)"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
tool_project="$script_dir/SourceOrdering/SourceOrdering.csproj"
if [[ ! -f "$tool_project" ]]; then
    echo "source-order tool project not found: $tool_project" >&2
    exit 2
fi

args=("$project_dir")
if [[ "$mode" == "--check" ]]; then
    args+=(--check)
fi

dotnet run --project "$tool_project" --no-launch-profile -- "${args[@]}"

dotnet_format_sdk="$(dotnet --info | sed -n 's/^[[:space:]]*Base Path: //p' | head -n 1)"
dotnet_format_dll="${dotnet_format_sdk%/}/DotnetTools/dotnet-format/dotnet-format.dll"
style_args=(
    "$project_dir/DeltaECS.slnx"
    style
    --diagnostics IDE0300 IDE0301 IDE0302
    --severity info
    --no-restore
    --verbosity minimal
)
if [[ "$mode" == "--check" ]]; then
    style_args+=(--verify-no-changes)
fi

if [[ -f "$dotnet_format_dll" ]]; then
    dotnet "$dotnet_format_dll" "${style_args[@]}"
else
    dotnet format "${style_args[@]}"
fi
