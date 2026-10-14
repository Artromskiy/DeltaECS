#!/usr/bin/env bash
set -euo pipefail

repo_root="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
game_root="$repo_root/benchmarks/SkyPirates"
runner_project="$game_root/Tools/SkyPirates.ReplayRunner/SkyPirates.ReplayRunner.csproj"
runner_dll="$game_root/Tools/SkyPirates.ReplayRunner/bin/Release/net10.0/SkyPirates.ReplayRunner.dll"
replay="$game_root/Replays/skypirates-20261015-005758-186.json"
dotnet_cmd="${DOTNET:-dotnet}"
cpu_affinity="${CPU_AFFINITY:-1}"
warmups="${WARMUPS:-3}"
repeats="${REPEATS:-10}"
csv="${CSV:-/tmp/skypirates-replay-$(date -u +%Y%m%dT%H%M%SZ)-$$.csv}"

if [[ ! -f "$game_root/.git" && ! -d "$game_root/.git" ]]; then
    echo "SkyPirates submodule is missing. Run: git submodule update --init benchmarks/SkyPirates" >&2
    exit 2
fi

"$dotnet_cmd" restore "$runner_project" -p:UseLocalDeltaECS=true \
    --disable-build-servers -m:1 --verbosity:minimal
"$dotnet_cmd" build "$runner_project" -c Release --no-restore \
    -p:UseLocalDeltaECS=true --disable-build-servers -m:1 \
    /p:UseSharedCompilation=false --verbosity:quiet

runner_args=("$replay" --warmup "$warmups" --repeat "$repeats" --csv "$csv")
if [[ "$cpu_affinity" == "none" ]]; then
    DOTNET_TieredCompilation=0 DOTNET_TieredPGO=0 \
        "$dotnet_cmd" "$runner_dll" "${runner_args[@]}"
else
    if ! command -v taskset >/dev/null 2>&1; then
        echo "taskset is required for CPU pinning; set CPU_AFFINITY=none to disable it." >&2
        exit 2
    fi
    DOTNET_TieredCompilation=0 DOTNET_TieredPGO=0 \
        taskset -c "$cpu_affinity" "$dotnet_cmd" "$runner_dll" "${runner_args[@]}"
fi
