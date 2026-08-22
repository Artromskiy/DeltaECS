#!/usr/bin/env python3
"""Capture one JIT probe and emit a compact instruction review in Markdown."""

from __future__ import annotations

import argparse
import bisect
import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path
from urllib.parse import quote


ROOT = Path(__file__).resolve().parents[1]
RUNNER = ROOT / "benchmarks" / "run-jit-disasm.sh"
PDB_MAPPER = ROOT / "benchmarks" / "JitSourceMap" / "JitSourceMap.csproj"

PATTERNS = (
    ("blr", re.compile(r"\bblr\b"), "P1"),
    ("bl", re.compile(r"\bbl\s+"), "P2"),
    ("bhs", re.compile(r"\bbhs\b"), "P2"),
    ("branch", re.compile(r"\b(?:b|beq|bne|blo|cbnz|cbz|tbnz|tbz)\b"), "P2"),
    ("sbfiz", re.compile(r"\bsbfiz\b"), "P1"),
    ("umull", re.compile(r"\bumull\b"), "P2"),
    ("ldr", re.compile(r"\bldr(?:b|h|sb|sh)?\b"), "P2"),
    ("str", re.compile(r"\bstr(?:b|h)?\b"), "P2"),
    ("ldp/stp", re.compile(r"\b(?:ldp|stp)\b"), "P1"),
)

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run the repository JIT probe and write an instruction summary."
    )
    parser.add_argument("--method", required=True, help="DOTNET_JitDisasm pattern")
    parser.add_argument("--filter", help="BenchmarkDotNet filter; defaults to --method")
    parser.add_argument("--project", help="Probe project passed to run-jit-disasm.sh")
    parser.add_argument("--framework", default="net8.0", help="Target framework; defaults to net8.0")
    parser.add_argument("--job", default="dry", help="BDN job; dry is the default")
    parser.add_argument(
        "--mode",
        choices=("release", "debug"),
        default="release",
        help="release: summary only; debug: summary plus source/assembly mapping",
    )
    parser.add_argument(
        "--configuration",
        choices=("Release", "Debug"),
        help="Managed build configuration; defaults to Release",
    )
    parser.add_argument(
        "--checked-jit",
        type=Path,
        help="Version-compatible Debug/Checked libclrjit; env/artifacts discovery is the fallback",
    )
    parser.add_argument("--assembly", type=Path, help="Managed assembly containing the mapped method")
    parser.add_argument("--pdb", type=Path, help="Portable PDB matching --assembly")
    parser.add_argument("--no-build", action="store_true", help="Reuse the existing probe DLL")
    parser.add_argument("--output", type=Path, help="Raw JIT output path")
    parser.add_argument("--report", type=Path, help="Markdown report path")
    parser.add_argument(
        "--auto-open",
        action="store_true",
        help="Open the generated Markdown report in Obsidian",
    )
    parser.add_argument(
        "--obsidian-vault",
        type=Path,
        help="Obsidian vault used by --auto-open; macOS config is used by default",
    )
    return parser.parse_args()


def absolute(path: Path) -> Path:
    return path if path.is_absolute() else ROOT / path


def safe_name(method: str) -> str:
    return re.sub(r"[^A-Za-z0-9._-]", "_", method)


def discover_checked_jit() -> Path | None:
    candidates = sorted(
        ROOT.glob(
            "artifacts/toolchains/runtime-v*/artifacts/bin/coreclr/"
            "osx.arm64.Checked/libclrjit.dylib"
        ),
        reverse=True,
    )
    return candidates[0] if candidates else None


def capture(args: argparse.Namespace, output: Path) -> None:
    configuration = args.configuration or "Release"
    command = [
        str(RUNNER),
        "--method",
        args.method,
        "--filter",
        args.filter or args.method,
        "--job",
        args.job,
        "--configuration",
        configuration,
        "--framework",
        args.framework,
        "--output",
        str(output),
    ]
    if args.project:
        command.extend(("--project", str(absolute(Path(args.project)))))
    if args.no_build:
        command.append("--no-build")
    if args.mode == "debug":
        checked_jit = args.checked_jit
        if checked_jit is None and os.environ.get("DELTAECS_CHECKED_JIT"):
            checked_jit = Path(os.environ["DELTAECS_CHECKED_JIT"])
        if checked_jit is None:
            checked_jit = discover_checked_jit()
        if checked_jit is None:
            raise RuntimeError(
                "--mode debug requires a version-matched Checked JIT. Pass "
                "--checked-jit <path>, set DELTAECS_CHECKED_JIT, or build it under "
                "artifacts/toolchains/runtime-v*/artifacts/bin/coreclr/"
                "osx.arm64.Checked. A managed Debug build does not provide DOTNET_JitDump."
            )
        command.extend(("--jit-dump", "--checked-jit", str(absolute(checked_jit))))

    subprocess.run(command, cwd=ROOT, check=True)


