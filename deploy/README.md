# Phase-0 deployment representation

This remains intentionally a runbook, not a premature Kubernetes/Terraform/CI/CD selection.

## Executables currently justified

- `apps/web/SquiFlow.Web`
- `apps/desktop/workstation/SquiFlow.Workstation`
- `apps/desktop/guard/SquiFlow.Guard`
- `services/core-api/SquiFlow.CoreApi`

`services/web-api`, `services/sync-api`, `services/admin-api`, `services/worker`, Admin Web, Diagnostics, Maintenance, Sync and Document helper executables are not created until their owning responsibility is implemented and earns the process boundary.

## Development startup

1. Build locally from `SquiFlow.sln`.
2. Start CoreApi and confirm `/health/live` and `/health/ready`.
3. Start tenant Web and confirm `/health/live`.
4. Start Workstation directly for UI development, or launch Guard with the built Workstation executable path to exercise external supervision.

Guard can receive the Workstation path as its first argument or from `SQUIFLOW_WORKSTATION_PATH`.

## Current limits

This Phase-0 runbook does not claim production deployment, TLS termination, PostgreSQL, SQLite, synchronization, Worker, Admin control plane, backup/restore, update orchestration or process-manager integration.

Repository CI/CD is intentionally absent under the current documentation-first rewrite directive. Environment-specific addresses, service definitions and production supervision remain later deployment decisions. Do not place credentials in this directory.
