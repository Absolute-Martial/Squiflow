# Phase 6E — Failure Independence and Recovery

## Required independence

Prove for the process boundaries actually introduced:

- Core/Web/Sync API stopped while Admin API remains healthy;
- Admin API stopped while ordinary tenant business remains healthy;
- Worker stopped while authoritative commits continue and durable backlog accumulates safely;
- Worker restart resumes without duplicate semantic effect;
- Admin API failure does not fail open into tenant/platform authority;
- public edge failure does not remove the private infrastructure recovery path.

If only a subset of these runtimes exists in the current slice, prove that subset now and retain the remaining checks for the phase gate where those runtimes become operational.

## Break-glass

If Admin API itself cannot run, use the documented private infrastructure recovery procedure for restart/redeploy/DB/OpenBao/network/config recovery. This is not a hidden business API.

## OpenBao dependency

Operations that genuinely require online Transit/key-service crypto may fail closed when the service is unavailable. Ordinary provisioned business paths should not be made continuously dependent on Admin UI/API merely for architectural symmetry.

## Exit gate

The new processes add fault containment rather than multiplying single points of failure or hidden bypasses.