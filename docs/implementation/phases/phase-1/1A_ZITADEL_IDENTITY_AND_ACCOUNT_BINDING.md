# Phase 1A — ZITADEL Identity and Account Binding

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Production intent

After 1A passes, a real user can authenticate through each declared Web/Workstation flow and SquiFlow can bind that login to a stable account identity without accepting a forged/misbound OAuth/OIDC transaction or falling back to a local identity bypass when the provider is unavailable.

## Standards/contract basis

Do not use `production-shaped` as a substitute for a concrete security contract.

The implemented topology must trace its claims to the standards and provider behavior it actually uses, including as applicable:

- OAuth 2.0 Security Best Current Practice — RFC 9700;
- OAuth 2.0 for Native Apps — RFC 8252;
- PKCE — RFC 7636;
- OpenID Connect Core;
- ZITADEL's current supported OIDC/OAuth configuration for the selected applications.

RFC 9449 is DPoP. It may become relevant if sender-constrained tokens are explicitly selected, but it is not the generic browser/native-app contract for this phase.

## Required foundation

Implement the selected ZITADEL topology for the current tenant Web and Workstation surfaces:

- exact expected issuer and audience/client validation for accepted tokens/responses;
- server-side Web login/session establishment for the selected Web topology;
- Workstation Authorization Code + PKCE S256 through the system browser;
- transaction-specific state and PKCE verifier/challenge binding;
- OIDC nonce validation where the selected flow relies on it;
- approved redirect URI behavior consistent with registered/provider semantics; do not implement permissive prefix/wildcard redirect matching in SquiFlow code;
- authorization response/code handling that rejects replay/mix-up/misbound transaction conditions relevant to the selected flow;
- logout/session-expiry behavior;
- SquiFlow account mapping by stable `(issuer, subject)` rather than email;
- no reusable native Workstation client secret;
- safe metadata/key/configuration refresh behavior required by the selected library/provider integration.

Do not implement protocol cryptography/validation manually when the selected standards-compliant .NET/provider library already owns it; verify the library/configuration actually enforces the property SquiFlow depends on.

## Existing components continue

Customers/Orders/etc. may add authenticated operations during 1A only under the Phase-1 transitional security contract. Workstation/Web can add login/logout/account UX. CoreApi can add authentication middleware and explicit request context. Guard remains outside identity authority.

Authentication alone is not tenant/business authorization.

## Data ownership

Keep separate:

```text
ZITADEL identity
SquiFlow account mapping
TenantMembership
Device / installation identity
```

Email/display name are attributes, not durable account identity.

## Threat model

For each declared flow, cover the relevant structured threat categories rather than relying only on happy-path provider examples:

- spoofed/wrong issuer or authorization server;
- wrong audience/client binding;
- state/transaction substitution;
- authorization-code replay/injection/mis-binding;
- PKCE mismatch or reused verifier/challenge;
- redirect manipulation/open-redirect confusion;
- OIDC nonce replay/mismatch where used;
- token/cookie/credential disclosure through logs/client artifacts;
- provider outage/degraded metadata/key retrieval;
- account profile/email changes attempting to change account identity.

## Evidence requirements

### Permanent `PER_MR` checks

As applicable to the selected flow, automated tests must prove:

- wrong issuer/audience rejected;
- transaction/state/nonce mismatch rejected;
- PKCE mismatch rejected;
- callback/redirect tampering rejected;
- replayed/expired transaction rejected;
- account mapping remains `(issuer, subject)` across mutable-profile changes;
- no reusable Workstation client secret appears in client artifacts/configuration;
- safe logs/errors contain no raw token/cookie/credential material;
- provider outage cannot activate a local password/JWT bypass.

### Real provider integration

An isolated real ZITADEL environment/configuration must be exercised on a recurring and pre-release cadence sufficient to prove the selected client/application/provider configuration, callback registration, metadata/key discovery, and logout/session behavior that mocks cannot establish.

If provider tests cannot run on every MR, that does not remove the permanent claim: record the recurring cadence and last/current evidence when gate or release sign-off needs it.

## Observability/security

Record stable safe failure codes and correlation without tokens/cookies/credentials. Secrets belong in deployment configuration, never client artifacts.

## Deferred security mechanisms

If sender-constrained tokens such as DPoP/mTLS are not selected, record that they are `NOT_INTRODUCED`, what bearer-token/session protections apply in their absence, and why that posture is accepted for the current topology. Do not imply DPoP merely because RFC 9449 exists.

## Exit gate

1A passes only when:

- every declared login flow is `PRODUCTION_HONEST` for the concrete standard/provider properties above;
- the named hostile/protocol evidence passes;
- real-provider evidence exists for claims mocks cannot prove;
- the tests/configuration checks remain active as regression guards;
- account identity is stable across profile changes;
- no business capability depends on a temporary identity shortcut;
- `BLOCKED = none` for the authentication scope.
