#!/usr/bin/env python3
"""Run deterministic consumer checks and retain complete compiler/runtime output."""

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path


def run_check(workspace, session, task, results, args, label):
    if args.sandbox:
        command = [sys.executable, str(Path(__file__).with_name("sandbox.py")),
                   "--workspace", str(workspace), "--session", session,
                   "--audit", str(results / "verification-audit"),
                   "--dotnet-root", str(args.dotnet_root), "--", "dotnet", "run",
                   "--project", f"tasks/{task}/Consumer.csproj", "-c", "Release",
                   "-p:UseSharedCompilation=false"]
        cwd = workspace
    else:
        command = [args.dotnet, "run", "--project", "Consumer.csproj", "-c", "Release",
                   "-p:UseSharedCompilation=false"]
        cwd = workspace / session / "tasks" / task
    try:
        process = subprocess.run(command, cwd=cwd, text=True,
                                 stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                 timeout=200)
        output, code = process.stdout, process.returncode
    except subprocess.TimeoutExpired as error:
        output = error.stdout or b""
        if isinstance(output, bytes):
            output = output.decode(errors="replace")
        output += "\nEvaluation timed out.\n"
        code = 124
    (results / f"{label}.log").write_text(output)
    return code


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", required=True, type=Path)
    parser.add_argument("--results", required=True, type=Path)
    parser.add_argument("--audit", type=Path, help="External sandbox audit with frozen first answers")
    parser.add_argument("--sandbox", action="store_true")
    parser.add_argument("--dotnet-root", type=Path)
    parser.add_argument("--dotnet", default="dotnet")
    args = parser.parse_args()
    if args.sandbox and not args.dotnet_root:
        parser.error("--sandbox requires --dotnet-root")
    args.workspace = args.workspace.resolve()
    args.results = args.results.resolve()
    args.results.mkdir(parents=True, exist_ok=False)
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
        if path.is_file():
            destination = args.results / "inputs" / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, destination)
    for session in manifest["assignments"]:
        for path in (args.workspace / session).rglob("*"):
            if {"bin", "obj"}.intersection(path.relative_to(args.workspace / session).parts):
                continue
            if path.is_file() and path.suffix in {".cs", ".csproj", ".props", ".targets"}:
                relative = str(path.relative_to(args.workspace))
                if relative not in manifest["snapshot_sha256"]:
                    mismatches.append("unexpected compiler input: " + relative)
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

    first_workspaces = args.results / "first-workspaces"
    first_workspaces.mkdir()
    shutil.copy2(manifest_path, first_workspaces / "manifest.json")
    for session in manifest["assignments"]:
        shutil.copytree(args.workspace / session, first_workspaces / session,
                        ignore=shutil.ignore_patterns("bin", "obj"))

    results = []
    for project in sorted(args.workspace.glob("*/tasks/*/Consumer.csproj")):
        label = f"{project.parents[2].name}-{project.parent.name}"
        evidence = args.results / "answers" / project.parents[2].name / project.parent.name
        evidence.mkdir(parents=True, exist_ok=True)
        for name in ["Solution.cs", "FIRST-Solution.cs.txt", "CHOICE.md", "ATTEMPTS.md"]:
            source = project.parent / name
            if source.is_file():
                shutil.copy2(source, evidence / name)
        attempts = project.parent / "ATTEMPTS.md"
        if not attempts.is_file():
            attempts = project.parents[2] / "ATTEMPTS.md"
            if attempts.is_file():
                shutil.copy2(attempts, evidence / "SESSION-ATTEMPTS.md")
        session = project.parents[2].name
        task = project.parent.name
        first = project.parent / "FIRST-Solution.cs.txt"
        if not first.is_file() and len(manifest["assignments"][project.parents[2].name]) == 1:
            first = project.parents[2] / "FIRST-Solution.cs.txt"
            if first.is_file():
                shutil.copy2(first, evidence / first.name)
        first_kind = "agent-supplied"
        if args.audit:
            audited = args.audit / session / "first" / "tasks" / task / "Solution.cs"
            if audited.is_file():
                first = audited
                first_kind = "external first non-stub observed at command boundary"
        relative = str((project.parent / "Solution.cs").relative_to(args.workspace))
        first_is_stub = first.is_file() and hashlib.sha256(first.read_bytes()).hexdigest() == manifest["snapshot_sha256"][relative]
        first_code = None
        if first.is_file() and not first_is_stub:
            shutil.copy2(first, evidence / "audited-first.cs.txt")
            shutil.copy2(first, first_workspaces / session / "tasks" / task / "Solution.cs")
            first_code = run_check(first_workspaces, session, task, args.results, args, label + "-first")
        code = run_check(args.workspace, session, task, args.results, args, label)
        result = {
            "task": project.parent.name,
            "session": project.parents[2].name,
            "exit_code": code,
            "first_snapshot_present": first.is_file(),
            "first_snapshot_path": str(first) if first.is_file() else None,
            "first_snapshot_is_stub": first_is_stub,
            "first_evidence_kind": first_kind if first.is_file() else None,
            "first_snapshot_sha256": hashlib.sha256(first.read_bytes()).hexdigest() if first.is_file() else None,
            "first_check_exit_code": first_code,
            "choice_present": (project.parent / "CHOICE.md").is_file(),
            "attempts_present": attempts.is_file(),
            "attempts_path": str(attempts) if attempts.is_file() else None,
        }
        results.append(result)
        print(json.dumps(result), flush=True)
    (args.results / "correctness.json").write_text(json.dumps(results, indent=2) + "\n")
    return 1 if any(item["exit_code"] != 0 for item in results) else 0


if __name__ == "__main__":
    raise SystemExit(main())
