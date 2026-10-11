# PR #5 recovery and consolidated-pricing decision proposal

Status: owner accepted both proposed contracts on 2026-10-11.

This closes the two business choices needed by the remaining review work. Existing quotation, program, approval, fulfillment, purchasing and invoice contracts remain owned by their accepted documents.

## Proposed import-source recovery contract

- Reservations and retries are scoped to the tenant, operator account and idempotency key. A matching retry recovers the original import identity and source key; changed content or retention is rejected.
- Provider upload ownership uses a durable expiring lease and generation. A stale writer cannot complete or change a successor's source, receipt or accounting.
- Source availability, retained-byte accounting, validated import rows, the source link and the command receipt commit in one PostgreSQL transaction.
- Until an import is accepted durably, its uploaded source is recoverable staging with a seven-day lifetime, including requests that elect archival retention. A completed archival import retains its existing indefinite lifetime. Completed default imports retain their existing seven-day lifetime.
- Hosted maintenance discovers expired staging and uncertain uploads even when no import work was accepted. It uses bounded provider calls and fenced durable cleanup leases.
- Cleanup atomically replaces customer bytes with, conditionally creates, or verifies a permanent zero-byte marker at the retired source key using conditional writes against the observed object version. A delayed cleanup cannot remove a newer marker. This prevents a late conditional upload from recreating the deleted customer bytes. Markers contain no CSV or customer details; they use zero retained CSV bytes.
- Bytes are released exactly once, in the same transaction that records verified retirement and terminal reservation state. Provider errors or uncertainty keep accounting intact and remain retryable after restart. No timeout or missing-object observation alone releases an uncertain reservation.
- Existing ambiguous historical reservations are not assigned to an arbitrary operator. They are retired through the same private recovery path; attributable accepted receipts remain readable.
- No live-provider qualification is implied. Object storage remains disabled by default and OPS-007/OPS-008 live evidence remains required.

## Proposed consolidated-customer pricing contract

Published prices retain their original customer identity and history. Consolidation does not automatically transfer, combine, reprice or rewrite them. New priced work resolves the canonical surviving customer and uses that survivor's applicable prices. A source customer's older price revisions remain viewer-readable as historical facts. Explicit retirement and any replacement price continue to require the independent Pricing permissions.

This avoids silently copying a commercial agreement to another customer. Source-price carryover would require a later explicit selection/conflict rule.

## Monitoring and automatic recovery

Owner selected separate responsibilities within the existing CoreApi host on 2026-10-11.
Monitoring reports outcomes/metrics and holds no business mutation authority. The recovery
workflow alone owns lease, cleanup, source-state and byte-accounting changes. Business
execution and recovery have independent failure/deadline scopes; diagnostic listener errors
cannot prevent recovery. No separately deployed services are selected.

## Compatibility

The existing v1 Pricing response field names and PascalCase values are preserved through explicit mappings. The new Order commercial-facts DTO keeps its independently owned wire contract. Neither requires a compatibility break for these fixes.
