# Identity onboarding, account suspension and recovery decisions

**Product version:** `v0.0.1`

**Task owners:** `ADM-005`, `ADM-007`, `ADM-025`

## ADM-005 — provider onboarding selection

Provider-side human create/invite is **not selected for v0.0.1**. The accepted scope is import/link of an already-existing verified ZITADEL human through the existing stable `(issuer, subject)` account boundary.

Consequences:

- ADM-005 closes by the task's explicit import-only branch; it does not activate OPS-003 or OPS-018.
- AdminApi/runtime receives no ZITADEL human-create/invite credential.
- Invitation delivery, provider invitation state and provider-side provisioning reconciliation are not product claims.
- Existing `AccountOnboardingEndpoint` / `PostgresAccountOnboardingStore` remain the only accepted onboarding/link path.
- A later product need for provider creation is a new requalification-triggering decision and must earn a least-privilege provider mutation credential plus durable outcome reconciliation.

## ADM-007 — local account suspension/reactivation

Global local-account suspension/reactivation is a **Platform Admin/security operation**, not Tenant Owner authority. A tenant administrator that needs to deny one tenant uses membership suspension/removal; it cannot disable a shared account used by other tenants.

Implemented local semantics:

- `AccountAvailability.Disabled` blocks fresh SquiFlow account/tenant admission even when an otherwise valid OIDC token has not expired.
- Suspension does not forcibly cancel work admitted before the transition.
- Reactivation restores only local account eligibility. Current tenant membership/device/OpenFGA/domain checks still run.
- Local suspension does not claim provider-user suspension, session destruction on every device, identity deletion or credential revocation.
- Provider-side suspend/delete remains not introduced.

## Identity unlink/recovery

There is no general unlink/delete/merge surface in v0.0.1.

- Removing the last usable external identity is not supported.
- Email, display name, phone or profile similarity can never authorize merge, ownership transfer or recovery.
- A normal additional identity link requires exact provider-verified human identity and existing authenticated SquiFlow account ownership under the qualified linking boundary.
- If no usable linked identity remains, recovery is a separate support/security process with strong ownership evidence; provider outage or ambiguous identity fails closed.
- Historical onboarding/link/grant receipts retain stable actor/account identifiers after later lifecycle changes.

## Local account API

`POST /api/v1/platform/accounts/{accountId}/lifecycle/suspend` and `/reactivate` require current `can_manage_accounts` authority, exact OIDC identity and the active registered Admin-device certificate. Tenant membership administration does not confer this global authority.

The strict JSON body contains `expectedRevision` (positive integer) and `reason` (nonblank, at most 500 characters). A caller-supplied `Idempotency-Key` is required. Account registry reads expose the current revision, starting at 1. A successful transition increments it, records actor/device/reason and timestamp, and commits its immutable receipt in the same PostgreSQL transaction. Same-key/same-intent retries return the original snapshot even after a later transition; changed intent conflicts. Stale revisions, repeated current-state transitions and revision exhaustion are rejected without effect.

Lifecycle changes and identity linking lock the same account row. Runtime has no direct account `UPDATE` privilege: a restricted transition function permits only a revision-checked availability change. Apply the IdentityAccess migration before reapplying `deploy/database/apply-admin-api.psql`. Existing account/binding identity and receipt fields remain immutable to the runtime role.

`GET /api/v1/platform/accounts/{accountId}/history` exposes retained committed transitions under the distinct `can_read_audit` permission; see [administrative audit reads](ADMIN_API_AUDIT_READS.md). This history is separate from authorization/access audit.

Regression evidence is owned by `AccountLifecycleStoreTests`, `AccountLifecycleBoundaryTests`, `AccountLifecycleAdmissionTests`, and `PlatformAuditBoundaryTests`. These exercise real PostgreSQL transactions, retry/concurrency/link locking, receipt-failure rollback, runtime privileges and the same-token CoreApi admission change. Changes to availability, linking locks, migrations, authorization or runtime grants require rerunning these suites.

## Requalification triggers

Requalify if provider-side human creation is selected, unlink/delete is introduced, account merging rules change, provider issuer changes, Tenant Owner obtains global account authority, or session-revocation semantics become part of account suspension.
