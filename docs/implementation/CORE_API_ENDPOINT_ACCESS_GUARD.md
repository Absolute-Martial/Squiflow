# Core API endpoint access guard

**Scope:** existing CoreApi startup and protected HTTP ingress; no Admin API runtime.

**State:** `PRODUCTION_HONEST` for this scope; `BLOCKED = none`.

## Claim and owner

`CoreApiEndpointAccessValidation` runs after all route mappings and before the
host starts serving. Every route must have exactly one recognized
`EndpointAccessMetadata` classification. Protected routes must have explicit
ASP.NET authorization metadata and must not allow anonymous access. Public
routes must not carry contradictory authorization metadata. Invalid declarations
fail startup with fixed messages that do not expose request or provider data.

This protects the classification used by no-store, admission and request-budget
policies from accidentally disagreeing with the authentication gate. It does not
prove that a handler performs the correct resource permission or business checks;
the existing capability and endpoint tests retain that responsibility.

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
