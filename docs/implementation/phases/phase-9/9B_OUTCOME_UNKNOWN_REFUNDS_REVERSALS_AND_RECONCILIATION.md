# Phase 9B — OutcomeUnknown, Refunds, Reversals, and Reconciliation

## OutcomeUnknown

When acknowledgement is ambiguous, persist an explicit reconciliation state. Do not blindly retry an effect that may already have occurred.

This rule applies whenever an external/provider effect capable of ambiguity is first introduced. Phase 9 is the canonical payment qualification point, not permission to postpone `OutcomeUnknown` handling for an earlier real external effect.

## Reconciliation

Use provider lookup/webhook/manual controlled evidence as appropriate to resolve uncertainty while preserving the original operation identity and audit trail.

## Corrections

Posted financial truth is corrected through refund/reversal/adjustment, not destructive editing of the original payment.

Refunds/reversals need their own idempotency/provider/reference states and may themselves become `OutcomeUnknown`.

## Admin/support

Do not expose `mark payment succeeded` or generic force-complete controls. Any exceptional support action must operate through reviewed reconciliation/correction semantics and authoritative audit.

## Exit gate

Every ambiguous financial effect has a bounded reconciliation path and corrections preserve historical explainability.