def first_assembly_block(text: str) -> tuple[list[str], int, int]:
    start = text.find("; Assembly listing for method")
    if start < 0:
        raise RuntimeError("No JIT assembly listing found in the probe output.")

    end = text.find("; Total bytes of code", start)
    if end < 0:
        raise RuntimeError("JIT listing has no code-size footer.")

    size_match = re.search(r"; Total bytes of code (\d+)", text[end:])
    # The footer is after the block; keep the size lookup separate so the
    # instruction scan cannot accidentally include BDN output.
    if size_match is None:
        size = 0
    else:
        size = int(size_match.group(1))

    # Retail JitDisasm prints instructions before the footer. Checked JitDump
    # prints its final INxxxx/native-offset stream immediately after it.
    next_listing = text.find("; Assembly listing for method", end + 1)
    checked_search_end = next_listing if next_listing >= 0 else len(text)
    checked_region = text[end:checked_search_end]
    checked_start = re.search(r"^G_M[^:\r\n]+:", checked_region, re.MULTILINE)
    if checked_start is not None:
        block_start = end + checked_start.start()
        block_end = text.find("*************** Finishing PHASE Emit code", block_start)
        if block_end < 0:
            block_end = text.find("*************** In genIPmappingGen()", block_start)
        if block_end < 0:
            raise RuntimeError("Checked JIT listing has no emitted-code terminator.")
    else:
        block_start = start
        block_end = end

    block = text[block_start:block_end]
    first_raw_line = text.count("\n", 0, block_start) + 1
    return block.splitlines(), size, first_raw_line


def vscode_href(target: Path, line: int | None = None) -> str:
    encoded_path = quote(str(target.resolve()), safe="/._-")
    suffix = f":{line}" if line is not None else ""
    return f"vscode://file{encoded_path}{suffix}"


def assembly_instructions(
    lines: list[str], first_raw_line: int
) -> tuple[list[tuple[int, int, str]], int]:
    instructions: list[tuple[int, int, str]] = []
    native_offset = 0
    for relative_line, line in enumerate(lines, 0):
        checked = re.match(
            r"IN[0-9A-Fa-f]+:\s+([0-9A-Fa-f]+)\s+([a-z][a-z0-9._]*)\s*(.*)",
            line,
        )
        if checked is not None:
            offset_text, mnemonic, operands = checked.groups()
            if mnemonic == "align":
                continue
            offset = int(offset_text, 16)
            instruction = f"{mnemonic} {operands}".rstrip()
            instructions.append((first_raw_line + relative_line, offset, instruction))
            native_offset = max(native_offset, offset + 4)
            continue

        alignment = re.match(r"\s+align\s+\[(\d+) bytes", line)
        if alignment:
            native_offset += int(alignment.group(1))
            continue

        if not re.match(r"\s+[a-z][a-z0-9._]*\s", line):
            continue

        instructions.append((first_raw_line + relative_line, native_offset, line.strip()))
        native_offset += 4

    return instructions, native_offset


def parse_ip_mappings(text: str) -> list[tuple[int, int | None]]:
    sections: list[list[tuple[int, int | None]]] = []
    for marker in re.finditer(r"In genIPmappingGen\(\)", text):
        end = text.find("***************", marker.end())
        section = text[marker.end() : end if end >= 0 else len(text)]
        mappings: list[tuple[int, int | None]] = []
        for match in re.finditer(
            r"IL offs\s+(PROLOG|EPILOG|NO_MAP|0x[0-9A-Fa-f]+)\s*:\s*0x([0-9A-Fa-f]+)",
            section,
        ):
            il_text, native_text = match.groups()
            il_offset = int(il_text, 16) if il_text.startswith("0x") else None
            mappings.append((int(native_text, 16), il_offset))
        if mappings:
            sections.append(mappings)

    if not sections:
        raise RuntimeError(
            "No genIPmappingGen table found. Use a version-compatible Debug/Checked libclrjit."
        )

    mappings = max(sections, key=lambda section: sum(il is not None for _, il in section))
    return sorted(mappings, key=lambda pair: pair[0])


