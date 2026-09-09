# URL 052 — JWT 101: Key to Stateless Authentication

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: comparisons, best-practice lists, pattern catalogs, protocol matrices, popularity claims, maturity ladders, and source diagrams do not select SquiFlow architecture by themselves. The review first asks what SquiFlow is actually doing at the corresponding boundary, what concrete requirement/invariant it solves, why the current mechanism has the needed property, what authority it owns, what it costs, where another mechanism could fit better, and what evidence would justify or falsify a change.

## A. Identification

- **URL occurrence:** `052`
- **PDF page:** `296`
- **Source URL:** `https://blog.bytebytego.com/p/ep149-jwt-101-key-to-stateless-authentication`
- **Source access:** public newsletter section accessible; exact archive-title overlap with archive 010 is still reviewed independently.
- **Related supplied visual:** archive page 51, closely matching JWT 101 visual.
- **Visual inspected:** PDF page `296` at full size.

## B. Core concept

### SOURCE

The newsletter describes JSON Web Tokens as an open standard for securely transmitting information and says they are widely used for authentication and authorization. It explains the three JWT parts—header, payload, and signature—and distinguishes symmetric signing with one shared secret from asymmetric signing with a private signing key and public verification key.

### INFERENCE

The valuable lesson for SquiFlow is that a token is a signed claim container whose validation and key ownership matter. It does not follow that SquiFlow should create its own token issuer, put durable business permissions into JWT claims, or choose JWT merely because “stateless authentication” sounds scalable.

### EXTERNAL KNOWLEDGE / CAVEAT

JWT is a token format, not a complete authentication/session/authorization architecture. Signed JWT payloads are normally readable unless separately encrypted, so signing is not confidentiality. Token validation must include trusted issuer, audience, algorithm/key, expiry and protocol transaction checks. Authorization data can become stale before token expiry. For SquiFlow’s native Workstation, the accepted login direction is OIDC Authorization Code + PKCE through ZITADEL, not a custom client-side JWT login flow.

## C. Important concepts

- header, payload/claims, signature;
- symmetric versus asymmetric signing and key distribution;
- signature integrity versus confidentiality;
- issuer/audience/expiry validation;
- token freshness versus authorization freshness;
- OIDC/OAuth protocol context around tokens;
- session/revocation behavior;
- stable external identity as issuer + subject;
- JWT claims not current OpenFGA business authority;

## D. Diagram / visual explanation

The visual shows JWT structure and a bearer-token flow in which a server signs a token and later validates its signature. That helps explain token mechanics, but SquiFlow deliberately places token issuance and interactive authentication behind ZITADEL/OIDC. The server consumes validated identity evidence, derives SquiFlow membership/TenantContext, then runs current OpenFGA and domain rules. The diagram therefore explains one artifact inside the authentication boundary, not the whole authority chain.

## E. How it works — step by step

1. User authenticates with ZITADEL using the appropriate OIDC flow.
2. ASP.NET/OIDC integration validates trusted issuer, audience, signature/key, expiry and transaction state.
3. SquiFlow resolves stable account identity from issuer + subject.
4. SquiFlow derives current TenantMembership/TenantContext and device/session context.
5. OpenFGA and domain/resource/state authorization run using current server authority.
6. Session/token refresh, revocation, logout and step-up follow the selected ZITADEL/session model.
7. No business operation trusts a token claim as the only source of current tenant/resource permission.

## F. Why it matters

SquiFlow needs standards-based identity evidence without embedding credentials or implementing password/MFA/token infrastructure itself. JWT may appear within the trusted OIDC/OAuth implementation, but the architectural reason for choosing ZITADEL is centralized identity, MFA/SSO/recovery and standards integration—not JWT popularity.

## G. Trade-offs / limitations

Self-contained signed tokens reduce some per-request session lookups and allow distributed verification, but revocation/freshness, key rotation, claim minimization, browser/native storage, issuer/audience mistakes, and leakage of readable claims become operational concerns. Opaque/server-managed session state can be simpler for some browser topologies.

## H. Alternatives / comparisons — fit, not winner/loser

