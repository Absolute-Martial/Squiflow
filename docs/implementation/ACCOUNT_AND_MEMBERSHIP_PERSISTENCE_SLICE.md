# Account and membership persistence integration

This owner covers shared IdentityAccess/Tenancy capability and PostgreSQL behavior.
It does not introduce a new AdminApi or tenant CoreApi mutation endpoint. Existing
host-neutral ports accept established actor IDs; constructing an actor does not
prove identity, device registration, permission or ownership. A calling host must
establish current authority before invoking them, including receipt reads/replays.

## Scope

Account onboarding atomically creates an active application account, binds an
exact externally verified issuer/subject and retains a caller-scoped receipt.
Linking adds an exact external identity to an existing active account and retains
its separate receipt. It cannot move a binding between accounts. It locks current
account availability against concurrent disabling. Failed preconditions do not
create successful receipts; callers can retry after resolving the failure.

Membership invitations register an existing application account within an active
tenant in the `Invited` state, which gives no workspace authority. Explicit,
expected-revision commands activate, suspend, resume or remove membership.
Removal is terminal in this scope. It changes membership eligibility only; it
neither creates nor deletes OpenFGA permission tuples. Current account and tenant
availability remain independent checks on every protected operation.

The capability owns transitions; the adapter owns transactions, row/advisory
locks and mapping. Single-flight command locks scope idempotency to the actor;
a second transaction lock serializes commands for one tenant/account, including
concurrent invitations before any row exists. Expected failures are explicit
outcomes. Successful effects and their receipts commit together. Same intent/key
replays the original result after later state changes without reapplying it;
changed intent/key conflicts. Revision exhaustion is an explicit result.

Receipt principal/device IDs record attribution, not authorization decisions.
Receipts have no automatic retention cleanup. These global security/control
schemas are not tenant business tables: only a separately authorized process
identity may receive their narrow mutation rights. CoreApi's existing runtime
identity gains no additional database rights from these registrations.

## Compatibility and safety

IdentityAccess's original target model and Tenancy's original/provisioning target
models remain immutable. New migrations have complete target models; named
migration targeting understands existing capability-owned identifier syntax.
Rollback refuses to discard retained successful receipts. EF performs individual
migration transactions; an empty later migration may be removed before an older
migration refuses rollback, so operators must inspect applied/pending state.

Existing membership registration timestamps remain their original facts. Upgrade
does not fabricate activation/suspension times for historical records that never
stored them. Those fields can remain unknown; resuming records the actual current
activation time. Legacy suspended rows may lack a suspension time at baseline
revision 1. New suspension commands record it. Old mutation writers that omit new
revision/lifecycle columns must be drained before upgrade; the previously qualified
CoreApi only reads membership state and remains compatible.

Runtime SQL stays in adapter-owned embedded resources, parameterized and executed
inside owned transactions. Membership maps selected columns by name. Provider
exceptions remain internal; these adapters do not expose HTTP error responses.
Cancellation propagates to connection acquisition, locks, queries and commits;
ambiguous commit results require retrying the same semantic key. Existing bounded
host/database policies apply when a host composes these ports.

## Source admission

The existing reference routing owner remains
`docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md`. The pinned
FullStackHero revision `3f2959e683e9f83f13e55e1678c9119f63c7e8e5` (MIT) was inspected
at `src/Modules/Identity/Modules.Identity/Services/UserService.cs`. Its password,
automatic principal provisioning and combined role facade remain rejected under
the Identity decision in `FULLSTACKHERO_BACKEND_ADOPTION_LEDGER.md`. No source was
copied. Exact external binding and membership facts remain capability-owned;
existing Npgsql/EF packages and repository transaction/receipt patterns supply
provider mechanics. No generic repository, provisioning engine or new dependency
is introduced.

## Evidence and requalification

Permanent guards are `AccountOnboardingIntentTests`, `AccountOnboardingStoreTests`,
`AccountOnboardingMigrationTests`, `TenantMembershipLifecycleTests` and
`TenantMembershipLifecyclePostgresTests`, plus the existing directory, migration
registry and repository architecture suites. They exercise input bounds,
concurrent commands, exact replay/conflict, account disable races, active membership
revocation, restricted-role denials, failed receipt rollback, historical upgrade
and destructive rollback protection against PostgreSQL.

Requalify after changing transitions, receipt semantics, schema targets, provider
versions, caller authority, migration IDs, runtime grants or transaction ownership.
Host exposure, external identity verification/recovery, invitation delivery,
account suspension administration, permissions/delegation, general audit,
production identity topology and deployment recovery remain separate claims.

Current checkout state: `BLOCKED` for integration qualification. Concurrent
provider-provisioning edits introduced `TenantIdentityOrganizationRow` and
`ProviderAccountProvisioningRow` without matching migrations/snapshots. The
IdentityAccess provider run returned 5 passed / 9 failed; the subsequent Tenancy
provider run returned 1 passed / 17 failed because the shared migrator refuses
that model drift. An earlier Tenancy run passed 17 tests before those changes;
that earlier result does not qualify the current checkout.

Current independent evidence: all 23 Tenancy unit tests pass, and CoreApi locked
restore/build/publish succeeds with zero warnings/errors. Architecture testing
returns 8 passed / 1 failed: the incoming `PostgresTenantDirectory` query remains
inline rather than an embedded SQL resource. The normal full repository gate has
not been rerun against this unstable checkout. Do not suppress model-drift checks
or weaken SQL placement guards. Resolve shared-file ownership, finish the missing
models/migrations and query placement, then rerun the normal gate. No remote or
production deployment check is claimed.
