#!/usr/bin/env python3
"""Run a consumer command with only its session mounted; retain command evidence."""

import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time


def fingerprint(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def capture(session, manifest, audit, phase):
    hashes = {}
    for solution in sorted(session.glob("tasks/*/Solution.cs")):
        relative = solution.relative_to(session)
        digest = fingerprint(solution)
        hashes[str(relative)] = digest
        original = manifest["snapshot_sha256"][str(Path(session.name) / relative)]
        first = audit / "first" / relative
        if digest != original and not first.exists():
            first.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(solution, first)
            first.with_suffix(".json").write_text(json.dumps({
                "sha256": digest, "observed": phase,
                "meaning": "first non-stub solution observed at a sandbox command boundary",
            }, indent=2) + "\n")
    return hashes


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", required=True, type=Path)
    parser.add_argument("--session", required=True)
    parser.add_argument("--audit", required=True, type=Path)
    parser.add_argument("--dotnet-root", required=True, type=Path)
    parser.add_argument("--timeout", type=int, default=180)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    manifest = json.loads((args.workspace / "manifest.json").read_text())
    if args.session not in manifest["assignments"]:
        parser.error("session is not in the prepared manifest")
    session = (args.workspace / args.session).resolve()
    audit = (args.audit / args.session).resolve()
    audit.mkdir(parents=True, exist_ok=True)
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command:
        parser.error("supply a command after --")
    if not shutil.which("bwrap"):
        parser.error("bubblewrap is required; there is no unsandboxed fallback")
    sequence = f"{time.time_ns()}-{os.getpid()}"
    before = capture(session, manifest, audit, sequence + ":before")
    invocation = [
        "bwrap", "--die-with-parent", "--new-session", "--unshare-all",
        "--cap-drop", "ALL", "--clearenv",
        "--ro-bind", "/usr", "/usr", "--ro-bind", "/lib", "/lib",
        "--ro-bind", "/lib64", "/lib64", "--symlink", "usr/bin", "/bin",
        "--symlink", "usr/sbin", "/sbin", "--ro-bind", "/etc", "/etc",
        "--proc", "/proc", "--dev", "/dev", "--tmpfs", "/tmp",
        "--ro-bind", str(args.dotnet_root.resolve()), "/opt/dotnet",
        "--bind", str(session), "/work",
        "--setenv", "PATH", "/opt/dotnet:/usr/bin:/bin",
        "--setenv", "HOME", "/tmp", "--setenv", "DOTNET_ROOT", "/opt/dotnet",
        "--setenv", "DOTNET_CLI_TELEMETRY_OPTOUT", "1",
        "--setenv", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1",
        "--setenv", "DOTNET_NOLOGO", "1",
        "--setenv", "DOTNET_CLI_USE_MSBUILD_SERVER", "0",
    ]
    # Protect every supplied input except the intentionally editable solution.
    for relative in manifest["snapshot_sha256"]:
        path = Path(relative)
        if path.parts[0] != args.session or path.name == "Solution.cs":
            continue
        local = path.relative_to(args.session)
        invocation.extend(["--ro-bind", str(session / local), str(Path("/work") / local)])
    invocation.extend(["--chdir", "/work", "--", *command])
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    try:
        result = subprocess.run(invocation, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, text=True, timeout=args.timeout)
        output, code = result.stdout, result.returncode
    except subprocess.TimeoutExpired as error:
        output = error.stdout or b""
        if isinstance(output, bytes):
            output = output.decode(errors="replace")
        output += "\nConsumer command timed out.\n"
        code = 124
    after = capture(session, manifest, audit, sequence + ":after")
    (audit / f"{sequence}.log").write_text(output)
    (audit / f"{sequence}.json").write_text(json.dumps({
        "command": command, "started_utc": started, "exit_code": code,
        "before": before, "after": after,
        "isolation": "bubblewrap filesystem/PID/network namespaces; only one consumer session and system toolchain mounted",
    }, indent=2) + "\n")
    sys.stdout.write(output)
    return code


if __name__ == "__main__":
    raise SystemExit(main())
