# Platform profiles, configuration and earned incident controls

Task ID: UIA-007
Phase: 05-admin-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: UIA-003, ADM-024, OPS-006, OPS-011, OPS-012, OPS-013
Conditional dependencies: ADM-029 when a key-lifecycle control is selected; ADM-033 when feature release controls are selected
Release requirement: REQUIRED

## Outcome

Expose the selected qualified platform profile/ceiling/configuration and incident/work controls with material diffs, explicit operations and truthful provider outcomes.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/admin/ADMIN_SURFACES.md`; `docs/architecture/TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md`; `docs/requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md`; `docs/server/WORKER_RUNTIME_AND_SCHEDULING.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write only selected Admin profile/config/status/control screens and tests. No generic set-anything, run-SQL, show-key, force-success or mark-job/payment-complete controls; Worker implementation is outside this catalog phase.

## Decisions/prerequisites

Backend prerequisites: exact qualified profile publication, entitlement/limit ceilings, provider config redaction, incident status and selected application controls. Worker pause/drain/retry/quarantine controls are conditional on an already qualified Worker/control backend. Implementation waits GATE-002; decisions may be prepared earlier. Admin host delivery also waits GATE-003.

## Acceptance and edge cases

- Diffs explain data, permission, durable-work and supported version effects.
- Publishing/enabling never silently grants roles or loses accepted work.
- Usage/limit views use authoritative accounting, not sampled telemetry.
- Secret configuration has write-only/redacted safe projections.
- Incident retry/drain/quarantine reports actual authoritative operation state.
- Admin controls remain usable without CoreApi where their dependencies allow.
- Unimplemented controls are absent, not fake actionable buttons.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review field-specific authority, bounded operational commands and sensitive provider diagnostics. Each high-risk action consumes qualified backend freshness/approval requirements, not just a hidden button.

## Dynamic verification and unavailable-environment handling

Run actual backend/provider Playwright tests for selected config/profile effects, pending/unknown controls and CoreApi outage. Keep conditional controls gated until real runtime evidence exists. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.0.1. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Select the precise qualified platform controls.
Read profile, limits, config and incident contracts.
Build purpose-built diffs and bounded commands.
Keep secrets redacted and permissions separate.
Show pending/unknown provider outcomes honestly.
Require accepted risk controls before mutations.
Test configuration races and data-plane outage.
Do not introduce Worker or generic infrastructure editors.
Leave conditional controls absent until qualified.
Return source ZIP and selected-control evidence.
```
