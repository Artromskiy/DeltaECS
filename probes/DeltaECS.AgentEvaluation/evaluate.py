#!/usr/bin/env python3
"""Run deterministic consumer checks and retain complete compiler/runtime output."""

import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", required=True, type=Path)
    parser.add_argument("--results", required=True, type=Path)
    args = parser.parse_args()
    args.results.mkdir(parents=True, exist_ok=True)
    manifest_path = args.workspace / "manifest.json"
    manifest = json.loads(manifest_path.read_text())
    shutil.copy2(manifest_path, args.results / "manifest.json")
    mismatches = []
    for relative, expected in manifest["snapshot_sha256"].items():
        if Path(relative).name == "Solution.cs":
            continue
        path = args.workspace / relative
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
            mismatches.append(relative)
    (args.results / "input-integrity.json").write_text(json.dumps({"modified_inputs": mismatches}, indent=2) + "\n")
    if mismatches:
        raise SystemExit("Supplied inputs changed: " + ", ".join(mismatches))

    for session in manifest["assignments"]:
        evidence = args.results / "answers" / session
        evidence.mkdir(parents=True, exist_ok=True)
        for name in ["CHOICE.md", "ATTEMPTS.md", "FIRST-Solution.cs.txt"]:
            source = args.workspace / session / name
            if source.is_file():
                shutil.copy2(source, evidence / name)

    results = []
    for project in sorted(args.workspace.glob("*/tasks/*/Consumer.csproj")):
        label = f"{project.parents[2].name}-{project.parent.name}"
        evidence = args.results / "answers" / project.parents[2].name / project.parent.name
        evidence.mkdir(parents=True, exist_ok=True)
        for name in ["Solution.cs", "FIRST-Solution.cs.txt", "CHOICE.md", "ATTEMPTS.md"]:
            source = project.parent / name
            if source.is_file():
                shutil.copy2(source, evidence / name)
        process = subprocess.run(
            ["dotnet", "run", "--project", "Consumer.csproj", "-c", "Release", "--no-restore"],
            cwd=project.parent,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            timeout=180,
        )
        (args.results / f"{label}.log").write_text(process.stdout)
        first = project.parent / "FIRST-Solution.cs.txt"
        if not first.is_file() and len(manifest["assignments"][project.parents[2].name]) == 1:
            first = project.parents[2] / "FIRST-Solution.cs.txt"
            if first.is_file():
                shutil.copy2(first, evidence / first.name)
        relative = str((project.parent / "Solution.cs").relative_to(args.workspace))
        result = {
            "task": project.parent.name,
            "session": project.parents[2].name,
            "exit_code": process.returncode,
            "first_snapshot_present": first.is_file(),
            "first_snapshot_path": str(first.relative_to(args.workspace)) if first.is_file() else None,
            "first_snapshot_is_stub": first.is_file() and hashlib.sha256(first.read_bytes()).hexdigest() == manifest["snapshot_sha256"][relative],
        }
        results.append(result)
        print(json.dumps(result), flush=True)
    (args.results / "correctness.json").write_text(json.dumps(results, indent=2) + "\n")


if __name__ == "__main__":
    main()