```text
JWT access/id tokens inside OIDC/OAuth
    -> standards-based identity/token exchange where provider uses them

server-managed Web session
    -> browser application session lifecycle where practical

opaque/reference token
    -> centralized introspection/revocation trade-off

API key / OAuth client credentials / mTLS
    -> future machine principals depending on lifecycle and assurance

OpenFGA + TenantContext + domain rules
    -> current application authorization regardless of token format
```

These are different layers and can coexist.

## I. Real implementation considerations

The implementation decision is not complete until the exact boundary, authority, failure modes, recovery, security/tenant behavior, compatibility, observability, resource cost, small-team operating burden, and adoption/falsification evidence are explicit. The current repository remains documentation/planning-only at the root rather than an application source tree, so architecture statements below are requirements and future proof gates, not claims that code already implements them.

### Implications for the Current Implementation

- **KEEP:** ZITADEL/OIDC as the identity platform and Workstation Authorization Code + PKCE; no reusable native client secret.
- **KEEP:** stable external identity `(issuer, subject)` and current TenantContext/OpenFGA/domain authorization after authentication.
- **KEEP:** token/session format as an identity-platform concern rather than a custom SquiFlow JWT subsystem.
- **IMPROVE NOW:** implementation proof for issuer/audience/algorithm/key/expiry validation, session revocation, cookie/native storage, and stale-permission negative tests when code exists.
- **LATER / SCALE TRIGGER:** shared durable Web session/revocation state only if the chosen Web topology becomes multi-node and actually requires it.
- **AVOID:** putting long-lived SquiFlow business permission truth in JWT claims or assuming a valid signature means an action is authorized.
- **AVOID:** describing signed JWTs as encrypted confidential containers.

**What are we actually doing and why?** SquiFlow uses ZITADEL/OIDC because centralized human identity, MFA/SSO, recovery, and standards-based browser/native login solve the actual identity requirement. JWT is an implementation artifact that may be used by that provider; it is not the reason for the architecture and it does not replace OpenFGA or TenantContext.

**What would falsify/change this?** If the selected ZITADEL/session topology uses opaque tokens or server-managed sessions for a surface, SquiFlow does not need JWT there. If a future machine integration has simpler credential lifecycle needs, an API key or client-credential/mTLS design may be more appropriate without changing human identity architecture.

**Implementation-evidence status:** documented/accepted architecture is not the same as verified implementation. The relevant future slice must prove the behavior in source, tests, deployment, and recovery evidence.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. What security property does a JWT signature provide, and what does it not provide?
2. How do symmetric and asymmetric signatures change key distribution?
3. Why is JWT a token format rather than a complete authentication protocol?

**Critical reasoning**

1. Why does SquiFlow identify an account by issuer + subject rather than email?
2. Why can a token be valid while the user is no longer authorized to refund an order?
3. Which token claims are identity evidence and which permissions must be re-evaluated through OpenFGA/domain rules?
4. Why does the Workstation use Authorization Code + PKCE instead of embedding a client secret?
5. What would go wrong if one broad JWT claim were treated as TenantContext?

**Trade-off**

1. When is a server-managed Web session preferable to relying on browser-held bearer tokens?
2. What is gained and lost with self-contained validation versus reference-token introspection?
3. When could an API key or mTLS be a better machine credential than an end-user JWT?

**Failure / edge**

1. Signing key rotates while a Workstation reconnects with an older token. What should happen?
2. Token is valid but TenantMembership was suspended seconds ago. What decides the command?
3. An ID token is replayed to the wrong audience. Which validation must reject it?
4. OpenFGA is unavailable after authentication succeeds. What is the safe behavior?

**Implementation**

1. Which OIDC validation properties must be configured in ASP.NET?
2. Where are refresh/session credentials stored for Web versus Workstation?
3. How is logout/revocation distinguished from deleting durable local Workstation state?
4. What tests prove business authorization is not sourced from stale token claims?

**System design interview**

1. Design the identity-to-authorization chain for a Workstation user accessing two tenants.
2. Compare server-managed browser sessions with self-contained bearer-token sessions for SquiFlow without declaring a universal winner.

**Challenge**

1. A perfectly signed, unexpired token says the user was an Owner when issued, but the user was removed from the tenant afterward. Explain every check that prevents stale token data from granting current authority.
