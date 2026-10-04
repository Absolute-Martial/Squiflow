# Separate Platform Admin Web assignments

This is a future assignment catalog, not an implementation or qualification claim. Current truth remains in `README.IMPLEMENTATION.md`; product version stays v0.1.0. Catalog status expresses readiness to assign after prerequisites, not PRODUCTION_HONEST/BLOCKED runtime state.

Backend gates precede frontend implementation. This application uses AdminApi directly and has its own identity, registered Admin-device, session and deployment boundary. Tenant Web phase completion is delivery sequencing, never an ordinary Admin runtime dependency.

Use shared [agent rules](../AGENT_RULES.md), [handoff](../HANDOFF_AND_INTEGRATION.md) and the [generated phase index](TASK_INDEX.md). Backend dependencies and selected controls are already bound to exact task IDs. No frontend can hide an absent backend behind a screen.

| Task | Outcome | Status | Release requirement |
|---|---|---|---|
| [UIA-001](UIA-001-decide-separate-platform-admin-web-topology.md) | Decide separate Platform Admin Web topology | DECISION_REQUIRED | REQUIRED |
| [UIA-002](UIA-002-admin-login-session-and-registered-device-binding.md) | Admin login, session and registered-device binding | READY_AFTER_DEPENDENCIES | REQUIRED |
| [UIA-003](UIA-003-admin-shell-access-status-and-health-views.md) | Admin shell, access status and health views | READY_AFTER_DEPENDENCIES | REQUIRED |
| [UIA-004](UIA-004-tenant-provisioning-and-exact-identity-onboarding.md) | Tenant provisioning and exact identity onboarding | READY_AFTER_DEPENDENCIES | REQUIRED |
| [UIA-005](UIA-005-membership-tenant-lifecycle-and-accepted-delegation-screens.md) | Membership, tenant lifecycle and accepted delegation screens | READY_AFTER_DEPENDENCIES | REQUIRED |
| [UIA-006](UIA-006-platform-operator-and-admin-device-lifecycle-screens.md) | Platform operator and Admin-device lifecycle screens | READY_AFTER_DEPENDENCIES | REQUIRED |
| [UIA-007](UIA-007-platform-profiles-configuration-and-earned-incident-controls.md) | Platform profiles, configuration and earned incident controls | READY_AFTER_DEPENDENCIES | REQUIRED |
| [UIA-008](UIA-008-step-up-recovery-audit-and-high-risk-failure-ux.md) | Step-up, recovery, audit and high-risk failure UX | READY_AFTER_DEPENDENCIES | REQUIRED |
| [UIA-009](UIA-009-independent-admin-web-build-and-browser-qualification.md) | Independent Admin Web build and browser qualification | READY_AFTER_DEPENDENCIES | REQUIRED |

GPT-6.1 Sol owns identity, session, authority and new capability flows. Luna or an available Flash model can handle fixed-contract display/component slices and read-only reviews with stronger integration review. Money/authority changes escalate; gate qualification remains strong-model/owner work. See [model routing](../MODEL_ROUTING.md).

Every task requires its scoped static security review, exact dynamic evidence, lasting regression guard and source-only ZIP. Receiving integration runs .NET 10 and actual Playwright/provider checks. Missing environments remain explicit; no SDK/test pass is claimed by writing this catalog. Workstation, SyncApi and Guard are deferred future boundaries and are not implemented by these phases. No folder/project is created merely to mirror this plan.
