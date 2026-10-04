# ADM-002 — Qualify incoming protected request budgets

Task ID: ADM-002
Phase: 01-admin-platform
Status: VERIFY_EXISTING
Model: GPT-6.1 Sol
Dependencies: ADM-001
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Verify the incoming bounded deadline contract and repair demonstrated cancellation or disclosure defects while preserving incoming work and health classification.

## Current basis and canonical inputs

Incoming Program metadata, AdminEndpointAccess, AdminApiRequestBudgets and RequestBudgetTests introduce cooperative protected-route deadlines. README and the new focused owner describe unqualified dynamic evidence; these files belong to the incoming work. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [ADMIN_API_REQUEST_BUDGETS.md](../../implementation/ADMIN_API_REQUEST_BUDGETS.md); [AdminApiRequestBudgets.cs](../../../services/admin-api/Application.AdminApi/Composition/AdminApiRequestBudgets.cs); [RequestBudgetTests.cs](../../../tests/integration/Application.AdminApi.Tests/RequestBudgetTests.cs).

## Scope and exclusions

Allowed areas: Incoming AdminApi budget/configuration/metadata files and request-budget test paths; targeted owner corrections only. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Read the diff before editing. Adopt existing 1–120-second configuration validation only after its tests agree; do not create a second timeout framework. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Every classified protected route receives the budget.
- Public live/ready routes retain their declared behavior.
- Expiry before headers returns safe no-store 504/request_timeout.
- Client abort differs from server deadline and releases capacity.
- Started responses do not receive a second invented success/error body.
- Provider or database work observes cancellation; a committed receipt remains replayable after response loss.

## Security/static review

Review middleware ordering, native timeout logs, trace identifiers and cancellation propagation. Deadlines do not promise rollback of already committed effects. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Run RequestBudgetTests through the real ASP.NET pipeline and repeat normal ./eng/verify.sh. Include real PostgreSQL/OpenFGA cancellation where the contract crosses those boundaries. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-002: Qualify incoming protected request budgets.
Read AGENTS.md and linked focused owners.
Use the declared model and shared rules.
Verify dependency handoffs against current evidence.
Preserve incoming files and restrict edits to the allowed areas.
Report unresolved policy; never invent defaults.
Reuse existing boundaries and framework mechanisms.
Implement or verify the exact outcome and acceptance cases above.
Perform static security review and the named real-boundary checks.
List exact unrun checks; do not claim success.
Return a source-only ZIP; no commit/push.
```