def method_identity(lines: list[str], fallback: str) -> tuple[str, str, str]:
    signature = next(
        (
            line.removeprefix("; Assembly listing for method ").split(" (")[0]
            for line in lines
            if line.startswith("; Assembly listing for method ")
        ),
        fallback,
    )
    before_arguments = signature.split("(", 1)[0]
    if ":" not in before_arguments:
        raise RuntimeError(f"Cannot parse declaring type and method from '{signature}'.")
    declaring_type, method = before_arguments.rsplit(":", 1)
    return signature, declaring_type, method


def default_managed_paths(args: argparse.Namespace) -> tuple[Path, Path]:
    project = absolute(
        Path(args.project)
        if args.project
        else Path("benchmarks/DeltaECS.MicroBenchmarks/DeltaECS.MicroBenchmarks.csproj")
    )
    configuration = args.configuration or "Release"
    assembly = absolute(args.assembly) if args.assembly else (
        project.parent / "bin" / configuration / args.framework / f"{project.stem}.dll"
    )
    pdb = absolute(args.pdb) if args.pdb else assembly.with_suffix(".pdb")
    return assembly, pdb


def load_sequence_points(
    args: argparse.Namespace, declaring_type: str, method: str
) -> list[dict[str, object]]:
    assembly, pdb = default_managed_paths(args)
    if not assembly.is_file() or not pdb.is_file():
        raise RuntimeError(f"Managed assembly/PDB not found: {assembly}, {pdb}")

    environment = os.environ.copy()
    environment.update({"NuGetAudit": "false", "RestoreIgnoreFailedSources": "true"})
    command = [
        "dotnet",
        "run",
        "--project",
        str(PDB_MAPPER),
        "-c",
        "Release",
        "--",
        str(assembly),
        str(pdb),
        declaring_type,
        method,
    ]
    result = subprocess.run(
        command,
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
        env=environment,
    )
    return json.loads(result.stdout)


def source_mapping(
    instructions: list[tuple[int, int, str]],
    ip_mappings: list[tuple[int, int | None]],
    sequence_points: list[dict[str, object]],
) -> dict[int, tuple[Path, int, int, int]]:
    native_starts = [native for native, _ in ip_mappings]
    points = sorted(sequence_points, key=lambda point: int(point["IlOffset"]))
    point_offsets = [int(point["IlOffset"]) for point in points]
    result: dict[int, tuple[Path, int, int, int]] = {}

    for raw_line, native_offset, _ in instructions:
        mapping_index = bisect.bisect_right(native_starts, native_offset) - 1
        if mapping_index < 0:
            continue
        il_offset = ip_mappings[mapping_index][1]
        if il_offset is None:
            continue
        point_index = bisect.bisect_right(point_offsets, il_offset) - 1
        if point_index < 0:
            continue
        point = points[point_index]
        document = Path(str(point["Document"]))
        if not document.is_absolute():
            document = ROOT / document
        result[raw_line] = (
            document,
            int(point["Line"]),
            il_offset,
            native_offset,
        )
    return result


def instruction_rows(
    instructions: list[tuple[int, int, str]],
    assembly_path: Path,
    sources: dict[int, tuple[Path, int, int, int]],
) -> tuple[list[str], list[str]]:
    summary_rows: list[str] = []
    detail_rows: list[str] = []
    for name, pattern, priority in PATTERNS:
        matches = [item for item in instructions if pattern.search(item[2])]
        if not matches:
            continue

        summary_rows.append(f"| `{name}` | {len(matches)} | **{priority}** |")

        for assembly_line, native_offset, instruction in matches:
            assembly_link = f"[L{assembly_line}]({vscode_href(assembly_path, assembly_line)})"
            mapped = sources.get(assembly_line)
            if mapped is None:
                source_link = f"— (`native 0x{native_offset:X}`; no sequence point)"
            else:
                source, source_line, il_offset, _ = mapped
                source_link = (
                    f"[{source.name}:{source_line}]({vscode_href(source, source_line)}) "
                    f"(`IL 0x{il_offset:X}`)"
                )
            detail_rows.append(
                f"| `{name}` | {source_link} | {assembly_link} `{instruction}` | **{priority}** |"
            )

    return summary_rows, detail_rows


