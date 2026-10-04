#!/usr/bin/env python3
"""Qualify fresh parallel Reaper startup and process-exit cleanup; never retry tests."""

import argparse
import json
import os
from pathlib import Path
import subprocess
import signal
import time
import uuid
import xml.etree.ElementTree as ET


def main():
    arguments = argparse.ArgumentParser(description=__doc__)
    arguments.add_argument("--abort-after-ready", action="store_true",
                           help="Deliberately fail after observing a created resource to test harness failure cleanup.")
    options = arguments.parse_args()
    root = Path(__file__).resolve().parent.parent
    evidence = root / "artifacts/verification/ops-022" / ("startup-" + uuid.uuid4().hex)
    evidence.mkdir(parents=True)
    run_id = evidence.name
    environment = dict(os.environ, APPLICATION_STARTUP_PROBE_RUN=run_id, APPLICATION_STARTUP_PROBE_EVIDENCE=str(evidence))
    project = root / "tests/integration/Application.IdentityAccess.Postgres.Tests"
    processes = []
    sessions = set()
    result = None
    deadline = time.monotonic() + 180
    def resources():
        sessions.update(str(uuid.UUID(path.stem)) for path in evidence.glob("*.session"))
        result = subprocess.run(["docker", "ps", "--all", "--filter", f"label=application.startup-probe={run_id}",
                                 "--format", '{{.ID}}\t{{.Label "org.testcontainers.resource-reaper-session"}}'],
                                check=True, capture_output=True, text=True, timeout=10)
        for line in result.stdout.splitlines():
            sessions.add(line.split("\t")[1])
        return result.stdout.splitlines()

    try:
        for index in range(6):
            log = (evidence / f"process-{index}.log").open("w")
            command = ["dotnet", "test", str(project), "--configuration", "Release", "--no-build", "--no-restore",
                       "--filter", "FullyQualifiedName~TestcontainersLifecycleTests",
                       "--logger", f"trx;LogFileName=process-{index}.trx", "--results-directory", str(evidence)]
            try:
                process = subprocess.Popen(command, cwd=root, env=environment, stdout=log, stderr=subprocess.STDOUT,
                                           start_new_session=True)
            except BaseException:
                log.close()
                raise
            processes.append((process, log))
        while any(process.poll() is None for process, _ in processes):
            observed = resources()
            if options.abort_after_ready and observed:
                raise RuntimeError("Deliberate harness interruption after observing a Docker resource.")
            if time.monotonic() > deadline:
                raise TimeoutError("Fresh concurrent startup exceeded 180 seconds.")
            time.sleep(0.1)
        for _, log in processes:
            log.close()
        exits = [process.returncode for process, _ in processes]
        if exits != [0] * 6:
            raise RuntimeError(f"Test process exits: {exits}; inspect {evidence}")
        summaries = []
        namespace = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
        for index in range(6):
            counters = ET.parse(evidence / f"process-{index}.trx").find("t:ResultSummary/t:Counters", namespace).attrib
            if any(counters.get(key) != value for key, value in
                   {"total": "4", "passed": "4", "failed": "0", "notExecuted": "0"}.items()):
                raise RuntimeError(f"Unexpected process-{index} counters: {counters}; inspect {evidence}")
            summaries.append(counters)
        if len(sessions) != 6 or "" in sessions:
            raise RuntimeError(f"Expected six distinct observed Reaper sessions: {sessions}; inspect {evidence}")
        result = {"run": run_id, "process_exits": exits, "sessions": sorted(sessions), "summaries": summaries}
    finally:
        # A second catchable termination signal must not interrupt cleanup.
        signal.signal(signal.SIGTERM, signal.SIG_IGN)
        signal.signal(signal.SIGHUP, signal.SIG_IGN)
        for process, log in processes:
            # A fresh Linux session owns the dotnet CLI, test runner and host;
            # terminate the entire owned group even if its leader already exited.
            try:
                os.killpg(process.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
        for process, log in processes:
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                process.wait(timeout=10)
            log.close()
        shutdown_deadline = time.monotonic() + 10
        cleanup_deadline = time.monotonic() + 60
        escalated = False
        while True:
            remaining = resources()
            reapers = subprocess.run(["docker", "ps", "--all", "--format", "{{.Names}}"], check=True,
                                     capture_output=True, text=True, timeout=10).stdout.splitlines()
            remaining_reapers = [name for name in reapers if name in {f"testcontainers-ryuk-{s}" for s in sessions}]
            # Zombies may briefly await reaping by the system init, but cannot
            # retain a live Ryuk connection or continue test work.
            group_ids = {process.pid for process, _ in processes}
            process_rows = subprocess.run(["ps", "-eo", "pid=,pgid=,stat="], check=True,
                                          capture_output=True, text=True, timeout=10).stdout.splitlines()
            live_children = [row for row in process_rows if int(row.split()[1]) in group_ids and not row.split()[2].startswith("Z")]
            if live_children and time.monotonic() > shutdown_deadline and not escalated:
                for group in {int(row.split()[1]) for row in live_children}:
                    try:
                        os.killpg(group, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                escalated = True
            if not remaining and not remaining_reapers and not live_children:
                break
            if time.monotonic() > cleanup_deadline:
                raise TimeoutError(f"Cleanup left resources: {remaining}, Reapers: {remaining_reapers}, children: {live_children}")
            time.sleep(0.2)
        cleanup = {"run": run_id, "sessions": sorted(sessions), "process_exits": [process.returncode for process, _ in processes],
                   "remaining_probe_containers": 0, "remaining_session_reapers": 0, "remaining_live_children": 0}
        (evidence / "cleanup.json").write_text(json.dumps(cleanup, indent=2) + "\n")
        print("Cleanup:", json.dumps(cleanup, indent=2))
        print(f"Evidence: {evidence}")

    result.update(cleanup)
    (evidence / "result.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    def terminate(signum, _frame):
        raise SystemExit(128 + signum)

    signal.signal(signal.SIGTERM, terminate)
    signal.signal(signal.SIGHUP, terminate)
    main()
