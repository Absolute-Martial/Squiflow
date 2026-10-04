# Core API endpoint access guard

**Scope:** existing CoreApi startup and protected HTTP ingress; no Admin API runtime.

**State:** prior accepted baseline was `PRODUCTION_HONEST`; the current strengthened declaration/execution binding is `BLOCKED` pending its updated CoreApi regression run and one exact post-change repository gate.

## Claim and owner

`CoreApiEndpointAccessValidation` runs after all route mappings and before the
host starts serving. Every route must have exactly one recognized
`EndpointAccessMetadata` classification. Protected routes must have explicit
ASP.NET authorization metadata and must not allow anonymous access. Public
routes must not carry contradictory authorization metadata. Invalid declarations
fail startup with fixed messages that do not expose request or provider data.

This protects the classification used by no-store, admission and request-budget
policies from accidentally disagreeing with the authentication gate. For every
`Authorized*` route, the classification also selects one canonical typed
application-authorization contract. A global post-authentication middleware reads
that validated endpoint declaration, resolves the current tenant boundary and
executes the same requirement set before endpoint dispatch; handler execution is
therefore downstream of the declared OpenFGA-backed admission instead of being
responsible for remembering it. Resource handlers and provider adapters still own
the meaning of those typed requirements and the capability/business checks after
admission.

## Evidence and regression guard

`EndpointAccessValidationTests` exercises missing, duplicate, unknown and
contradictory declarations. Its real-host HTTP regression enumerates all current
protected route/method pairs and verifies `401` plus `no-store` without a token,
including requests carrying forged platform-admin, registered-device and
Tailscale login headers. These headers do not grant authority to CoreApi.

`./eng/verify.sh` is the recurring regression gate. Requalify when route mapping,
classification, authentication/authorization middleware, inherited authorization
metadata or policy selection changes. Dynamically added routes after startup are
not supported by this guarantee.

The 2026-10-02 local full parallel gate passed locked restore, formatting,
Release build and all 351 tests, with zero failures/skips and zero build
warnings/errors. It did not collect coverage or qualify remote CI, private
network deployment or a complete backend.

## Admin boundary

This guard does not implement platform authorization, private ingress,
registered-device proof, authoritative administration audit, or Admin API
operations. Those remain separate work governed by
`docs/admin/ADMIN_SURFACES.md` and
`docs/operations/PRIVATE_ADMIN_NETWORK_AND_PODMAN.md`. Tenant identity, membership,
permissions and a private-network header are never substitutes for those gates.

## Application-authorization declaration guard, 2026-10-04

Every `Authorized*` CoreApi access classification requires matching `CoreApiApplicationAuthorizationMetadata` in addition to `IAuthorizeData`. The metadata carries the canonical non-empty typed requirement set selected from the access classification. Startup rejects missing, mismatched or substituted requirement sets and rejects an authorized route without the `tenantId` route boundary required by the admission middleware.

`UseCoreApiApplicationAuthorization` is the mandatory pre-handler admission path. It resolves and caches the current tenant context, calls `CoreApiDeclaredAuthorization` with the exact matched endpoint metadata and passes its requirement set to `IAuthorizationService`, then marks the request admitted before dispatch. Existing handler calls to `CoreApiDeclaredAuthorization` observe that middleware state and cannot introduce a second independent permission constant. The declaration is therefore executable authority, not a parallel comment about authority. Current Order create/price-preview/revision contracts declare both of their required permissions and execute them together. The Order-actions endpoint still performs explicit secondary permission probes only to calculate optional action affordances after its declared `ViewOrders` admission succeeds; those probes are not route-admission authority.

`EndpointAccessValidationTests.RuntimeAuthorizationUsesTheValidatedEndpointDeclaration` is the focused structural guard. The current source must rerun that suite plus `./eng/verify.sh` before the strengthened claim returns to `PRODUCTION_HONEST`.
