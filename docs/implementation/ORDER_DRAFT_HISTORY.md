# Retained order-draft history

**State:** `PRODUCTION_HONEST` for the declared retained-draft-history scope;
`BLOCKED = none`. Broader commercial lifecycle and general audit remain
`NOT_INTRODUCED`. Product version stays v0.1.0.

Owner: `Application.Orders`; provider: `Application.Orders.Postgres`; transport:
CoreApi. This responsibility adds a usable history of the already supported
priced draft commands to the end-to-end commercial path. It does not introduce
acceptance, quotation, fulfillment, invoicing or settlement.

## Declared scope

The existing successful create, full revision and abandonment commands commit a
priced snapshot and durable caller-scoped receipt in the same transaction.
History reads these retained receipts rather than creating a second audit table,
event store, asynchronous projection or generic logging framework. A retry returns
the same receipt and therefore does not add a historical revision. Failed or
rolled-back commands are not successful business changes and do not appear.

`GET /api/v1/tenants/{tenantId}/orders/{orderId}/history` requires the current bound
account, current tenant membership and `can_view_orders`, independently on every
request. It returns the current revision, newest-first entries with change kind
(`created`, `revised`, `abandoned`), the acting account ID, server-recorded receipt
time, retained priced snapshot and `nextBeforeRevision` when older entries exist.
The retained snapshot uses the same API representation as draft detail. It is
historical content, never current authorization or approval evidence.

The actor comes from the committed receipt's account, not the draft's original
creator or request input. The creation actor must match the snapshot's creator;
abandonment actor/time must match its retained lifecycle facts. Revision snapshots
preserve earlier supplied prices even after the current draft changes. The response
never exposes receipt keys, fingerprints, raw response JSON or credentials.

No permission relation/model changes, schema changes, new grants or migrations are
required. Runtime receipts are already insert/read-only under the restricted role.
This is attributable draft-command history, not a general audit ledger, failed
attempt log, recorded override reason or independently qualified price-change
authority. Those meanings must be added by their owning future operation.

## Bounds, consistency and failures

- `limit` defaults to 5 and accepts 1–10 because each entry includes up to 100
  priced lines. The adapter enforces the bound as well as the application operation.
- `beforeRevision` is one positive 64-bit integer and is exclusive. Use the
  previous `nextBeforeRevision`; values above current start at the newest entry.
  A value of 1 yields an empty terminal page. Repeated, malformed, overflowing or
  out-of-range query values fail with a stable 400 code.
- The query fetches at most `limit + 1` entries. It includes the current header
  revision and historical receipts in one SQL statement and therefore one
  PostgreSQL statement snapshot. Concurrent edits cannot produce an older header
  combined with newer receipts. Separate pages can observe different current
  revisions; previously committed entries remain immutable and revision paging
  excludes newly added higher revisions.
- Every query has explicit tenant/order predicates and runs inside the existing
  transaction-local RLS session. A missing order or an order in another tenant
  returns the same 404 after authorization. Revision input is paging, not authority.
- The existing `(tenant_id, order_id)` receipt index narrows the history scan;
  ordering extracts the revision from supported retained JSON shapes. A read can
  scan/sort the selected order's receipts before returning a bounded page. No
  constant-time, load-capacity or revision-index claim is made. Existing database
  command timeouts, protected-request concurrency and cooperative request budgets
  apply; a measured need can earn a migration-owned revision index later.
- Direct legacy snapshots and current version-1/version-2 envelopes use the same
  strict receipt decoder as command replay. Unsupported versions, contradictory
  actors/state, missing revisions and duplicate revisions in the checked range
  fail closed. Do not invent or silently hide a partial history. Global exception
  handling returns safe 500 responses without receipt contents.
- Authorization-provider outage fails closed with safe 503; all protected success
  and error responses use the central no-store policy. Reads create no receipts,
  mutations, durable work or fire-and-forget tasks.

History depends on the existing indefinite receipt retention and on backup/restore
keeping business effects and receipts together. Receipt deletion, compaction or a
new supported command must requalify both history and retry guarantees. Privileged
database administrators remain outside the restricted-runtime immutability claim.

## Source admission and regression ownership

This extends the admitted Npgsql/PostgreSQL adapter, parameterized SQL, native
single-statement snapshots, existing RLS and receipt compatibility decoder. It
reuses application-owned committed facts; no new infrastructure package or generic
audit mechanism is needed. Host-neutral history contracts expose no provider types.

Permanent guards under `./eng/verify.sh`:

| Claim | Falsifiable evidence |
|---|---|
| Boundary validation and context/token forwarding | `OrderDraftHistoryTests` |
| Current membership/permission, safe errors, query bounds, no-store, API shapes | `OrderDraftHistoryEndpointTests`; all-protected-route anonymous guard |
| Retained prices, different actors, paging, no retry/failed-command duplication | `HistoryRetainsPricedRevisionsAndActorsWithoutDuplicatingRetriesOrFailedCommands` |
| Cross-tenant denial | Same real PostgreSQL history test, using the restricted runtime role |
| Legacy response compatibility and unsupported-version rejection | `HistoryReadsLegacySnapshotsAndRejectsUnsupportedEnvelopesWithoutChangingBusinessState` |
| Runtime cannot rewrite/delete history facts | `RuntimeRoleCannotRewriteOrDeleteTheReceiptsUsedForHistory` |
| Incomplete history is not fabricated | `MissingHistoricalRevisionsFailClosedInsteadOfInventingAnIncompleteHistory` |
| Concurrent reads use one coherent revision/snapshot | `HistoryReadsCurrentRevisionAndReceiptsFromOneSnapshotDuringConcurrentRevision`, with an observed PostgreSQL lock barrier |
| Concurrent retries leave one historical effect | Existing concurrent create/revise tests now inspect history as well as receipt counts |

Requalify on receipt scope, retention, schema/version, new operation/state,
authorization or API changes, pagination/index changes, actor/time ownership,
provider/pooler changes, or another host consuming the history contract.

## Local qualification

On 2026-10-02, the final normal parallel `./eng/verify.sh` completed with exit 0:
locked restore, formatting verification, Release build with zero warnings/errors,
and all 413 tests in twelve test projects passed with zero failures/skips. This
includes actual PostgreSQL 17 history/RLS/concurrency tests and actual Kestrel
order-entry input tests. An earlier final-gate attempt had two Testcontainers
image-parser initialization timeouts; the unchanged-image structured-fixture
correction is recorded in `docs/testing/VERIFICATION_STRATEGY.md`. The corrected
gate was rerun normally, without serialization or test retries.

Logs were inspected locally at
`/tmp/application-commercial-final-verify-20261002.log`; the failed attempt remains
at `/tmp/application-commercial-qualified-verify-20261002.log`. These temporary
logs are not the permanent regression guard; committed source tests and the
repository verification contract own requalification. Coverage, remote CI,
deployment/load capacity, backup restoration and the whole commercial backend
are separate, unqualified claims.
