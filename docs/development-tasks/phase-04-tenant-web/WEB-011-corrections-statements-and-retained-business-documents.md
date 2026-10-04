# Corrections, statements and retained business documents

Task ID: WEB-011
Phase: 04-tenant-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: WEB-010, COM-031, OPS-007, OPS-009, OPS-013
Conditional dependencies: OPS-008 when managed attachments/uploads are selected
Release requirement: REQUIRED

## Outcome

Present qualified post-effect corrections and retained documents/statements so operators can explain what changed without silently editing committed commercial facts.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/BUSINESS_OPERATION_END_TO_END.md`; `docs/data/FILES_AND_OBJECT_STORAGE.md`; `docs/security/APPLICATION_SECURITY_BASELINE.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write bounded apps/web correction/history/statement/document screens and tests. Physical printing, Workstation devices, arbitrary HTML templates, object-store credentials and backend correction/export engines are outside scope.

## Decisions/prerequisites

Backend prerequisites: selected correction scope and qualified cancel/credit/return/refund/adjustment operations where required, immutable linkage/history, statement projections, retained invoice/receipt exports and file authorization. Unneeded return/refund variants remain conditional exclusions. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- Before-effect cancellation and after-effect corrective records are clearly distinguished.
- Partial effects show exactly which fulfillment/stock/financial fact is corrected.
- Credit, returned stock and refund never imply all other reversal effects occurred.
- Statements explain current outstanding and unapplied amounts from server projections.
- Documents retain historical debtor/prices after later policy or identity changes.
- Download/preview requires current resource authority and safe content disposition.
- Export/print failure never undoes a committed invoice, payment or correction.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review untrusted filenames, MIME/content previews, rich content, URLs and download exposure. No raw HTML, active document content or broad object-store signed access is accepted without its qualified controls.

## Dynamic verification and unavailable-environment handling

Run Playwright plus real retained-file/financial APIs for revoked download access, historical facts, partial correction and export failure. Preserve document-authorization and correction-history regression guards. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.1.0. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Select only qualified correction effects and document formats.
Use retained server history and statement projections.
Explain original fact, corrective record and remaining state.
Submit explicit authorized correction intent.
Keep refund, credit, stock and return effects distinct.
Render and download files through accepted safe controls.
Test revocation, historical retention and export failure.
Never silently rewrite issued facts.
Leave physical printing and unsupported variants deferred.
Deliver source ZIP and correction/document evidence.
```
