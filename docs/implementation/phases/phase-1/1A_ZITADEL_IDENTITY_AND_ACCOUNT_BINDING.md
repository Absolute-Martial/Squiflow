# Phase 1A — ZITADEL Identity and Account Binding

## Required foundation

Implement the selected ZITADEL topology for the current tenant Web and Workstation surfaces:

- OIDC issuer/audience validation;
- server-side Web login/session establishment;
- Workstation Authorization Code + PKCE S256 through the system browser;
- redirect/state/nonce/transaction validation as appropriate;
- logout/session-expiry behavior;
- SquiFlow account mapping by stable `(issuer, subject)` rather than email;
- no reusable native Workstation client secret.

## Existing components continue

Customers/Orders/etc. may add authenticated operations during 1A. Workstation/Web can add login/logout/account UX. CoreApi can add authentication middleware and explicit request context. Guard remains outside identity authority.

## Data ownership

Keep separate:

```text
ZITADEL identity
SquiFlow account mapping
TenantMembership
Device / installation identity
```

Email/display name are attributes, not durable account identity.

## Failure/degraded behavior

Test wrong issuer/audience, expired/replayed transaction, PKCE mismatch, redirect tampering, provider outage, account attribute changes, and session expiry. Provider outage must not become a local password/JWT bypass.

## Observability/security

Record stable safe failure codes and correlation without tokens/cookies/credentials. Secrets belong in deployment configuration, never client artifacts.

## Exit gate

Web and Workstation authenticate through the selected production-shaped path, account identity is stable across profile changes, and no business capability depends on a temporary identity shortcut.