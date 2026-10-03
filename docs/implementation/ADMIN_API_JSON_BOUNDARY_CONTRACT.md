# AdminApi JSON boundary contract

**Status:** qualified narrow correction, 2026-10-03
**Product version:** remains v0.1.0

## Declared scope and ownership

The existing private AdminApi host owns JSON shape validation before passing
requests to Tenancy and provider responses to IdentityAccess. This correction
closes two wrong-kind failures in those introduced boundaries; it adds no route,
domain operation, permission, persistence contract or dependency.

`BoundedAdminJson.TrySinglePositiveInt` accepts exactly one correctly named JSON
number that is representable as a positive `Int32`. A non-number, out-of-range or
fractional number, missing field, duplicate field or additional field is invalid.
Membership transitions return `400 / invalid_membership_request`; tenant
lifecycle commands return `400 / invalid_tenant_lifecycle_request`. These invalid
requests change no lifecycle state or revision and retain no command receipt.
A corrected request can therefore use the same idempotency key.

The ZITADEL verifier requires an object response root before looking up `user`.
A valid JSON root of another kind is an invalid provider success response and
becomes `503 / identity_provider_unavailable`, matching the existing malformed
response contract in `ADMIN_API_IDENTITY_IMPORT_AND_LINKING.md`. Onboarding and
linking retain no account, binding or command receipt for these failures. Provider
recovery allows the same intended command and key to be retried.

The existing authentication, HTTPS, registered-device and pinned OpenFGA checks
precede request parsing. Protected responses remain `no-store`. Unexpected
internal exceptions retain the global `500 / internal_error` classification.

## Security and resource review

The correction uses explicit `JsonValueKind` checks at the use sites rather than
catching all `InvalidOperationException` instances. It preserves the existing
4 KiB/depth-2 lifecycle request limit and 64 KiB/depth-12 provider response limit,
provider timeout/cancellation, same-origin configuration and bearer request.
It does not weaken authorization or change durable mutation ownership.

Provider response contents and bearer credentials are not copied into HTTP
failure responses. The existing exception handler logs only failure code,
status, exception type and trace identifier, without exception diagnostics.
The real-host provider tests assert response secrecy as well as nonmutation.

## Evidence and recurring guard

Permanent guards in the existing `Application.AdminApi.Tests` project are:

- `InvalidRevisionShapesHaveNoDurableEffectsAndDoNotConsumeIdempotencyKeys`:
  both lifecycle endpoints, exact status/code, real PostgreSQL state and receipt
  checks, corrected same-key success and replay;
- `RegisteredDeviceAuthorityPrecedesLifecycleBodyParsing`: missing registered
  device remains `403 / admin_device_required` for malformed request bodies;
- `MismatchedProviderIdentityAndMalformedSuccessResponsesFailClosed`: actual
  verifier parsing of malformed syntax, wrong root kinds and invalid user shapes;
- `MalformedProviderJsonThroughActualVerifierHasNoIdentityEffectsAndCanBeRetried`:
  actual verifier with controlled HTTP responses through both real AdminApi
  commands, safe `503`, real PostgreSQL nonmutation and corrected same-key retry.

The focused Release run passed **44/44 tests**, zero failures/skips and zero
warnings/errors. On 2026-10-03 the normal parallel `./eng/verify.sh` passed locked
restore, formatting, Release build and **640 tests across 15 suites**, zero
failures/skips and zero warnings/errors, including real PostgreSQL and OpenFGA
guards. That exact command remains the recurring repository guard.

## Classification and non-claims

The JSON validation and failure classification described here are
`PRODUCTION_HONEST`; `BLOCKED = none` for this correction.
This document does not qualify a live ZITADEL deployment, add request
budgets, permission administration, provider-side user creation or other backend
responsibilities. Existing focused owners retain their domain authority.

Requalify when JSON field/root kinds, integer bounds, request/response parser
limits, endpoint error mapping, authorization ordering, retry receipts or the
ZITADEL user lookup contract changes.
