# OPS-011 — Reserve and reconcile the first durable usage meter

Task ID: OPS-011
Phase: 03-runtime-operations
Status: PRODUCTION_HONEST
Model: GPT-6.1 Sol
Dependencies: OPS-001, OPS-007
Release requirement: REQUIRED

The declared durable PostgreSQL raw-import-byte accounting scope is
`PRODUCTION_HONEST`: tenant/provider reservation admission, semantic replay,
unknown-outcome retention and fenced one-time usage release are locally qualified.
Real Customers PostgreSQL 51/51 and the exact 1045-test gate passed with zero
failures/skips and zero Release warnings/errors. Remote physical-byte accounting
and provider ambiguous-effect reconciliation are not claimed by this local state;
those remain live OPS-007/OPS-008 obligations. Live Hugging Face upload, download, delete, conditional write, redirects/timeouts, finite capacity, asymmetric provider/database failure and remote reconciliation remain unrun and NOT qualified. Checked-in `ObjectStorage.Enabled=false`; no nonempty relevant provider runtime configuration variables were visible. No private credential, substitute provider or fallback byte archive was introduced.

Current evidence/guards are owned by
[CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md](../../implementation/CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md).
No repository integration or production acceptance is inferred.

Cross-track prerequisites: OPS-007

## Outcome

Enforce one actual hard resource limit using durable reservation and usage facts, including concurrent admission and reconciliation after uncertain completion.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [RESOURCE_CONSUMPTION_AND_LIMITS](../../requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md), [PERSISTENCE_SELECTION](../../data/PERSISTENCE_SELECTION.md), [API_CONTRACT_IDEMPOTENCY_AND_RETRY](../../api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: first consuming capability/provider, its meter/policy schema and tests, and relevant API/Worker admission. Exclude generic billing plans, pricing subscriptions, every meter and metrics as authoritative accounting. Keep neutral `Application.*` identities and product version `v0.0.1`. No empty future projects or unrelated changes.

## Decisions/prerequisites

First concrete candidate is retained object bytes under OPS-007. Define reservation, actual-byte commit, orphan reconciliation and release from that consumer; meter names/limits still require owner acceptance. Use a different resource only after updating exact producer dependencies.

Choose one real meter such as retained-object bytes or provider-send allowance. Accept units/window, policy revision, critical headroom, reserve/commit/release and OutcomeUnknown rules. Identify whether enforcement is tenant or provider-account scope.

The first meter is retained raw import bytes, scoped by tenant and configured provider
bucket. PostgreSQL owns the maximum allowance, reserved bytes, retained bytes and
reservation state. Reservation admission locks the tenant/provider usage row; retries
reuse the import idempotency key and immutable content-addressed object key. Known
absence releases reserved bytes; timeout/ambiguous effects retain an orphan/unknown
reservation for reconciliation. The seven-day retirement path decrements retained
bytes only after the database claim confirms no active/unknown reservation and the
provider delete returns success or not-found.

## Acceptance and edge cases

- Concurrent reservations cannot oversubscribe the accepted hard limit.
- Idempotent retries neither double reserve nor double count completed consumption.
- Cancel/failure releases only provably unused reservation; ambiguous effects remain reconcilable.
- Policy lowered below current usage preserves business state and blocks/degrades only new consumption as declared.
- Counter drift is detected/repaired from retained authoritative facts without erasing usage.
- Window/timezone/reset changes and duplicate reset work cannot grant double allowance.

## Security/static review

Review atomic SQL/constraints, authorized policy ownership, unit overflow/negative values and tenant/provider separation. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

The final real Customers PostgreSQL suite passed **51/51** with reservation replay,
capacity-owned metadata and expired-retirement coverage. The full external-provider
ambiguous-effect reconciliation run remains part of the OPS-007/OPS-008 live provider
blocker; it is not replaced by a mock or telemetry assertion. Requalify on schema,
reservation-state, provider-scope, limit or retention changes.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-011: Reserve and reconcile the first durable usage meter.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Implement one durable meter with exact reservation and reconciliation semantics.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
