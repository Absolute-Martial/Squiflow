# ADM-013 — Decide first resource permission scope

Task ID: ADM-013
Phase: 01-admin-platform
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: ADM-008, ADM-012
Release requirement: REQUIRED
Cross-track prerequisites: none

## Outcome

Select one real protected operation requiring narrower scope and define its domain fact, OpenFGA relation, grant ceiling and supported query behavior.

## Current basis and canonical inputs

Branch/program and Own/Assigned scope semantics remain open. Customers program attribution already exists, but attribution is not a program-scoped permission model and must not be silently repurposed. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [OPEN_DECISIONS.md](../../decisions/OPEN_DECISIONS.md); [CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md](../../implementation/CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md); [1B_TENANT_AUTHORIZATION_ROLES_AND_DEVICES.md](../../implementation/phases/phase-1/1B_TENANT_AUTHORIZATION_ROLES_AND_DEVICES.md).

## Scope and exclusions

Allowed areas: One selected capability resource-scope decision/model POC and executable threat specification; no universal scope framework or speculative branch schema. Exclude UI implementation, unrelated capabilities and purged Parties code.

## Decisions/prerequisites

Product owner chooses the resource family and meaning of ownership/assignment. Keep resource tenant membership independent from relationship permission; no scope is accepted from a browser claim. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- Foreign-tenant object ID never discloses existence.
- A broad provider relation still cannot bypass local tenant/resource facts.
- Resource move or assignment change has an explicit freshness decision.
- List filtering agrees with detail/mutation authority.
- Missing domain scope evidence denies safely.
- Migration of existing grants does not widen authority by default.

## Security/static review

Review confused-deputy paths, object parentage, list-query leakage and relation-model cycles. Reject undocumented inference from customer attribution or labels. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Prove the model with a real isolated OpenFGA store and define real PostgreSQL scope/query cases for the successor implementation. Record unresolved business choices as blockers, not defaults. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-013: Decide first resource permission scope.
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
