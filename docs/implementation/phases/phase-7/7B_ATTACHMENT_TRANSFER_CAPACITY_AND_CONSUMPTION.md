# Phase 7B — Attachment Transfer, Capacity, and Consumption

## Separate semantic sync from byte transfer

Large files/artwork must not inflate the semantic sync protocol into an unbounded blob channel.

Use explicit staging/transfer state with bounded concurrency, retry and integrity verification.

This transfer foundation may be pulled forward if an earlier real capability requires durable attachments; the phase number does not justify temporary unbounded upload logic.

## Capacity

Treat the current provider/account envelope as finite. Measure retained bytes, staging bytes, growth rate and provider limits.

Where storage usage becomes an enforced/cost/capacity concern, define the first authoritative consumption meter:

- what counts as retained bytes;
- when consumption is committed;
- whether retries count once;
- delete/retire/correction behavior;
- tenant/platform/provider scope;
- hard/advisory limit behavior;
- reconciliation against provider state.

Telemetry is not the authoritative usage ledger.

## Failure tests

Upload succeeds/metadata fails; metadata exists/object missing; interrupted upload; duplicate retry; two uploads race for final allowed capacity; low local disk; provider timeout/capacity exhaustion.

## Exit gate

Transfer is resumable/reconcilable/bounded and storage-limit decisions cannot silently delete retained business objects.