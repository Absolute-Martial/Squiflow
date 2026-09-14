# Phase 2A — First Real Local-Capable Business Slice

## Required slice

Use a real Customer/Order journey, not fake demo persistence. Define the exact local-capable operation and its authority:

```text
user intent
→ host-neutral capability rules
→ local application use case
→ durable local commit
→ immediate local result
```

The result must be named honestly: local/provisional/pending, not globally authoritative.

## Domain requirements

For the selected slice close only the business semantics it needs, such as Customer identity, walk-in handling, Order identity/lines, quantity/currency basics, and allowed local state transitions.

Do not pull payment/stock/credit current-authority rules into local-final execution prematurely.

## Continuing work

Other modules, Web/API work, identity/authorization work, observability, deployment, and Guard can continue in parallel.

## UX

The Workstation must distinguish states such as Draft, LocalCommitted, PendingRemote, Conflict/NeedsReview, and Authoritative when those states exist.

## Exit gate

The first local-capable operation has real business meaning and does not depend on network availability to become durably local.