# Backend high-risk action admission

**Product version:** `v0.0.1`

**Decision / implementation owner:** `ADM-034`

## Current admitted operation

The only operation admitted by this slice is **initial Tenant Owner transfer**. Ordinary reads, role browse, grant proposal/reconciliation and normal tenant mutations do not inherit a blanket stronger-authentication requirement.

## Required provider evidence

CoreApi accepts Owner transfer only after ordinary JWT validation and current tenant/initial-Owner authorization have succeeded, and only when the authenticated principal also carries:

1. `azp` exactly equal to the configured Tenant Web application/client ID;
2. `acr` exactly equal to the configured accepted strong-authentication context;
3. provider-authenticated numeric `auth_time` no older than the configured maximum (default 300 seconds);
4. `auth_time` no further in the future than the configured skew tolerance (default 60 seconds).

Missing configuration fails the guarded operation with safe `503`. Missing, stale, wrong-client or wrong-strength evidence fails with safe `403`. Arbitrary headers, cookies, query values and client booleans are ignored.

The normal CoreApi authentication layer independently validates issuer, audience, signature and lifetime before these claims are consumed. Step-up evidence never replaces current SquiFlow membership or authorization.

## Provider qualification status

Static claim/admission fixtures are implemented. The exact ZITADEL Cloud `acr` value/flow used by the production Tenant Web application still requires the isolated live-provider proof from ADM-004/ADM-034; static token fixtures do not claim that provider mapping.

## Recovery

If the configured stronger factor is unavailable, there is no silent fallback to weaker Owner transfer. Recovery remains a separate support/security process with ownership evidence; it does not mint ordinary tenant authority.

## Requalification triggers

Requalify on Tenant Web client ID, accepted ZITADEL `acr`, token claim provenance, authentication-age/skew policy, issuer/audience configuration, Owner-transfer semantics or any new operation added to `HighRiskAction`.
