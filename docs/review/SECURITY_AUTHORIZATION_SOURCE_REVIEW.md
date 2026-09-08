# Security and Authorization Source Review — v0.0.15

**Status:** Source-backed review. Current accepted architecture is owned by `docs/security/IDENTITY_AND_SESSIONS.md` and `docs/security/TENANT_PERMISSIONS.md`.

This review records how the previously reviewed OpenID Connect, OWASP API Security, Zanzibar, and ASP.NET Core authorization material maps into the current **ZITADEL + OpenFGA** architecture.

## 1. OpenID Connect specifications

Source index: https://openid.net/wg/connect/specifications/

Relevant specifications reviewed:
- OpenID Connect Core 1.0;
- Discovery 1.0;
- RP-Initiated Logout 1.0;
- Back-Channel Logout 1.0;
- Dynamic Client Registration 1.0;
- Native SSO for Mobile Apps implementer's draft;
- RFC 8252 and RFC 9700 for current native/OAuth security practice.

### Current SquiFlow mapping

ZITADEL is now the selected OIDC identity provider/platform.

SquiFlow keeps the following protocol conclusions:
- stable external identity is `(issuer, subject)`, not email;
- Workstation is a public native client using system browser + Authorization Code + PKCE `S256`;
- no reusable native client secret;
- issuer/audience/signature/expiry/state/nonce and callback validation are mandatory;
- custom tenant domains are application origins, not automatically separate identity issuers;
- step-up/recent authentication can use OIDC/ZITADEL capabilities;
- logout is separated into local app session, SquiFlow session/revocation, and provider logout/back-channel behavior where configured;
- Dynamic Client Registration and the OpenID Native SSO mobile draft remain outside the baseline.

OIDC authentication still does **not** represent current business authorization.

Current ZITADEL references:
- https://zitadel.com/docs/guides/integrate/login/oidc
- https://zitadel.com/docs/guides/integrate/login/oidc/login-users
- https://zitadel.com/docs/guides/solution-scenarios/b2b

## 2. OWASP API Security Top 10 — 2023

Source: https://owasp.org/API-Security/editions/2023/en/0x11-t10/

The important SquiFlow conclusions remain:

### Object-level authorization
Knowing an object ID is not authority. Scope tenant-owned lookups using authoritative `TenantContext` where possible, then perform fine authorization.

### Broken authentication
Use ZITADEL/OIDC rather than inventing credential/token protocols. Recovery/step-up/login paths require stronger abuse controls than ordinary endpoints.

### Property-level authorization
Use explicit request/response contracts. Hidden UI properties are not security. Sensitive cost/margin/credit/internal-note fields require server-side projection/command authorization.

### Resource consumption
Bound body/upload/batch/page/CPU/memory/provider-cost work. Rate limiting alone does not control a single expensive document/image/report request.

### Function-level authorization
`/tenant-admin` or `/platform-admin` paths organize the API; they do not grant authority. Endpoint policy + OpenFGA/application authorization remain mandatory.

### Sensitive business flows
Protect actual high-impact flows such as invitations/recovery/ownership transfer/refund/provider-paid actions instead of adding generic friction everywhere.

### SSRF
Webhook/remote-fetch features require a controlled outbound HTTP policy, destination validation, private/metadata-range defense, redirect revalidation, limits and deadlines.

### Security misconfiguration
Production CORS/cache/header/error/debug behavior is explicit and fail-safe.

### API inventory
Generate endpoint/version inventory from executable metadata/OpenAPI; do not revive a manual CSV as architecture truth.

### Unsafe third-party consumption
ZITADEL/OpenFGA/storage/provider responses are still external inputs: validate schemas/statuses/timeouts and never turn provider failure into accidental allow or business corruption.

## 3. Zanzibar paper and the OpenFGA correction

Source: https://www.usenix.org/system/files/atc19-pang.pdf

The earlier review correctly rejected **copying Google's planet-scale machinery**, but incorrectly generalized that into rejecting any relationship-based authorization service.

OpenFGA is now the selected SquiFlow authorization engine. That is compatible with the useful Zanzibar lessons while still avoiding unnecessary Google-scale infrastructure.

### What SquiFlow adopts

- relationship-based permission checks where they clarify tenant/custom-role/resource authorization;
- explicit authorization-model versions;
- authorization-change freshness as a correctness concern;
- safe permission revocation behavior;
- current permission relationships are not trusted merely because a token/snapshot is old but valid;
- cached/lower-consistency results are selected deliberately by operation risk;
- audit/reconciliation around authorization mutations.

