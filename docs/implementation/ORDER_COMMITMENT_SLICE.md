# Direct order commitment

Product version: v0.0.1. Focused owner: Orders. Owner decision, 2026-10-03:
commit the current priced draft and freeze its customer attribution; billing and
fulfillment remain separate later operations. This is a direct order path;
quotation is optional. This scope does not define universal tenant workflow.

## Contract and authority

`POST /api/v1/tenants/{tenantId}/orders/{orderId}/commit` accepts only
`{"expectedRevision":1}` and one normalized `Idempotency-Key`. The request body is
bounded to 1 KiB, including bodies without Content-Length. Current active account,
tenant membership and pinned OpenFGA `can_commit_order` are checked before body
processing or receipt access, including replays. That permission requires
`order_committer` independently of view, edit and manual-price authority.

A successful transition from `draft` to `committed` advances the revision by one
and records the committing account and server UTC timestamp. Identity, creator,
creation time, summary, currency, prices, lines, totals and customer/program
attribution stay unchanged. Committed orders cannot be revised or abandoned.
The response discloses only order identity, state, revision and commitment time;
viewing prices and history still requires `order_viewer`. Detail and browse include
`committed` and nullable `committedAt`. Action guidance explains commitment too;
current command authorization is always independent of that possibly stale guide.

Missing orders return tenant-safe 404; stale revisions, already terminal orders
and reused keys with a different semantic command return distinct stable 409
codes. Exact retries return the original retained result and
`Idempotency-Replayed: true`. All protected results and errors remain no-store.

## Persistence and recovery

The owning PostgreSQL adapter executes parameterized embedded SQL inside the
existing transaction-local tenant session. The conditional header update and
version-three commitment receipt commit together. Concurrent same-key requests
return one effect with retained replays; different keys cannot commit a terminal
order again. Historical create/revise/abandon receipt versions retain their old
meaning; commitment requires its own supported envelope version. History reads
check revision continuity and commitment actor/time against the receipt facts.

The new module migration retains all older target models. Its header trigger
rejects changes to terminal facts and a commitment that changes priced attribution
facts. Restrictive line INSERT/UPDATE/DELETE policies require a draft parent and
lock it, coordinating with commitment. Runtime grants remain narrow: line UPDATE
is not granted. RLS provides additional protection even if that privilege later
broadens. The migration cannot be rolled back while retained commitments exist.
Migration credentials remain separate from runtime credentials.

Permanent regressions: `CommitOrderDraftTests`, `OrderDraftActionGuideTests`,
`OrderCommitEndpointTests`, real `OpenFgaTenantAuthorizationTests`, and
`OrderCommitmentPostgresTests`. They cover malformed/oversized input, revoked
permissions on retries, permission separation, current reads/history, concurrent
commands, receipt-write failure rollback, restricted-role mutation rejection and
migration rollback preservation. The repository's normal `eng/verify.sh` is the
qualification gate. Requalify after lifecycle, authority, SQL, receipt format,
migration, transaction isolation or database-role changes.

## Non-claims

No stock reservation, fulfillment task, invoice, receivable, debtor assignment,
payment, approval engine, workflow continuation or external notification is
created by commitment. Price-policy revision evidence is not invented where no
policy exists. Commit records the accepted priced facts, not completion of a sale
or a financial posting. Broader effects need their own owned transactions and
contracts before introduction. Diagnostics remain best-effort telemetry; retained
order receipts are the authority for this scoped order history.
