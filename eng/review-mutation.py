#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


def find_method_range(source: Path, signature: str) -> tuple[int, int]:
    lines = source.read_text(encoding="utf-8").splitlines()
    start = next((index for index, line in enumerate(lines, 1) if signature in line), None)
    if start is None:
        raise ValueError(f"Could not find {signature!r} in {source}.")

    depth = 0
    opened = False
    for index in range(start, len(lines) + 1):
        for char in lines[index - 1]:
            if char == "{":
                depth += 1
                opened = True
            elif char == "}":
                depth -= 1
                if opened and depth == 0:
                    return start, index
    raise ValueError(f"Could not determine method extent for {signature!r} in {source}.")


def find_report(root: Path) -> Path:
    candidates = sorted(root.rglob("*.json"))
    for candidate in candidates:
        try:
            value = json.loads(candidate.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        if isinstance(value, dict) and isinstance(value.get("files"), dict):
            return candidate
    raise FileNotFoundError(f"No Stryker JSON report with a files object was found below {root}.")


def line_of(mutant: dict) -> int | None:
    location = mutant.get("location") or {}
    start = location.get("start") or {}
    value = start.get("line")
    return int(value) if isinstance(value, int) else None


def main() -> int:
    parser = argparse.ArgumentParser(description="Require Stryker to challenge the order commitment state invariant.")
    parser.add_argument("--root", default="artifacts/mutation-orders")
    parser.add_argument("--source", default="modules/orders/Application.Orders/OrderDraft.cs")
    parser.add_argument("--output", default="artifacts/mutation-orders/commit-invariant-review.txt")
    args = parser.parse_args()

    report = find_report(Path(args.root))
    source = Path(args.source)
    method_start, method_end = find_method_range(source, "AssessCommit(")
    data = json.loads(report.read_text(encoding="utf-8"))

    file_entry = None
    for name, value in data["files"].items():
        normalized = name.replace("\\", "/")
        if normalized.endswith("/" + args.source) or normalized.endswith("Application.Orders/OrderDraft.cs"):
            file_entry = value
            break
    if not isinstance(file_entry, dict):
        print("OrderDraft.cs is absent from the Stryker report.", file=sys.stderr)
        return 1

    mutants = [m for m in file_entry.get("mutants", []) if (line := line_of(m)) is not None and method_start <= line <= method_end]
    if not mutants:
        print("Stryker produced no mutants for OrderDraftLifecycle.AssessCommit.", file=sys.stderr)
        return 1

    unacceptable = {"Survived", "NoCoverage", "Timeout", "RuntimeError"}
    failures = [m for m in mutants if m.get("status") in unacceptable]
    status_counts: dict[str, int] = {}
    for mutant in mutants:
        status = str(mutant.get("status", "Unknown"))
        status_counts[status] = status_counts.get(status, 0) + 1

    lines = [
        "Order commitment mutation review",
        "================================",
        f"Stryker report: {report}",
        f"Invariant: OrderDraftLifecycle.AssessCommit lines {method_start}-{method_end}",
        "This invariant prevents stale revisions and terminal draft states from being committed.",
        "",
        "Targeted mutant statuses:",
    ]
    lines.extend(f"- {key}: {status_counts[key]}" for key in sorted(status_counts))
    lines.append("")
    if failures:
        lines.append("Unacceptable surviving/unexercised mutants:")
        for mutant in failures:
            lines.append(
                f"- id={mutant.get('id')} line={line_of(mutant)} status={mutant.get('status')} mutator={mutant.get('mutatorName')}"
            )
    else:
        lines.append("PASS: no targeted mutant survived or lacked runtime exercise.")

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(output.read_text(encoding="utf-8"), end="")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
