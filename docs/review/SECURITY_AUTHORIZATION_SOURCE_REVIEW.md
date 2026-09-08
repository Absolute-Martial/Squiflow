# Security and Authorization Source Review — v0.0.15

**Status:** Current source-backed review.  
**Method:** Sources were reviewed sequentially in the order supplied. This file records what each source actually supports, what SquiFlow adopts, what it adapts, and what it deliberately does not add.

## 1. OpenID Connect specifications

Source index: https://openid.net/wg/connect/specifications/

Relevant specifications reviewed for SquiFlow:
- OpenID Connect Core 1.0;
- OpenID Connect Discovery 1.0;
- RP-Initiated Logout 1.0;
- Back-Channel Logout 1.0;
- Dynamic Client Registration 1.0;
- Native SSO for Mobile Apps implementer's draft.

Current OAuth native/security guidance was also cross-checked against RFC 8252 and RFC 9700 because OpenID Connect Core itself predates the newest OAuth security BCP.

### What the specifications establish

OpenID Connect is the authentication/identity layer over OAuth 2.0. ID Tokens carry authentication claims. `iss`, `sub`, `aud`, token expiration/signature and transaction-bound values must be validated according to the protocol. Discovery metadata is tied to an issuer and the returned issuer must match exactly. `max_age`/`auth_time` and authentication-context values can support explicit reauthentication/step-up requirements. RP-Initiated Logout and Back-Channel Logout solve different parts of logout propagation.

RFC 8252 establishes the external/system-browser pattern for native applications and treats installed native applications as public clients that cannot safely keep a shared client secret. It requires PKCE for public native clients and defines loopback redirects for desktop applications. RFC 9700 strengthens current practice: authorization-code flows, PKCE (with `S256`), exact redirect matching apart from the native loopback port exception, CSRF/mix-up defenses, and no open redirectors.

### SquiFlow adoption

1. SquiFlow interactive identity uses **OpenID Connect**, while SquiFlow application permissions remain a separate authorization system.
2. Keep one canonical configured SquiFlow issuer initially. Tenant custom domains are relying-party/application origins, not new tenant identity issuers.
3. Native Workstation is a **public native client**:
   - system browser;
   - Authorization Code flow;
   - PKCE `S256`;
   - transaction-bound state/nonce as appropriate;
   - no embedded reusable client secret.
4. Native callback remains a packaging/security POC:
   - prefer an OS/app-claimed HTTPS mechanism where Windows packaging proves it reliably; or
   - use a loopback IP callback bound only to `127.0.0.1`/`::1`, random port, open only during login.
5. Identity record is keyed by stable issuer + subject, not email. Email/name are mutable profile claims.
6. Discovery is only performed against configured/trusted issuers. A tenant/user cannot provide an arbitrary discovery URL and make SquiFlow fetch it.
7. ID/access-token validation is centralized; individual endpoints do not hand-roll JWT parsing.
8. High-risk Admin operations can request recent/strong authentication using `max_age`, `auth_time` and supported authentication-context semantics.
9. Logout is decomposed into:
   - local application session termination;
   - RP-initiated identity-provider logout where appropriate;
   - SquiFlow session/token revocation;
   - optional back-channel logout for Web sessions when supported by the chosen identity provider.
10. Dynamic Client Registration is **not baseline**. SquiFlow has a controlled set of application clients and registered custom-domain callbacks, so automatic arbitrary client registration adds attack surface without current value.
11. OpenID Native SSO for Mobile Apps is **not the Windows Workstation design**. It is mobile/same-vendor-app oriented and remains an implementer's draft.

### Important boundary

An OIDC ID Token proves an authentication event. It does **not** prove that the user currently has `payments.refund`, `roles.manage`, access to a particular Order, or authority in a particular tenant. SquiFlow resolves current tenant membership and application authorization separately.

---

## 2. OWASP API Security Top 10 — 2023

Source: https://owasp.org/API-Security/editions/2023/en/0x11-t10/

The Top 10 and the individual risk pages were reviewed because the index points to ten separate failure classes.

### API1 — Broken Object Level Authorization

Every API operation accepting an object identifier needs object/resource authorization. UUIDs/random IDs reduce enumeration but are not authorization.

SquiFlow consequence:
- derive tenant scope from authenticated membership;
- constrain the resource lookup by tenant/context where possible;
- then run resource/action authorization;
- never authorize merely because the caller knows an object ID.

### API2 — Broken Authentication

