# Phase 4B — Conflict, Reconciliation, and Adjustment

## Principle

Conflict semantics belong to the owning aggregate/capability. There is no universal last-write-wins policy.

The shared sync/recovery foundation may provide version/evidence/operation primitives, but it must not centralize all business conflict meaning into one generic merger.

## Required result vocabulary

Use only what real capabilities need, but distinguish meaningful outcomes such as:

```text
Authoritative
Adjusted
AlreadyApplied
Conflict
Rejected
Retryable
AuthorizationChanged
UpgradeRequired
OutcomeUnknown where an external effect can be ambiguous
```

## Capability rules

A Customer profile edit may have different merge/review rules from an Order transition, Inventory adjustment, Payment or Quotation revision. The sync framework supplies operation identity/version evidence, not one generic business merger.

## User experience

The Workstation must present understandable actions such as `Needs review`, `Permission changed`, `Updated on server`, or `Upgrade required`, not infrastructure jargon.

## Evidence

Preserve enough local/server version, operation, causation and decision evidence to explain why a pending operation was applied, adjusted, rejected or conflicted.

## Tests

Concurrent valid edits, remote delete/merge, stale expected version, changed current authority, duplicate old operation and repeated conflict-resolution submission.

## Exit gate

At least one real aggregate has a complete conflict/review flow that preserves history and cannot silently overwrite a protected authoritative fact.