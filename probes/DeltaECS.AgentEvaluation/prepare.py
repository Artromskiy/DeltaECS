#!/usr/bin/env python3
"""Create source-free consumer workspaces for independent API-choice sessions."""

import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--session", choices=["a", "b", "c", "d"], action="append")
    parser.add_argument("--task", action="append", help="Prepare only these task IDs within the selected sessions")
    args = parser.parse_args()
    suite = Path(__file__).resolve().parent
    repo = suite.parents[1]
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    assignments = {
        "a": ["01-motion", "02-nearest", "03-budget", "04-entity-output"],
        "b": ["05-configured-rows", "06-health-events", "07-tag-events", "11-entity-only"],
        "c": ["08-runtime-dispatch", "09-world-snapshot", "10-structural", "12-target-list"],
        "d": ["13-group-cleanup"],
    }
    if args.session:
        assignments = {name: tasks for name, tasks in assignments.items() if name in args.session}
    if args.task:
        assignments = {name: [task for task in tasks if task in args.task] for name, tasks in assignments.items()}
        assignments = {name: tasks for name, tasks in assignments.items() if tasks}
        if not assignments:
            raise SystemExit("No tasks matched the selection")
    documents = ["README.md", "docs/API-SELECTION.md", "docs/API-GRAMMAR.md", "docs/usage-examples.md"]
    binaries = {
        "src/DeltaECS/bin/Release/net10.0/DeltaECS.dll": "lib/DeltaECS.dll",
        "src/DeltaECS.Generators/bin/Release/netstandard2.0/DeltaECS.Generators.dll": "analyzers/DeltaECS.Generators.dll",
    }
    manifest = {
        "source_commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip(),
        "model": "gpt-6-luna",
        "reasoning_effort": "high",
        "assignments": assignments,
        "snapshot_sha256": {},
        "isolation": "fresh context and file whitelist; not an OS-enforced filesystem sandbox",
    }
    for session, tasks in assignments.items():
        target = output / session
        target.mkdir()
        for relative in documents:
            destination = target / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(repo / relative, destination)
        for source, relative in binaries.items():
            destination = target / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(repo / source, destination)
        shutil.copy2(suite / "common/Fixture.cs", target / "Fixture.cs")
        (target / "NuGet.Config").write_text(
            "<configuration><packageSources><clear /></packageSources></configuration>\n"
        )
        for name in tasks:
            destination = target / "tasks" / name
            shutil.copytree(suite / "tasks" / name, destination)
            shutil.copy2(suite / "common/Consumer.csproj", destination / "Consumer.csproj")
        (target / "SESSION.md").write_text(
            "# Consumer evaluation session\n\n"
            "Implement the tasks below using only this directory's public documentation and consumer templates.\n\n"
            + "\n".join(f"- tasks/{name}/TASK.md" for name in tasks)
            + "\n\nRead docs/API-SELECTION.md first, then the grammar and cookbook as needed.\n"
            "Do not read AGENTS.md, memory, repository sources, other sessions, evaluator rubrics, "
            "assembly metadata/decompilation, generated implementation files, or online material. "
            "Use an explicit working directory under this session for every command. "
            "The compiler may read installed framework/runtime dependencies.\n"
            "Edit only each task's Solution.cs and create CHOICE.md and FIRST-Solution.cs.txt. "
            "Keep a record of build/run attempts in ATTEMPTS.md. "
            "Do not request advice from other agents. Do not commit or push.\n"
        )
        for item in sorted(target.rglob("*")):
            if item.is_file():
                manifest["snapshot_sha256"][str(item.relative_to(output))] = hashlib.sha256(item.read_bytes()).hexdigest()
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(output)


if __name__ == "__main__":
    main()
