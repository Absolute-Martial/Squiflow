# AdminApi identity import and linking

**Status:** qualified narrow slice, 2026-10-03  
**Product version:** remains v0.1.0

## Declared scope

This slice introduces one narrow identity-administration capability in the private
AdminApi boundary: import an already-existing interactive identity from the
configured ZITADEL issuer into SquiFlow, and link another verified identity from
that same issuer to an existing active SquiFlow account.

The operations are POST /api/v1/platform/accounts and
POST /api/v1/platform/accounts/{accountId}/identities. Both run behind the
existing AdminApi OIDC identity, registered Admin-device certificate, active
principal/device lookup, pinned OpenFGA model and higher-consistency
authorization boundary. The first checks can_onboard_account; the second checks
can_link_identity.

AdminApi derives the issuer from its configured authentication authority and
verifies the supplied subject through ZITADEL V2 GET /v2/users/{user_id} before
it permits a local mutation. A missing provider user is rejected, a non-human
provider user is not imported as an interactive account, and provider timeout,
malformed response, oversized response or non-success failure crosses the HTTP
boundary only as identity_provider_unavailable.

## IdentityAccess ownership

Application.IdentityAccess owns the provider-neutral business meaning:

- stable external identity is (issuer, subject);
- one external identity may bind to only one SquiFlow account;
- onboarding creates one active local account plus its first binding;
- linking requires an existing active account;
- caller idempotency keys are scoped to the administering principal and retain
  the administering device in the durable receipt;
- replay of the same intent returns the retained result;
- reuse of a key for a different intent is a conflict.

Application.IdentityAccess.Postgres owns the durable transaction. Account,
binding and onboarding receipt are committed atomically. Identity link and link
receipt are committed atomically. Unique constraints arbitrate concurrent
claims for the same external identity.

Linking serializes against account-availability changes. PostgreSQL requires an
UPDATE privilege for SELECT ... FOR UPDATE even when no update is issued, so
the AdminApi runtime role does not receive table update authority. Migration
202610030003_AccountOnboarding instead owns the narrowly scoped
identity_access.lock_account_for_identity_link(uuid) security-definer function.
The runtime role receives EXECUTE on that function plus only the SELECT/INSERT
table privileges needed by this slice.

## Provider boundary

Application.AdminApi owns the ZITADEL adapter because it is provider and
transport configuration, not IdentityAccess business meaning. Startup fails
closed unless the ZITADEL administrative API URL is HTTPS, has the same origin
as the configured OIDC authority, has a bounded request timeout and has an API
token supplied through configuration.

The adapter uses bearer authentication, caps the response at 64 KiB and JSON
depth at 12, requires returned user.userId to equal the requested subject
exactly, accepts only a human user object and never returns provider diagnostics
or credentials through the HTTP error contract.

## Falsifiable evidence and regression guards

Permanent evidence for the declared scope is:

- Application.IdentityAccess.Tests for provider-neutral intent and fingerprint
  semantics;
- Application.IdentityAccess.Postgres.Tests for a database-free runtime-model/snapshot drift guard plus durable replay, concurrent claims, account-availability serialization, atomic rollback and migration lifecycle against real PostgreSQL;
- AccountOnboardingBoundaryTests for the real AdminApi host with PostgreSQL and
  OpenFGA, including create, replay, conflict, linking, provider rejection,
  provider outage, malformed input and database least privilege;
- ZitadelIdentityVerifierTests for the current ZITADEL V2 user response
  contract, bearer request shape, human versus machine handling, subject
  mismatch, malformed/oversized response, timeout and issuer mismatch;
- deploy/database/grant-admin-api.sql for the exact runtime database grants.

Focused qualification on 2026-10-03 passed:

- Application.IdentityAccess.Tests: 12/12;
- Application.IdentityAccess.Postgres.Tests: 14/14;
- Application.AdminApi.Tests: 36/36.

The repository-wide ./eng/verify.sh result is recorded only after that exact
command is run successfully.

## Explicit non-claims

This slice does not create or mutate a ZITADEL user, decide a SquiFlow-tenant to
ZITADEL-organization mapping, create invitations or memberships, create tenant
OpenFGA tuples or roles, provide batch import/drift reconciliation, qualify a
particular ZITADEL Cloud instance/service-account scope set, or introduce
Web/Workstation login/session flows.

The tenant-to-ZITADEL organization mapping stays open until the identity
lifecycle POC proves multi-tenant users, Owner/Staff behavior, enterprise
SSO/custom-domain needs and recovery semantics.

## Production-honesty classification

The SquiFlow-owned identity import/link command semantics, PostgreSQL durability
and AdminApi security/transport boundary described above are PRODUCTION_HONEST.

Provider-side account creation, tenant/provider organization mapping,
provider-side reconciliation and live ZITADEL Cloud deployment qualification
remain NOT_INTRODUCED. They are not hidden prerequisites of this declared scope
because these operations only accept provider subjects that already exist.

BLOCKED = none for this declared slice.

## Requalification triggers

Requalify this slice when the ZITADEL V2 user-by-ID contract or administrative
authentication method changes; when issuer/origin, external-identity uniqueness,
account availability, idempotency or receipt semantics change; when the
PostgreSQL lock function/runtime grants change; or when AdminApi authentication,
Admin-device or OpenFGA consistency behavior changes.
