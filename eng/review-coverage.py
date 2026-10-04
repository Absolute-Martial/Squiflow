#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def normalize(value: str) -> str:
    return value.replace("\\", "/").lstrip("./")


def branch_counts(line: ET.Element) -> tuple[int, int]:
    coverage = line.attrib.get("condition-coverage", "")
    match = re.search(r"\((\d+)\s*/\s*(\d+)\)", coverage)
    if match:
        return int(match.group(1)), int(match.group(2))
    return (0, 0)


def main() -> int:
    parser = argparse.ArgumentParser(description="Review material-path coverage without a repository-wide vanity threshold.")
    parser.add_argument("--report", default="artifacts/coverage/report/Cobertura.xml")
    parser.add_argument("--config", default="eng/quality-critical-paths.json")
    parser.add_argument("--output", default="artifacts/coverage/material-review.txt")
    args = parser.parse_args()

    report = Path(args.report)
    config = Path(args.config)
    output = Path(args.output)
    if not report.is_file():
        print(f"Coverage review requires {report}.", file=sys.stderr)
        return 2
    if not config.is_file():
        print(f"Coverage review requires {config}.", file=sys.stderr)
        return 2

    critical = json.loads(config.read_text(encoding="utf-8"))["criticalPaths"]
    root = ET.parse(report).getroot()
    classes: dict[str, list[ET.Element]] = {}
    for cls in root.findall(".//class"):
        filename = normalize(cls.attrib.get("filename", ""))
        if filename:
            classes.setdefault(filename, []).append(cls)

    failures: list[str] = []
    lines_out = [
        "Material coverage review",
        "========================",
        "This gate verifies that named money/authority paths are exercised and reports their uncovered surface.",
        "It intentionally does not turn repository-wide coverage into a vanity percentage.",
        "",
    ]

    for entry in critical:
        path = normalize(entry["path"])
        matching_names = [
            filename for filename in classes
            if filename == path or filename.endswith("/" + path)
        ]
        if not matching_names:
            suffix = "/".join(path.split("/")[-2:])
            matching_names = [
                filename for filename in classes
                if filename == suffix or filename.endswith("/" + suffix)
            ]
        if len(matching_names) > 1:
            failures.append(f"{path}: coverage filename match is ambiguous ({', '.join(sorted(matching_names))})")
            lines_out.append(f"FAIL {path} — ambiguous coverage filename match")
            continue
        flattened = classes.get(matching_names[0], []) if matching_names else []
        if not flattened:
            failures.append(f"{path}: absent from coverage report")
            lines_out.append(f"FAIL {path} — absent from coverage report")
            continue

        source_lines: dict[int, int] = {}
        covered_branches = total_branches = 0
        for cls in flattened:
            for line in cls.findall("./lines/line"):
                number = int(line.attrib["number"])
                hits = int(line.attrib.get("hits", "0"))
                source_lines[number] = max(source_lines.get(number, 0), hits)
                covered, total = branch_counts(line)
                covered_branches += covered
                total_branches += total

        total_lines = len(source_lines)
        covered_lines = sum(1 for hits in source_lines.values() if hits > 0)
        uncovered = sorted(number for number, hits in source_lines.items() if hits == 0)
        if total_lines == 0 or covered_lines == 0:
            failures.append(f"{path}: no executable line was covered")
        if total_branches > 0 and covered_branches == 0:
            failures.append(f"{path}: branches exist but none were exercised")

        uncovered_preview = ",".join(str(number) for number in uncovered[:20]) or "none"
        if len(uncovered) > 20:
            uncovered_preview += f",… (+{len(uncovered) - 20})"
        branch_text = f"{covered_branches}/{total_branches}" if total_branches else "n/a"
        status = "PASS" if not any(item.startswith(path + ":") for item in failures) else "FAIL"
        lines_out.extend([
            f"{status} {path}",
            f"  reason: {entry['reason']}",
            f"  executable lines covered: {covered_lines}/{total_lines}",
            f"  branches covered: {branch_text}",
            f"  uncovered lines: {uncovered_preview}",
            "",
        ])

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines_out), encoding="utf-8")
    print(output.read_text(encoding="utf-8"), end="")
    if failures:
        print("Material coverage review failed:", file=sys.stderr)
        for failure in failures:
            print(f"- {failure}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
