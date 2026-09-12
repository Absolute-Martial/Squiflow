# Phase-0 deployment representation

This is intentionally a runbook, not a premature Kubernetes/Terraform selection.

## Executables

- `SquiFlow.Web`
- `SquiFlow.Workstation`
- `SquiFlow.Guard`
- `SquiFlow.CoreApi`

`services/worker`, Admin API and Admin Web are not created until their owning phase.

## Development startup order

1. Start Core API and confirm `/health`.
2. Start tenant Web and confirm `/health`.
3. Start Workstation directly for UI development, or start Guard with the Workstation executable path to exercise supervision.

Environment-specific addresses, service definitions, TLS termination and production process supervision remain deployment decisions. Do not place credentials in this directory.
