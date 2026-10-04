# Phase 05-admin-web task index

Generated from full task metadata. Read the complete file and accepted dependency handoffs before dispatch.

| Task | Status | Model | Release | Dependencies |
|---|---|---|---|---|
| [UIA-001 — Decide separate Platform Admin Web topology](UIA-001-decide-separate-platform-admin-web-topology.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | none |
| [UIA-002 — Admin login, session and registered-device binding](UIA-002-admin-login-session-and-registered-device-binding.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | UIA-001, GATE-002, GATE-003 |
| [UIA-003 — Admin shell, access status and health views](UIA-003-admin-shell-access-status-and-health-views.md) | READY_AFTER_DEPENDENCIES | GPT-6 Luna (high) | REQUIRED | UIA-002 |
| [UIA-004 — Tenant provisioning and exact identity onboarding](UIA-004-tenant-provisioning-and-exact-identity-onboarding.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | UIA-003, ADM-006 |
| [UIA-005 — Membership, tenant lifecycle and accepted delegation screens](UIA-005-membership-tenant-lifecycle-and-accepted-delegation-screens.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | UIA-004, ADM-012, ADM-025 |
| [UIA-006 — Platform operator and Admin-device lifecycle screens](UIA-006-platform-operator-and-admin-device-lifecycle-screens.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | UIA-005, ADM-016, ADM-017 |
| [UIA-007 — Platform profiles, configuration and earned incident controls](UIA-007-platform-profiles-configuration-and-earned-incident-controls.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | UIA-003, ADM-024, OPS-006, OPS-011, OPS-012, OPS-013 |
| [UIA-008 — Step-up, recovery, audit and high-risk failure UX](UIA-008-step-up-recovery-audit-and-high-risk-failure-ux.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | UIA-002, UIA-007, ADM-034, ADM-023, OPS-013, OPS-015 |
| [UIA-009 — Independent Admin Web build and browser qualification](UIA-009-independent-admin-web-build-and-browser-qualification.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | UIA-002, UIA-003, UIA-004, UIA-005, UIA-006, UIA-007, UIA-008, GATE-002, GATE-003, OPS-017, OPS-021 |

Conditional prerequisites and approved writable areas are in the full task and root tasks.json.