Authentication, account recovery and sensitive identity changes need strong controls. SquiFlow should use standards rather than invent credential/token flows.

SquiFlow consequence:
- OIDC implementation owns interactive authentication;
- login/recovery/step-up endpoints have stricter abuse limits than ordinary APIs;
- tokens are fully validated;
- sensitive owner/MFA/security changes require recent authentication according to risk.

### API3 — Broken Object Property Level Authorization

Authorization can fail on individual fields even if object access is correct, and mass assignment can let a client mutate properties it was never meant to control.

SquiFlow consequence:
- API contracts use explicit request/response DTOs and allowlisted mapping;
- do not bind client JSON directly onto persistence/domain entities;
- sensitive read fields such as cost, margin, credit limit and privileged notes are projected only when authorized;
- hidden UI fields are never treated as a protection.

### API4 — Unrestricted Resource Consumption

Limits must account for CPU, memory, request size, expensive processing, downstream calls and paid provider operations.

SquiFlow consequence:
- endpoint/work-class budgets;
- bounded upload sizes, page sizes, batch sizes and execution deadlines;
- admission control for document/image/report processing;
- per-tenant/provider budgets where external APIs can create cost;
- rate limiting alone is not enough for a single expensive request.

### API5 — Broken Function Level Authorization

An `/admin` path is not authorization. HTTP method/path guessing must never expose a privileged function.

SquiFlow consequence:
- `/tenant-admin` and `/platform-admin` are organizational boundaries only;
- endpoint policy + application authorization are mandatory;
- deny by default for privileged operations;
- test normal users against admin verbs/routes.

### API6 — Unrestricted Access to Sensitive Business Flows

Some APIs can be technically authorized but still economically/business-abusable through automation.

SquiFlow consequence:
- classify only actual sensitive flows, for example invitations/recovery, ownership transfer, payment/refund attempts, externally paid messaging, selected public/client-client flows;
- apply flow-specific controls rather than CAPTCHAs or arbitrary throttles everywhere.

### API7 — SSRF

User-controlled URLs can make the server reach internal/cloud/private resources.

SquiFlow consequence:
- future webhooks, remote-file import, callback testing or integration URL features use a dedicated outbound-fetch policy;
- scheme/host/port restrictions;
- DNS/IP checks and private/metadata-range blocking where appropriate;
- bounded redirects with destination revalidation;
- response size/time limits;
- no raw internal response reflection.

### API8 — Security Misconfiguration

Security can fail through unsafe defaults, unnecessary features, CORS/cache/header mistakes, detailed errors, stale dependencies or permissive infrastructure.

SquiFlow consequence:
- production security-header/CORS/cache policy is explicit per Web/API surface;
- private responses are not accidentally public/browser-cacheable;
- stack traces and provider secrets never reach ordinary clients;
- unused debug/development endpoints are absent from production.

### API9 — Improper Inventory Management

API/version/deployment/data-flow visibility is a security requirement.

SquiFlow consequence:
- endpoint inventory is generated from executable endpoint metadata/OpenAPI during CI/release;
- each public version has owner, audience/environment and retirement policy;
- stale `/v1-beta`, debug or test endpoints cannot quietly remain live;
- third-party sensitive data flows are inventoried.

This **does not revive the old manual architecture CSV**. Runtime/API inventories are generated verification artifacts, not hand-maintained design truth.

### API10 — Unsafe Consumption of APIs

Managed or well-known third-party responses remain untrusted input.

SquiFlow consequence:
- outbound integrations use TLS, deadlines and bounded retries;
- validate provider payload schemas/limits before persistence or downstream processing;
- do not blindly follow redirects;
- isolate provider failures and untrusted content from authoritative business logic.

---

## 3. Zanzibar: Google's Consistent, Global Authorization System

Source: https://www.usenix.org/system/files/atc19-pang.pdf

The entire 15-page paper was reviewed.

### What Zanzibar actually solves

Zanzibar provides a uniform relationship/ACL model across many Google services, supports indirect/nested relationships, and addresses authorization consistency at planetary scale. A central correctness problem is the paper's **“new enemy”** case: a permission revocation must not be followed by a content/action decision evaluated against an older permission snapshot. Zanzibar uses externally consistent storage plus bounded-freshness tokens (“zookies”) to preserve causal ordering. ACL writes are committed with a changelog atomically; Watch lets clients update derived authorization views. The production system additionally uses specialized indexing, caching, deduplication and large-scale resource isolation.

### What SquiFlow adopts

