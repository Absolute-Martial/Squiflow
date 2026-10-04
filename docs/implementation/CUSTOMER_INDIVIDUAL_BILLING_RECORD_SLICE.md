# Individual customer billing record

Product version: v0.0.1. Focused owner: Customers. Owner decision, 2026-10-03:
a display name, optional email/phone and active/inactive state are sufficient for
the first record. It is independent of application login accounts. Debtor
assignment requires separate billing authority and is not introduced here.

## Owned behavior

- `POST /api/v1/tenants/{tenantId}/customers/individuals` creates an active record
  with revision one and caller-scoped semantic idempotency.
- `GET .../individuals/{individualId}` reads that tenant's record.
- `POST .../individuals/{individualId}/availability` takes `expectedRevision` and
  `availability` (`active` or `inactive`), plus an Idempotency-Key. A change advances
  revision and retains actor/time; stale revision or already-current state is a
  stable conflict rather than an invented new change.

Current account and membership precede pinned OpenFGA checks on every request and
retry. `individual_creator`, `individual_viewer` and
`individual_availability_editor` are independent persisted relations, intersected
with verified contextual membership. A creator receives the record it submitted;
an availability editor receives only identity, availability, revision and change
time. That grant does not disclose contact details. No public account identifier
or sign-in binding is introduced. All protected outcomes remain no-store.

Names and optional contacts are normalized and bounded in Customers. The new HTTP
payloads reject unknown/duplicate fields and use a bounded 4 KiB reader regardless
of Content-Length. Responses use explicit host contracts; storage rows do not
escape the provider. Current record reads are not financial-authority checks.

## Durability and evidence

The existing Customers PostgreSQL session sets tenant context transaction-locally.
Explicit tenant predicates, forced RLS and separate module SQL protect records and
caller/operation-scoped receipts. Creation and availability changes commit their
retained result together. A replay returns its historical result even if the
current availability subsequently changes. The migration owns its tables and
constraints; runtime grants allow only the narrow availability columns to update
and do not allow deletion of records or receipts.

Permanent regressions: `CustomerIndividualTests`, real
`CustomerIndividualPostgresTests`, `CustomerIndividualEndpointTests` and
`OpenFgaTenantAuthorizationTests`. Qualification uses the normal `eng/verify.sh`.
Requalify after record shape, normalization, state transitions, authorization,
receipt persistence, SQL, RLS or deployment privilege changes. Contacts must never
be logged as diagnostic payloads.

## Non-claims

No uniqueness/identity deduplication by name/email/phone, account login, invitation,
representative relationship, import, contact edit/delete, debtor assignment,
invoice, credit, payment allocation or customer portal is implemented. Duplicate
people are not silently merged. Inactive records and their historical receipts
remain readable by current authorized viewers. No legal or fiscal compliance is
claimed by merely storing a customer record.
