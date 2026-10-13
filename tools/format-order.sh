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
