# Phase 9A — Payments and Provider-Effect Idempotency

## State model

Represent payment lifecycle with enough states to avoid boolean fiction, for example:

```text
NotStarted
Pending
Succeeded
Failed
OutcomeUnknown
PartiallyRefunded
Refunded
Reversed
```

Payment-domain concepts, UI, contracts or provider POCs may exist earlier when real work requires them. This subphase is the maturity gate for authoritative provider effects and payment-state correctness.

## Authority

Payment operations requiring provider/current central authority are server-required; local-first Workstation capture does not mean offline-final charging.

## Idempotency

Separate SquiFlow semantic payment identity from transport retries and provider attempt/reference identity. A retry must not create a second semantic financial effect.

## Historical truth

Persist amount/currency/provider/reference/applied context required to explain the financial record later.

## Failure tests

Provider succeeds/response lost, client retries, server crashes before/after provider call, provider timeout, duplicate callback/webhook, delayed callback after local/server retry.

## Exit gate

The system cannot silently convert an uncertain provider outcome into a guessed success/failure or duplicate charge.