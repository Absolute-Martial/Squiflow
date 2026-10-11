# COM-019 — Close the first invoice contract decisions

Task ID: COM-019
Phase: 02-commercial-backend
Status: VERIFY_EXISTING
Model: GPT-6.1 Sol
Dependencies: COM-001
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Record the owner-accepted first invoice contract, examples, and implementation handoff before durable schema or APIs harden its semantics. Decision work was completed on 2026-10-10; the existing host-neutral implementation remains `BLOCKED` receiving qualification until aligned to the accepted contract. Durable invoice runtime remains `NOT_INTRODUCED`.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [INVOICE_ISSUE_CONTRACT.md](../../implementation/INVOICE_ISSUE_CONTRACT.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: focused invoice owner and accepted/open decision updates only; define contract examples and scope. Preserve accepted debtor choices, currency, arithmetic and incoming host-neutral implementation. Exclude runtime issuance, tax, accounting compliance and a blanket universal ledger.

## Decisions/prerequisites

Owner accepted on 2026-10-10: one complete invoice per committed Order; invoiceable Orders require frozen organization attribution, which anchors an increasing per-organization numeric ordinal even for program or individual debtors; retain that ordinal with a tenant-wide invoice GUID and allow gaps; derive business date from the UTC calendar date of the server issue instant without caller dating; permit only active individual bill-to records. No printable syntax, gapless numbering, tax/fiscal rule, or compliance claim is accepted. Preserve earlier accepted NPR, decimal(19,4), per-line ToEven rounding then sum, organization-default/program-or-individual debtor choice under separate billing authority, and issue-time fact freeze. The current host-neutral code supports a partial-invoice donor shape and `ContractDecisionRequired` for inactive individuals, so it remains `BLOCKED` receiving qualification pending deltas. The business authority remains a distinct check through the existing `IInvoiceIssueAuthority` port; COM-022 owns live CoreApi/OpenFGA relation design and qualification. COM-021 adds PostgreSQL issuance only, not an endpoint, receivable, payment, or document effect.

## Acceptance and edge cases

- A tenant-wide invoice GUID differs from the increasing numeric ordinal scoped to the Order's frozen organization; gaps are allowed.
- An unattributed Order is ineligible; the same ordinal anchor applies when the selected debtor is its program or an individual.
- Exactly one full invoice may issue from a committed Order; a second issue and partial invoicing are rejected.
- Business date is the UTC calendar date of server `issuedAt`; no caller date, tenant timezone, backdating, or future-dating is used.
- An inactive individual cannot be selected for a new issue; previously issued snapshots do not change.
- Exact printable format stays open; no gapless, fiscal-numbering, or legal-compliance claim is invented.
- Per-invoice debtor choice never changes future program defaults.
- Program attribution and operator login cannot implicitly select the debtor.
- Committed source revision, frozen organization attribution and NPR are required; draft, abandoned, unattributed and non-NPR Orders are ineligible.
- No initial tax is explicit scope, not tax exemption or legal compliance.

## Security/static review

Review billing-selection authority, cross-tenant/program relationships and financial fact disclosure. State retention/privacy decisions that remain genuinely unresolved.

## Dynamic verification and unavailable-environment handling

Record examples: an attributed organization Order defaults to that organization and can issue once in full; an attributed program or active individual may be selected while the same organization scopes the ordinal; an inactive individual and an unattributed Order are rejected; a second invoice for the same Order is rejected; exact same-key retry returns the original GUID, ordinal, UTC date and frozen facts. Validate midpoint/overflow examples against the existing arithmetic contract when executing implementation work; this decision handoff claims no new test/build/runtime qualification. COM-020 must implement and qualify the accepted host-neutral deltas; COM-021 must prove actual PostgreSQL atomicity, numbering and recovery; COM-022 later qualifies live API authority.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Execute COM-019 as a contract-decision assignment only.
Read current owners and applicable instructions.
Inspect existing callers and tests; preserve incoming work.
Close listed decisions before dependent contracts.
Apply the owner-accepted 2026-10-10 choices in the focused invoice owner and decision records; preserve the host-neutral and durable-runtime qualification boundaries.
Review security, authority, durability and concurrency.
Update focused behavior/decision documentation.
Run available checks; name exact unrun checks.
Deliver source-only ZIP and evidence handoff.
No commit/push or unrun qualification claims.
```
