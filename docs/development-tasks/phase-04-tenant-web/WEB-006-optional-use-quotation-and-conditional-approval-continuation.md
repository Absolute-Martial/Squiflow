# Optional-use quotation and conditional approval continuation

Task ID: WEB-006
Phase: 04-tenant-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: WEB-005, COM-010, COM-012
Release requirement: REQUIRED

## Outcome

Support quotation revisions/conversion as an optional operator operation within the complete product, plus human approval continuation where the selected policy requires it. Direct order entry remains usable without quotation.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/BUSINESS_OPERATION_END_TO_END.md`; `docs/workflow/WORKFLOW_DESIGN.md`; `docs/rules/NATIVE_RULE_ENGINE.md`; `docs/security/TENANT_PERMISSIONS.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write only selected quote/approval screens in apps/web and their tests. Exclude a workflow designer, generic task engine, customer portal, new lifecycle semantics and unqualified quote/approval endpoints.

## Decisions/prerequisites

Backend prerequisites: qualified quote issue/validity/accept/reject/expiry/conversion for the required quoted and direct-order cases. Approval work discovery/authority/reassignment/recovery and policy publication qualify where selected. Optional quotation use does not make quotation support an optional release requirement. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- Issued revisions retain immutable terms/prices and explicit validity.
- Conversion retries cannot create duplicate orders; stale/expired offers are explained.
- Required approval cannot be bypassed by navigating directly to commitment.
- Show current approver, missing evidence, rejected/pending and unavailable-actor recovery.
- Policy changes prompt supported revalidation without silently rewriting ongoing work.
- Unavailable notification delivery does not hide an in-product continuation.
- Direct orders remain available for cases without quotation or required approval.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review approval evidence, unauthorized disclosure and stale action hints; UI never signs approvals on behalf of another actor. Encode terms and gate downloads under current resource authority.

## Dynamic verification and unavailable-environment handling

Use real browser/provider tests for quote revision races, duplicate conversion, authority revocation and unavailable approver. Retain those cases only for selected workflows; an absent backend prevents implementation. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.1.0. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Confirm quoted/direct-order and selected approval cases.
Read qualified backend contracts and policy versions.
Build bounded operator continuation screens.
Preserve immutable issued facts and current actor authority.
Explain pending, expired, rejected and missing evidence states.
Never implement workflow rules in components.
Test conversion retry and applicable approval denial.
Verify in-product discovery when delivery fails.
Do not confuse optional quote use with optional support.
Return source ZIP and selected-case evidence.
```
