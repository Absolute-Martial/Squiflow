# Phase 9 — Payments, Credit, Inventory, and Protected Business Authority

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Purpose

Phase 9 applies the already-proven durability/idempotency/reconciliation architecture to the highest-risk shared business facts and external financial effects.

Inventory, pricing, payment-domain concepts, or related UI may exist earlier when real product work needs them. Phase 9 is where the protected current-authority/concurrency/external-effect behavior is qualified, not necessarily where those module folders first appear.

## Subphases

```text
9A  Payments and provider-effect idempotency
9B  OutcomeUnknown, refunds, reversals and reconciliation
9C  Inventory concurrency and correction
9D  Credit/pricing authority and historical financial truth
9E  Integrated Phase-9 production-honesty gate
```

Other capabilities continue evolving; Phase 9 raises the maturity of protected financial/stock behavior rather than limiting development to those modules.

## Phase-specific evidence and regression map

- **9A — payments/provider effects:** semantic idempotency/state-machine tests run `PER_MR`; real/sandbox provider integration covers provider idempotency/reference behavior, timeout, duplicate request and response loss. Provider behavior is `SCHEDULED`/`PRE_RELEASE` where external execution cannot be continuous.
- **9B — OutcomeUnknown/refunds/reconciliation:** `PER_MR` tests permanently cover ambiguous outcome state, duplicate refund/webhook, retry after uncertainty, correction/reversal history and reconciliation transitions. Fault injection verifies no guessed success/failure after an ambiguous external effect.
- **9C — inventory:** real authoritative DB concurrency tests cover last-unit races, expected version/constraint behavior, correction/adjustment history and stale projection/local state rejection. Protected stock concurrency runs `PER_MR` against the real persistence mechanism where practical.
- **9D — credit/pricing/history:** tests cover current central credit authority, stale offline denial/defer behavior, Owner/manual override authorization/audit, historical issued values, supported currency/rounding/time semantics, and configuration/rule changes not rewriting history.
- **9E — integration:** provider happy path alone never qualifies protected financial authority; ambiguity/concurrency/reconciliation evidence and permanent guards are required.

## Transitional contract

Payment/inventory/credit/pricing domain models and UI may appear earlier, but protected mutations remain disabled/server-required until the relevant Phase-9 authority semantics are production-honest.

Examples:

```text
payment UI without qualified provider-effect semantics
→ may collect/display draft intent
→ may not claim charge/refund success

inventory local snapshot without qualified central mutation
→ may support UX/read context
→ may not become final shared-stock authority

credit snapshot offline
→ may inform UX
→ may not approve a protected credit effect when current authority is required
```

## No error budget for protected truth

There is no operational error budget for SquiFlow-caused semantic double charge, unauthorized refund, cross-tenant financial effect, protected stock invariant violation, or historical financial corruption. Provider availability/latency and reconciliation completion time may receive measured operational SLOs, but correctness remains absolute within the declared contract.

## Completion meaning

Passing Phase 9 qualifies only the protected business behavior actually implemented and promised. New providers, inventory modes or pricing models introduced later must satisfy the same authority/idempotency/concurrency/history rules and add their own regression evidence.