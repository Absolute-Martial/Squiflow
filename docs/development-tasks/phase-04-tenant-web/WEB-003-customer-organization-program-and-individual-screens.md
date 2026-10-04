# Customer organization, program and individual screens

Task ID: WEB-003
Phase: 04-tenant-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6 Luna (high)
Dependencies: WEB-002, COM-003
Release requirement: REQUIRED

## Outcome

Expose the qualified Customers contracts as small operator browse/create/detail and supported availability screens, distinguishing organization, child program and individual billing identity.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/CUSTOMER_ORGANIZATION_PROGRAM_ATTRIBUTION_SLICE.md`; `docs/implementation/CUSTOMER_INDIVIDUAL_BILLING_RECORD_SLICE.md`; `docs/domain/BUSINESS_TERMS.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write customer routes/components under apps/web and their browser checks. Update the focused Web journey owner only; no Customers domain, migration or provider change. Contacts/import/editing appear only if separately qualified.

## Decisions/prerequisites

Backend prerequisites: qualified customer create/read/browse/availability contracts, pagination limits, independent grants, and accepted customer identity/contact rules. Do not invent a PartyKind taxonomy, debtor assignment policy or representative login. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- Organization and program create/view grants are independent.
- Program choices are scoped to the selected organization and current tenant.
- Individual availability changes carry expected revision and display conflict safely.
- Pagination, empty states, validation errors and inaccessible IDs are understandable.
- Operator identity never silently becomes customer, representative or debtor.
- Duplicate submission and changed-intent retries follow backend receipt semantics.
- Changing tenants or revoking view authority removes stale contact/detail state.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review DTO field allow-lists, contact PII, route/resource substitution, encoded labels and accessibility. No contact records enter localStorage, diagnostic payloads or unauthorized summaries.

## Dynamic verification and unavailable-environment handling

Use Playwright with real CoreApi/PostgreSQL/OpenFGA fixtures for independent grants, cross-tenant IDs and stale availability revisions. Retain deterministic browser checks without mocking those authority properties. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.0.1. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Read the exact Customers API contracts.
Build bounded organization/program/individual routes.
Use independent grants only for presentation hints.
Send only accepted DTO fields and current revisions.
Keep billing identity distinct from authenticated operator.
Render validation and empty states accessibly.
Test retries, revocation and cross-tenant identifiers.
Do not introduce new customer lifecycle or contacts semantics.
Inspect real browser/provider results.
Return source ZIP and the regression mapping.
```