def write_report(args: argparse.Namespace, output: Path, report: Path) -> None:
    text = output.read_text(encoding="utf-8", errors="replace")
    lines, code_size, first_raw_line = first_assembly_block(text)
    assembly_path = output.resolve()
    instructions, calculated_size = assembly_instructions(lines, first_raw_line)
    method_match, declaring_type, method = method_identity(text.splitlines(), args.method)
    sources: dict[int, tuple[Path, int, int, int]] = {}
    if args.mode == "debug":
        sources = source_mapping(
            instructions,
            parse_ip_mappings(text),
            load_sequence_points(args, declaring_type, method),
        )
    summary_rows, detail_rows = instruction_rows(instructions, assembly_path, sources)
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(
        "\n".join(
            [
                "| Operation | Count | Priority |",
                "|:---|---:|:---:|",
                *summary_rows,
                *(
                    [
                        "",
                        "| Operation | Source | Assembly | Priority |",
                        "|:---|:---|:---|:---:|",
                        *detail_rows,
                        "",
                    ]
                    if args.mode == "debug"
                    else []
                ),
                "---",
                "",
                "## Probe details",
                "",
                f"- Mode: **{args.mode}**",
                f"- Method: `{method_match}`",
                f"- Assembly: [{output.name}]({vscode_href(assembly_path)})",
                f"- First emitted code block: **{code_size} B**",
                f"- Reconstructed ARM64 instruction span: **{calculated_size} B**",
                "- Counts are for the first emitted JIT block; repeated BDN parameter blocks are ignored.",
                "- `bhs`/branches may belong to setup or chunk transitions, not necessarily the slot loop.",
                "- Code size does not prove cache misses or throughput.",
                *(
                    [
                        "- Source links are approximate JIT IP-map → IL offset → Portable PDB sequence-point mappings.",
                        "- Prolog, epilog and `NO_MAP` native ranges intentionally have no source link.",
                    ]
                    if args.mode == "debug"
                    else []
                ),
                "",
            ]
        ),
        encoding="utf-8",
    )


def configured_obsidian_vault() -> Path | None:
    config = Path.home() / "Library" / "Application Support" / "obsidian" / "obsidian.json"
    if not config.is_file():
        return None

    try:
        vaults = json.loads(config.read_text(encoding="utf-8")).get("vaults", {})
    except (OSError, json.JSONDecodeError):
        return None

    candidates = [
        (int(value.get("ts", 0)), Path(value["path"]))
        for value in vaults.values()
        if value.get("path")
    ]
    open_vaults = [candidate for candidate in candidates if candidate[1].is_dir()]
    if not open_vaults:
        return None

    return max(open_vaults, key=lambda candidate: candidate[0])[1]


def prepare_obsidian_report(report: Path, requested_vault: Path | None) -> Path:
    vault = absolute(requested_vault) if requested_vault else configured_obsidian_vault()
    if vault is None or not vault.is_dir():
        raise RuntimeError(
            "No Obsidian vault was found. Pass --obsidian-vault <path> or open a vault once."
        )

    report = report.resolve()
    try:
        report.relative_to(vault.resolve())
        return report
    except ValueError:
        target = vault / "DeltaECS Reports" / "jit-disasm" / report.name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(report, target)
        print(f"Copied report into Obsidian vault: {target}")
        return target


def open_report(report: Path, requested_vault: Path | None) -> None:
    if sys.platform == "darwin":
        obsidian_report = prepare_obsidian_report(report, requested_vault)
        uri = f"obsidian://open?path={quote(str(obsidian_report), safe='')}"
        command = ["open", uri]
    elif sys.platform.startswith("linux"):
        command = ["xdg-open", str(report)]
    elif os.name == "nt":
        os.startfile(report)  # type: ignore[attr-defined]
        return
    else:
        raise RuntimeError(f"No automatic viewer is configured for {sys.platform}.")

    if shutil.which(command[0]) is None:
        raise RuntimeError(f"Viewer command not found: {command[0]}")

    subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def main() -> int:
    args = parse_args()
    output = absolute(args.output or Path("artifacts/jit-disasm") / f"{safe_name(args.method)}.txt")
    report = absolute(args.report or output.with_suffix(".md"))
    output.parent.mkdir(parents=True, exist_ok=True)
    try:
        capture(args, output)
        write_report(args, output, report)
    except (RuntimeError, subprocess.CalledProcessError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1
    print(f"JIT report written to {report}")
    if args.auto_open:
        open_report(report, args.obsidian_vault)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
