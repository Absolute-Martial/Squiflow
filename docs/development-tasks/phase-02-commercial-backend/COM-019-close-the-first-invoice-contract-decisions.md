# COM-019 — Close the first invoice contract decisions

Task ID: COM-019
Phase: 02-commercial-backend
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: COM-001
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Complete the current invoice contract decisions and examples before durable schema or APIs harden the four remaining contract choices.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [INVOICE_ISSUE_CONTRACT.md](../../implementation/INVOICE_ISSUE_CONTRACT.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: focused invoice owner and accepted/open decision updates only; define contract examples and scope. Preserve accepted debtor choices, currency, arithmetic and incoming host-neutral implementation. Exclude runtime issuance, tax, accounting compliance and a blanket universal ledger.

## Decisions/prerequisites

Already accepted: NPR displayed as रु; decimal(19,4), line ToEven to four decimals then sum; organization default, independent program or explicit individual debtor under separate billing authority; freeze per invoice. Close four blockers: full versus partial/multiple allocation; organization reference anchor (including individual/unattributed Orders), reference/sequence and gap behavior; business-date authority/bounds; inactive-individual eligibility. Printable/fiscal syntax can remain open.

## Acceptance and edge cases

- Tenant-wide unique invoice identity differs from organization-scoped reference/sequence.
- Exact printable format stays open until explicitly selected; no fiscal numbering claim is invented.
- Per-invoice debtor choice never changes future program defaults.
- Program attribution and operator login cannot implicitly select the debtor.
- Committed source revision and NPR are required; draft, abandoned and non-NPR Orders are ineligible.
- No initial tax is explicit scope, not tax exemption or legal compliance.

## Security/static review

Review billing-selection authority, cross-tenant/program relationships and financial fact disclosure. State retention/privacy decisions that remain genuinely unresolved.

## Dynamic verification and unavailable-environment handling

Provide concrete organization, independent program, individual, partial invoice and duplicate issue examples. Validate midpoint/overflow examples against the existing arithmetic contract; document decisions without claiming runtime tests. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Execute COM-019 as a contract-decision assignment only.
Read current owners and applicable instructions.
Inspect existing callers and tests; preserve incoming work.
Close listed decisions before dependent contracts.
Return the four proposed choices and examples for owner acceptance; do not choose policy silently.
Review security, authority, durability and concurrency.
Update focused behavior/decision documentation.
Run available checks; name exact unrun checks.
Deliver source-only ZIP and evidence handoff.
No commit/push or unrun qualification claims.
```