SquiFlow should adopt the *correctness lessons*, not Google-scale machinery:

1. Authorization changes have a version/revision.
2. A tenant role/grant/membership/entitlement update commits:
   - the authorization change;
   - an incremented tenant authorization revision;
   - security/audit evidence;
   - the durable outbox/invalidation event;
   in the same authoritative transaction.
3. Server-side authoritative commands must not rely on an unversioned stale permission cache.
4. Workstation effective-permission snapshots carry the tenant authorization revision. They are UX/offline context, not server authority.
5. If a server authorization cache is introduced later, its cache key/validity is tied to the authorization revision; role revocation advances the revision and prevents a stale cache from surviving unnoticed.
6. Derived search/read models that filter by authorization need an explicit freshness/rebuild/invalidation policy.
7. Tenant-aware quotas/resource isolation remain important for authorization and API checks too.

### What SquiFlow deliberately does not adopt

- no Zanzibar service;
- no Spanner dependency;
- no global relationship-tuple database;
- no zookie protocol;
- no Leopard-like specialized set index;
- no universal per-row ACL graph;
- no authorization microservice merely because Google has one.

Owner/Staff roles + scoped permission grants + resource-based checks are the correct starting complexity for SquiFlow. Relationship ACLs can be introduced later only for a demonstrated business need such as explicit sharing/assignment that the simpler model cannot express cleanly.

---

## 4. ASP.NET Core resource-based authorization

Source: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based?view=aspnetcore-10.0

### What the Microsoft guidance establishes

Resource-based authorization cannot always be expressed as an `[Authorize]` attribute because attribute evaluation occurs before the application loads the resource. ASP.NET Core provides `IAuthorizationService.AuthorizeAsync(...)`, authorization requirements and `AuthorizationHandler<TRequirement,TResource>`. `OperationAuthorizationRequirement` is a convenience for CRUD-style operations.

### SquiFlow adoption

Do not build a competing .NET authorization framework. Use ASP.NET Core authorization as the host/runtime primitive.

The SquiFlow request path becomes:

```text
Authenticate
→ derive authoritative tenant/platform context
→ coarse endpoint/function policy
→ tenant-scoped resource lookup
→ IAuthorizationService resource/action requirement
→ business/domain/workflow invariant validation
→ concurrency/version check
→ execute transaction
```

Important SquiFlow refinements:

1. Scope the lookup by authoritative tenant context where practical **before** exposing the resource to fine-grained authorization. A request for another tenant's object should not become a cross-tenant existence oracle.
2. Use semantic requirements for material business actions, for example:
   - `ApproveQuoteRequirement`;
   - `RefundPaymentRequirement`;
   - `AdjustInventoryRequirement`;
   - `ManageRolesRequirement`.
3. CRUD `OperationAuthorizationRequirement` is fine for genuinely CRUD-like resources, but `Update` is not an adequate name for every business permission.
4. Authorization handlers should be deterministic/side-effect-free checks over the actor, scope, resource and current permission state. Side effects happen only after authorization succeeds.
5. Create operations do not yet have the final resource. Authorize against the relevant parent/scope/command context rather than manufacturing a fake entity.
6. Queries/read models are authorization-sensitive too. Do not secure only commands.

---

## Combined architecture after all four sources

```text
OIDC authentication
→ SquiFlow tenant/platform context
→ endpoint/function policy
→ tenant-scoped resource resolution
→ ASP.NET Core resource/action authorization
→ current SquiFlow permission revision
→ canonical state/workflow/domain guards
→ optimistic/transactional concurrency
→ commit
→ audit + outbox/invalidation
```

For native Workstation:

```text
System browser OIDC + PKCE
→ SquiFlow account (issuer + subject)
→ tenant membership/device context
→ effective permission snapshot + AuthorizationRevision
→ local UX/offline-permitted work
→ sync
→ server repeats authoritative authorization
```

For Web role changes:

```text
Owner Web Settings
→ roles.manage endpoint policy
→ resource/delegation authorization
→ update role/grants
→ increment TenantAuthorizationRevision
→ audit + outbox/invalidation in same transaction
→ new effective permission snapshot
```

## Net result

These sources strengthen SquiFlow's existing architecture without justifying a new authorization service, another database, a relationship graph, or more runtime processes. The implementation target remains a modular-monolith authorization module using ASP.NET Core primitives, OIDC for identity, explicit API security gates, and a simple versioned permission model that can evolve only when actual relationship complexity or scale requires it.
