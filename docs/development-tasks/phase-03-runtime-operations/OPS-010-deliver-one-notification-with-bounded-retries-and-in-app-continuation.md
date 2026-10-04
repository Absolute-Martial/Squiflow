# OPS-010 — Deliver one notification with bounded retries and in-app continuation

Task ID: OPS-010
Phase: 03-runtime-operations
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: OPS-003, OPS-004
Release requirement: CONDITIONAL
Cross-track prerequisites: none

## Outcome

Add one earned external notification channel while preserving an authoritative in-app continuation when delivery is delayed, rejected or ambiguous.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [NOTIFICATIONS_AND_EXTERNAL_DELIVERY](../../integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md), [CORE_API_AND_WORKER](../../server/CORE_API_AND_WORKER.md), [RESOURCE_CONSUMPTION_AND_LIMITS](../../requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: one notification capability/provider adapter, durable consequence/attempt state, Worker handler and status/read contract tests. Exclude every provider/channel, webhook platform and notification-as-business-authority behavior. Keep neutral `Application.*` identities and product version `v0.1.0`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Bind the first notification to OPS-001's accepted retained-work completion/failure fact and its in-product status path. A selected approval notification is an additional bounded consumer, not a prerequisite for creating this channel. Select the provider/channel explicitly; update exact producer dependencies before adding another workload.

Accept one provider, consent/preference rules, destination validation, content/template limits and cost budget. In-app required business action stays usable independently. If no external channel is needed, record that decision; do not remove requested scope silently.

## Acceptance and edge cases

- Originating transaction remains committed despite failed or exhausted delivery.
- Duplicate producer/Worker attempts preserve one semantic notification identity.
- Provider idempotency/reference supports reconcile-after-send; lost responses enter OutcomeUnknown.
- One retry owner classifies permanent/transient failure and caps attempts/time with backoff.
- Malformed provider responses, destination injection and unavailable secrets fail safely.
- Visible in-app status explains pending/deferred/failed delivery without exposing destinations or bodies broadly.

## Security/static review

Review consent, channel encoding, outbound URL/SSRF where applicable, secret rotation and provider SDK license; keep sensitive message content out of diagnostics. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Use provider sandbox and failure proxy tests for accepted send, response loss, timeout, quota and permanent rejection; prove in-app continuation remains available. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-010: Deliver one notification with bounded retries and in-app continuation.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Implement one selected notification provider and ambiguous-outcome reconciliation only.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
