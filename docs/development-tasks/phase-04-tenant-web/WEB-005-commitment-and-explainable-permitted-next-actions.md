# Commitment and explainable permitted next actions

Task ID: WEB-005
Phase: 04-tenant-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: WEB-004, COM-013
Release requirement: REQUIRED

## Outcome

Add the qualified order commitment action and action guide without conflating a committed order with fulfilled, invoiced or paid business effects.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/implementation/ORDER_COMMITMENT_SLICE.md`; `docs/implementation/ORDER_DRAFT_ACTION_GUIDANCE.md`; `docs/implementation/BUSINESS_OPERATION_END_TO_END.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write commitment/detail/action-guidance components under apps/web and focused checks. Do not add approval, fulfillment or financial states, reinterpret receipts, or alter authoritative transition rules.

## Decisions/prerequisites

Backend prerequisites: commitment/history/action guidance qualified with separate commit authority, revisions and retry semantics. Expanded guidance may show configured stages only once compatible published policy and missing-information contracts qualify. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- Show observed revision and stable reasons why an action is unavailable.
- Guide availability grants no execution authority; submit rechecks current server rules.
- Commit uses expected revision and retained semantic operation identity.
- A commit-only actor sees only permitted transition metadata, not hidden prices.
- Concurrent edit/commit, repeated click and response loss resolve to authoritative history.
- Explain who acts next, missing information and supported unavailable-actor recovery.
- Committed is never labelled fulfilled, invoiced, settled or universally accepted.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Trace guide/detail projection versus mutation authority and protect sensitive fields. Review confirmation focus, error announcements, no-store responses and non-disclosing forbidden/not-found behavior.

## Dynamic verification and unavailable-environment handling

Run Playwright with real Orders/OpenFGA/PostgreSQL evidence for independent commit/view rights, stale guides and lost responses. The permanent guard must fail if UI assumes guidance is a capability token. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.1.0. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Read current commitment and guidance owners.
Expose only declared actions and metadata.
Render current server prohibition reasons.
Confirm material consequences before commitment.
Submit current expected revision and retry identity.
Refresh authoritative state after conflict or uncertainty.
Do not infer billing or fulfillment effects.
Test commit without view and permission revocation.
Keep browser evidence and unavailable checks separate.
Deliver source-only ZIP and owner notes.
```
