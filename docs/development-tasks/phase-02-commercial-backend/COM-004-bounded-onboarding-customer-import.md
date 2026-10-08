# COM-004 — Bounded onboarding customer import

Task ID: COM-004
Phase: 02-commercial-backend
Status: VERIFY_EXISTING
Runtime state: BLOCKED
Model: GPT-6.1 Sol
Dependencies: COM-003
Conditional dependencies: OPS-008 when import bytes are retained; OPS-003 when the accepted import uses background execution
Release requirement: REQUIRED

`Status` above is the scheduling value defined by
[`ORCHESTRATOR.md`](../ORCHESTRATOR.md); runtime gate states are recorded on
their own line and are owned by the focused owners. The task-level runtime state
is **BLOCKED**: it declares the conditional dependency OPS-008, whose own runtime
state is BLOCKED pending live Hugging Face provider evidence. Its local
capability, CoreApi and PostgreSQL scope below is separately and locally
qualified; that narrower state must not be read as task-level acceptance.

COM-004 implementation and local qualification are **COMPLETE** for the declared
capability, CoreApi and PostgreSQL contracts. Raw-source lifecycle, expiry-aware
reads, fenced retirement/recovery and hosted source-only tenant execution are
locally `PRODUCTION_HONEST`. Production/provider qualification remains `BLOCKED`
only on the live OPS-007/OPS-008 Hugging Face evidence; this is not an unresolved
local retention or recovery implementation claim.

The fresh exact `./eng/verify.sh` completed with **exit 0**, **1045 passed / 0 failed /
0 skipped** across **20 test projects**, and a Release build with **0 warnings /
0 errors**. The final gate includes CoreApi **458/458** and Customers PostgreSQL
**51/51**. Standalone pre-upload-fix suites passed 457/457 and 51/51; they are
supporting earlier evidence, not substitutes for the final gate. Post-fix standalone
formatter verification and `git diff --check` passed. Complete local results, source
hashes, original failures and the safety review are retained under
`artifacts/verification/com004-current-20261007/` (`RESULTS.json`, `gate.log`,
`gate-source.json`, `STATIC-REVIEW.md`).

Live Hugging Face upload, download, delete, conditional write, redirects/timeouts, finite capacity, asymmetric provider/database failure and remote reconciliation remain unrun and NOT qualified. Checked-in `ObjectStorage.Enabled=false`; no nonempty relevant provider runtime configuration variables were visible. No private credential, substitute provider or fallback byte archive was introduced.

This is local dirty-tree qualification, not commit/merge/PR integration, remote CI,
coverage, live identity-provider qualification, deployment readiness or production
acceptance. This pass also changed the gate's execution policy: `eng/verify.sh` now
classifies test projects by their own Testcontainers dependency and runs the unit
group and then the container group, one `dotnet test` per project, where the
preceding script issued a single solution-wide `dotnet test`. No test case is
serialized within a project. Historical gate totals are preserved below only as
historical evidence.

Implementation/local qualification complete; only the external provider qualification remains to be received. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Import the exact agreed customer onboarding format with useful row errors, tenant-safe duplicate handling and resumable semantic results when a real onboarding workload requires it.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md](../../implementation/CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md), [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: one bounded customer format and supported organization/program/contact fields; validation, preview and authorized import receipts. Exclude a generic ETL platform, arbitrary mappings, supplier/financial imports and automatic identity-provider account creation.

## Decisions/prerequisites

Owner activation/format/limits/duplicates/atomicity/retention/execution decisions were closed on 2026-10-06. Ship the canonical `customer-import/v1` template; a third-party input sample is no longer an activation prerequisite. The Customers-owned durable row ledger, fencing and awaited hosted execution qualify only their explicit bounded workload, not the general OPS Worker runtime. Raw-source retention is implemented through OPS-007/OPS-008/OPS-011, and its local contracts are qualified. Production/provider qualification remains `BLOCKED` until the real private Hugging Face provider contract, asymmetric failures and capacity/retention evidence are qualified. Current scope/evidence: [CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md](../../implementation/CUSTOMER_DUPLICATES_AND_IMPORTS_SLICE.md).

Omission from full completion requires an explicit owner disposition, safe absence behavior and an activation trigger.

## Acceptance and edge cases

- Preview performs no durable customer mutations.
- Malformed encoding, oversized input and unsupported fields return bounded errors.
- Program rows resolve only an organization in the same tenant.
- Duplicate resolution follows COM-003 rather than merging by name.
- Retry or interrupted batches preserve row identities and reveal committed versus rejected rows.
- Source personal data is excluded from logs and unauthorized downloads.

## Security/static review

Review parser/formula injection, retained import-file access and all row-level tenant checks. File retention/upload uses the selected OPS-008 boundary; live provider qualification remains required.

## Dynamic verification and unavailable-environment handling

Use representative good/bad samples and actual PostgreSQL interrupted/retry tests. Report synchronous limits or the exact durable-job crash checks required if background import is selected. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-004: Bounded onboarding customer import only.
Read current owners and applicable instructions.
Inspect existing callers and tests; preserve incoming work.
Close listed decisions before dependent contracts.
Implement the smallest complete scope and focused tests.
Review security, authority, durability and concurrency.
Update focused behavior/decision documentation.
Run available checks; name exact unrun checks.
Deliver source-only ZIP and evidence handoff.
No commit/push or unrun qualification claims.
```
