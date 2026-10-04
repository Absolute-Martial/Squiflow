# ADM-003 — Review route access readiness and admission gaps

Task ID: ADM-003
Phase: 01-admin-platform
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: ADM-002
Release requirement: CONDITIONAL
Cross-track prerequisites: none

## Outcome

Create an exact route-to-permission/security inventory and fix only verified omissions or unbounded readiness/admission behavior; otherwise return evidence that no change is needed.

## Current basis and canonical inputs

Program already classifies routes; PlatformAdminRequestAuthorizer checks HTTPS, exact identity, active certificate and pinned OpenFGA permission. AdminApiAdmission already uses queue-free concurrency limits. Readiness currently queries PostgreSQL and OpenFGA directly. Read [README.IMPLEMENTATION.md](../../../README.IMPLEMENTATION.md) and [Program.cs](../../../services/admin-api/Application.AdminApi/Program.cs); [PlatformAdminRequestAuthorizer.cs](../../../services/admin-api/Application.AdminApi/PlatformAdminRequestAuthorizer.cs); [AdminApiAdmission.cs](../../../services/admin-api/Application.AdminApi/Composition/AdminApiAdmission.cs).

This conditional task was activated by one observed receiving-baseline gap: [current-head evidence](../../production-completion-review/evidence/ADM-002-ADM-003.md) retains the independent pre-fix reproduction of public liveness invoking native JWT validation when given a bearer token (exit 1, 0 passed / 1 failed / 0 skipped). The correction is present in commit `6735370` in both hosts' native authentication registration and focused regressions: trusted explicitly public endpoint metadata with no authorization metadata returns `NoResult()` before bearer validation/discovery, while protected routes retain normal authentication. Current-head AdminApi 87/87 and Tenancy.Postgres 19/19 support the implementation. The required post-fix ADM-003 review receipt is retained at [ADM-003-INDEPENDENT-REVIEW-RECEIPT.md](../../production-completion-review/evidence/ADM-003-INDEPENDENT-REVIEW-RECEIPT.md) with an explicit `ACCEPTED` disposition for the declared narrow scope, produced under the bounded [independent review assignment](../../production-completion-review/assignments/ADM-003-INDEPENDENT-REVIEW.md). That receipt discloses its reviewer was not independent of other work in this repository, so organizational-independence confirmation remains a gate-owner decision; the technical evidence is retained and re-verified on the current source. This task does not authorize speculative readiness/admission redesign or future feature work.

## Scope and exclusions

Allowed areas: AdminApi route registration, readiness/admission configuration, shared request authorizer and corresponding boundary tests only. Exclude UI implementation, unrelated capabilities and purged Parties code.

For the activated public-auth slice only, the assigned host-native JWT registration files in AdminApi/CoreApi and corresponding public/protected pipeline tests are also allowed, exactly as listed in its bounded assignment. No new dependency, issuer/audience relaxation or generic authentication framework is earned.

## Decisions/prerequisites

Measure existing readiness provider budgets before proposing cache/single-flight changes. Use existing ASP.NET mechanisms and keep public status-only probes independent of admin identity. Follow [shared rules](../AGENT_RULES.md) and [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md).

## Acceptance and edge cases

- A new protected route cannot omit required classification or permission.
- Missing/wrong principal-bound device denies independently of token validity.
- Private-address or spoofed header never grants access.
- Full concurrency cap rejects before protected work and retains no-store.
- Cancelled/failed requests release permits.
- Readiness dependency failure returns bounded status without provider details.
- Bearer-bearing public health does not enter JWT validation/discovery; protected routes still enforce their complete authentication/current-authority path.

## Security/static review

Check all HTTP methods including constraint mismatch and unknown operations; verify no alternate public/admin authority path or readiness secret disclosure. Report unresolved material findings as blockers.

## Dynamic verification and unavailable-environment handling

Use AdminApiBoundaryTests plus deterministic saturation/cancellation checks. Exercise the actual health routes against dependency outages; run ./eng/verify.sh if a fix is made. Run ./eng/verify.sh for implementation changes. Without SDK/Docker/provider access, complete static review/test implementation and list exact unrun checks; mocks cannot qualify real boundaries.

## Handoff

Return a source-only ZIP, changed files, inspected evidence and blockers. Name regression guards and requalification triggers. No commit/push or product-version change.

## Assignable prompt

```text
Execute ADM-003: Review route access readiness and admission gaps.
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
