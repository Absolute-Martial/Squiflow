# ByteByteGo Exhaustive Sequential Study — URL Entry 007

# URL 007 — Top Authentication Techniques to Build Secure Applications

## A. Identification

- **URL entry:** `007`
- **PDF page:** `251`
- **Source URL:** `https://blog.bytebytego.com/p/top-authentication-techniques-to`
- **Public source access:** paid article; public preview inspected.
- **Related visual:** archive page `403`, five REST API authentication methods.
- **Visual inspection:** PDF page `251` inspected in full.

## B. Core concept

### SOURCE

The preview frames authentication as both security and user-experience design. It explicitly names session hijacking, token theft and replay attacks, and says authentication choices must balance security, scalability and usability. It says multiple techniques should be evaluated by advantages/disadvantages rather than assuming one universal method.

The related visual shows Basic Authentication, Session Authentication, Token Authentication, OAuth-based Authentication and API-Key Authentication.

### INFERENCE

Authentication mechanism should be chosen by principal type, client capability, credential lifecycle, threat model and UX — not by a generic ranking.

### EXTERNAL KNOWLEDGE / CAVEAT

The visual’s categories overlap: sessions commonly use cookies, token authentication may use OAuth-issued tokens or other credentials, and OAuth is primarily an authorization framework while OIDC commonly supplies authentication. “JWT” is a token format, not a complete authentication architecture.

## C. Important concepts

- human versus machine principal;
- public native client versus confidential server client;
- browser session;
- OIDC/OAuth Authorization Code + PKCE;
- MFA/passkey/SSO/step-up;
- API key lifecycle;
- service credentials/mTLS/client credentials;
- session fixation/hijacking;
- token theft/replay;
- revocation/rotation;
- identity versus authorization;
- device enrollment and tenant membership.

## D. Diagram / visual explanation

The visual is useful as a mechanism catalog. SquiFlow’s mapping is deliberately mixed:

```text
human Web / Workstation
    -> ZITADEL OIDC/OAuth

Web application session
    -> hardened server-managed/cookie-backed session where selected

native Workstation
    -> Authorization Code + PKCE, system browser

future simple machine integration
    -> API key may fit

future high-assurance machine identity
    -> OAuth client credentials / mTLS / signed request may fit
```

After authentication, SquiFlow authorization still runs.

## E. How it works — step by step

For Workstation:

1. generate state + PKCE verifier/challenge;
2. open system browser;
3. authenticate through ZITADEL/MFA as configured;
4. receive authorization response to registered callback;
5. validate transaction and exchange code + verifier;
6. validate issuer/audience/signature/expiry;
7. map stable `(issuer, subject)` to SquiFlow account/membership/device context;
8. resolve current TenantContext/OpenFGA/domain authorization on material server commands.

## F. Why it matters

This article supports the current identity-platform approach: SquiFlow should not build its own password/OTP/passkey/token zoo. It should integrate a standards-based identity provider and select additional machine-credential mechanisms only when a concrete integration needs them.

## G. Trade-offs / limitations

- server sessions improve revocation/control but require server/shared state;
- bearer tokens scale/distribute well but theft/replay/freshness matter;
- API keys are simple but coarse unless carefully scoped/rotated;
- Basic Auth is simple but credential exposure/reuse risk is high and always needs TLS;
- MFA/passkeys improve security but have enrollment/recovery/UX concerns;
- federation/SSO reduces password sprawl but adds IdP dependency and lifecycle complexity.

## H. Alternatives / comparisons — fit, not winner/loser

No global `session vs token vs OAuth vs API key` winner exists. Human identity, browser session, native client, service client and partner integration are different boundaries and may use different mechanisms together.

## I. Real implementation considerations / SquiFlow implications

- **KEEP:** ZITADEL is current identity/authentication platform choice.
- **KEEP:** Workstation Authorization Code + PKCE with system browser.
- **KEEP:** server-managed Web session direction where practical.
- **KEEP:** authentication never replaces TenantContext/OpenFGA/domain authorization.
- **LATER / SCALE TRIGGER:** API keys/client credentials/mTLS may be selected for machine integrations when a concrete principal/lifecycle/risk requires them.
- **AVOID:** custom password/JWT/PASETO/MFA subsystem without a demonstrated provider gap.
- Implementation evidence still required: callback security, session fixation/revocation, token replay/theft controls and device lifecycle must be proven in code/tests.

**What are we doing and why?** We delegate human authentication techniques to ZITADEL because identity-factor security/recovery/federation is a specialized platform concern, while SquiFlow owns membership/device/session binding and current business authorization.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. Which threats does the public preview name?
2. What three priorities does it say authentication must balance?
3. Which five mechanisms appear in the related visual?

**Critical reasoning questions**
1. Why is Workstation a public client that should not contain a reusable secret?
2. What would an API key prove in a future partner integration, and what would it not prove?
3. Why should SquiFlow avoid implementing WebAuthn directly if ZITADEL can own it?
4. How does current authorization remain fresh after token issuance?
5. What recovery path exists when a user loses their second factor without allowing support to grant permanent tenant authority?

**Trade-off questions**
1. When is a server session better than a bearer token for the Web?
2. When can mTLS be worth the certificate lifecycle cost?
3. When is an API key appropriately simple rather than insecurely coarse?

**Failure / edge-case questions**
1. Token is stolen and replayed from another device. What controls exist?
2. Identity provider is reachable but OpenFGA is unavailable. What happens?
3. Workstation auth callback is intercepted/confused. What PKCE/state checks protect it?
4. Device revoked while user account remains valid. Which access should fail?

**Implementation questions**
1. What exact issuer/audience/nonce/state/PKCE validation is tested?
2. How are refresh/session capabilities revoked/rotated?
3. How are machine credentials scoped, stored and audited if introduced?
4. What telemetry distinguishes identity failure from membership/authorization failure?

**System design interview questions**
1. Design human and machine authentication for SquiFlow without one universal credential type.
2. Explain why OIDC authentication and OpenFGA authorization are complementary.

**Challenge**
A large customer wants SSO for staff, passkeys for owners, native Workstation login, and a headless accounting integration. Map each to the appropriate identity/credential mechanism while keeping one coherent SquiFlow authorization model.

---
