# Phase 06 — release checkpoints

| Gate | Placement | Evidence owner |
|---|---|---|
| [GATE-002](GATE-002-qualify-backend-readiness-for-frontend-work.md) | After qualified required Admin/commercial/operations branches; before tenant Web runtime implementation | Accepted backend case matrix and operating evidence |
| [GATE-003](GATE-003-accept-tenant-web-qualification.md) | After WEB-014; before Admin Web runtime implementation | Reuses WEB-014 evidence without a second feature build |
| [GATE-004](GATE-004-accept-admin-web-qualification.md) | After UIA-009 | Reuses UIA-009 and independent host/security evidence |
| [GATE-005](GATE-005-qualify-the-whole-declared-web-and-backend-release.md) | After both Web gates and integrated target qualification | Whole selected release, owner acceptance and regression mapping |

These are ordered checkpoints, not a claim that all phase 06 work happens after phase 05. Independent backend branches can proceed in parallel under file ownership. Session/topology decisions can precede GATE-002, but the gate must never depend on a frontend implementation that waits for it. Owner transfer transport can be qualified with backend contract tests before guarded Web use, avoiding a dependency cycle.

Each gate accepts the explicit required/selected-conditional release matrix and lists unselected cases/non-claims. Absent Workstation/Sync/Guard, external customer portal, tax/fiscal certification, full ERP/accounting and subscription/license semantics are not declared complete. Catalog completion, passing unit tests or an old local full gate does not qualify any release. Follow [rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md).
