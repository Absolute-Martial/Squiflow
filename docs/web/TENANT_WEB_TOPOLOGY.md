# Tenant Web topology decision

**Product version:** `v0.0.1`

**Decision owner:** `WEB-001`

**State:** topology selected; Web runtime remains `NOT_INTRODUCED` until its implementation gate.

## Selected topology

The first tenant-operator Web surface is a separate **Blazor Web App using Interactive Server for authenticated operator workflows**. It is online-only for business operations. Static/server rendering may be used for unauthenticated shell/error content, but SquiFlow does not introduce a browser business replica or WebAssembly business-authority runtime in this decision.

The Web application owns presentation/session composition only. CoreApi remains the authoritative tenant business API. Web business meaning is not duplicated in components.

## Identity and session boundary

- The Web application uses the ZITADEL **Tenant Access** project selected by ADM-004 and its own registered Web application/client ID.
- Authentication is Authorization Code flow through maintained ASP.NET Core/OIDC mechanisms.
- Browser authentication state uses a narrowly scoped `Secure`, `HttpOnly` server-managed cookie. Access/refresh tokens are not stored in `localStorage`, `sessionStorage`, IndexedDB or application JavaScript.
- State-changing browser requests use the framework antiforgery mechanism. Exact CSP/header values are implementation-gate evidence, not invented here.
- The Web origin/cookie is distinct from Platform Admin Web and from future customer custom-domain sessions. Cookies are not deliberately shared between them.
- Valuable business state remains server-authoritative. Interactive Server circuit state is disposable presentation state, not durable business truth.

## CoreApi path

Authenticated server-side Web code may call CoreApi using the authenticated actor's server-held OIDC access context. The CoreApi independently validates issuer/audience/current account/current tenant membership and application authorization. A Web session or hidden button is never authority.

The Tenant Web client ID is a security input for accepted high-risk actions. ADM-034 binds Owner transfer to the exact authenticated token `azp`, accepted provider `acr`, and recent provider `auth_time`; arbitrary headers/cookies cannot substitute for those claims.

## Deployment promises

The first implementation does not claim transparent multi-node circuit failover. A single Web node or deliberately sticky/drained Interactive Server deployment is acceptable initially because authoritative state is outside the circuit. Redis/distributed circuit state is not selected without measured need.

Network loss may lose disposable circuit state but never reports an uncommitted operation as successful. Reconnect requires current authentication/authorization/resource state again.

## Exclusions

This decision does not introduce:

- browser offline business data or queued mutations;
- a generic BFF/security framework beyond the chosen Blazor/OIDC host;
- customer portal identity/session reuse;
- custom-domain login lifecycle;
- Platform Admin Web;
- Web project scaffolding before its implementation gate.

## Requalification triggers

Re-open this decision if render mode changes, browser token custody changes, a second Web node requires non-sticky circuit failover, the Tenant Access application/client changes, custom-domain sessions are introduced, or CoreApi moves behind a materially different session/delegation boundary.
