# COM-004 — Bounded onboarding customer import

Task ID: COM-004
Phase: 02-commercial-backend
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: COM-003
Conditional dependencies: OPS-008 when import bytes are retained; OPS-003 when the accepted import uses background execution
Release requirement: CONDITIONAL

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Import the exact agreed customer onboarding format with useful row errors, tenant-safe duplicate handling and resumable semantic results when a real onboarding workload requires it.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md](../../implementation/CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md), [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: one bounded customer format and supported organization/program/contact fields; validation, preview and authorized import receipts. Exclude a generic ETL platform, arbitrary mappings, supplier/financial imports and automatic identity-provider account creation.

## Decisions/prerequisites

Activation requires a real input sample, encoding/column/version contract, row/byte bounds, duplicate policy and batch atomicity decision. Large durable jobs additionally require OPS-001 and OPS-002; do not force background processing for a small bounded synchronous import.

Omission from full completion requires an explicit owner disposition, safe absence behavior and an activation trigger.

## Acceptance and edge cases

- Preview performs no durable customer mutations.
- Malformed encoding, oversized input and unsupported fields return bounded errors.
- Program rows resolve only an organization in the same tenant.
- Duplicate resolution follows COM-003 rather than merging by name.
- Retry or interrupted batches preserve row identities and reveal committed versus rejected rows.
- Source personal data is excluded from logs and unauthorized downloads.

## Security/static review

Review parser/formula injection, retained import-file access and all row-level tenant checks. File retention/upload requires OPS-008 when bytes are retained.

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