### What SquiFlow still does not copy

- Spanner;
- zookie protocol;
- Leopard/specialized set-index architecture;
- Google's global authorization cache topology;
- a universal ACL tuple for every business row;
- a second home-grown authorization service wrapping OpenFGA;
- putting workflow/payment/stock arithmetic into the authorization graph.

### Current OpenFGA guidance reviewed

OpenFGA's current documentation directly supports SquiFlow's custom-role requirement:
- authorization model defines stable object types/relations;
- tenant/user-defined role instances are first-class `role` objects/tuples;
- role assignees can receive supported permissions through usersets/relations;
- a new model deployment is not required every time a tenant creates a new custom role.

OpenFGA also recommends:
- opaque/non-PII tuple IDs;
- explicitly specifying authorization model ID;
- controlled immutable model migrations;
- deliberate consistency selection (`MINIMIZE_LATENCY` vs `HIGHER_CONSISTENCY`).

Current references:
- https://openfga.dev/docs/modeling/custom-roles
- https://openfga.dev/docs/best-practices/modeling-roles
- https://openfga.dev/docs/getting-started/immutable-models
- https://openfga.dev/docs/interacting/consistency
- https://openfga.dev/docs/getting-started/tuples-api-best-practices

## 4. ASP.NET Core resource-based/policy authorization

Sources:
- https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based?view=aspnetcore-10.0
- https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0

ASP.NET Core remains the in-process host integration layer.

Current path:

```text
ZITADEL-authenticated actor
→ authoritative SquiFlow TenantContext
→ coarse endpoint policy
→ tenant-scoped resource resolution
→ IAuthorizationService semantic requirement
→ OpenFGA Check where relationship/permission belongs in OpenFGA
→ SquiFlow workflow/domain/state validation
→ concurrency/idempotency
→ transaction
```

Important implementation rules:
- handlers do not depend on invocation order;
- handlers are side-effect-free with respect to the business mutation;
- important actions use semantic requirements (`RefundPayment`, `ApproveQuote`, etc.);
- Create authorization uses a real parent/scope/context rather than a fake persisted object;
- OpenFGA allow is not enough when domain/workflow state is invalid;
- resource lookup remains tenant-scoped even if OpenFGA also models relationships.

## 5. Cross-system authorization mutation issue

Selecting OpenFGA adds a real distributed-state edge case that must not be hidden by architecture minimalism.

SquiFlow role/grant metadata/audit and OpenFGA tuples cannot be committed as one local ACID transaction.

Therefore Web-only role changes require a durable/reconcilable operation:

```text
request + idempotency identity
→ validate current actor/delegation/tenant
→ durable Pending change record
→ OpenFGA tuple write/delete
→ verify intended relation state
→ persist Applied + SquiFlow authorization revision/audit
```

If the process dies after OpenFGA changed but before SquiFlow recorded completion, retry/reconciliation checks actual tuple state rather than blindly repeating or reporting success.

A grant/revocation is not shown as successfully applied until its OpenFGA outcome is known/applied.

This is more important than making the implementation look like a single simple database transaction.

## 6. Current combined architecture

```text
                    ZITADEL
             authentication / MFA / SSO
                        │
                        ▼
            SquiFlow account/session
                        │
                        ▼
             authoritative TenantContext
                        │
                        ▼
       ASP.NET Core endpoint / requirement
                        │
                        ▼
                    OpenFGA
          role / permission / relationship
                        │
                        ▼
              SquiFlow domain/workflow
        state / facts / invariants / version
                        │
                        ▼
          tenant-scoped persistence + txn
```

For Workstation offline mode:

```text
ZITADEL-authenticated account/device
→ local effective permission snapshot for UX
→ local-capable durable work
→ later sync
→ server repeats current OpenFGA + domain + tenant checks
```

## 7. Net result

The current architecture is **not** "OIDC + home-grown role tables only" and it is also **not** "put every decision into OpenFGA".

The accepted split is:

- ZITADEL: identity/authentication;
- OpenFGA: application roles/custom roles/relationships/permission checks;
- ASP.NET Core: API authorization integration/policies/requirements;
- SquiFlow: workflow/domain/business correctness;
- persistence: tenant data isolation/transactions;
- Workstation: local snapshot for UX only, server reauthorization on sync.

This preserves the source-backed security lessons without either overbuilding Zanzibar-scale infrastructure or minimizing authorization until important edge cases disappear.
