# URL 043 — API Security Best Practices

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: a comparison, checklist, pattern catalog, popularity claim, maturity ladder, or source diagram never selects SquiFlow architecture by itself. The review must first identify what SquiFlow is actually doing at the corresponding boundary, why that mechanism exists, what authority it owns, what it costs, and what evidence would justify changing it.

## A. Identification

- **URL occurrence:** `043`
- **PDF page:** `287`
- **Source URL:** `https://blog.bytebytego.com/p/api-security-best-practices`
- **Source access:** paid article with public preview/Authentication opening; no bypass.
- **Related supplied visual:** archive page 65, `12 Tips for API Security`.
- **Visual inspected:** PDF page `287` at full size.

## B. Core concept

### SOURCE

The public preview frames APIs as a broad attack surface exposed to injection, scripting and denial-of-service threats. It says scanners and occasional penetration testing alone do not provide comprehensive API security and introduces layered measures including authentication, authorization, secure communication and rate limiting. The visible Authentication section says the mechanism should be chosen according to use case, security requirements and client compatibility.

### INFERENCE

The source supports mechanism selection by client/principal context rather than one universal authentication method. It also reinforces that API security is an end-to-end pipeline and operating discipline rather than a single gateway/scanner feature.

### EXTERNAL KNOWLEDGE / CAVEAT

The source sentence that authentication ensures only authorized users/applications can access resources loosely mixes authentication and authorization; authentication establishes principal identity/credential validity, while resource permission remains a separate authorization decision. OAuth2, API keys, WebAuthn, gateways, rate limits and allow-lists solve different problems. API security also includes object/function/property authorization, business-flow abuse, SSRF, unsafe API consumption, inventory/deprecation and resource consumption beyond what the preview exposes.

## C. Important concepts

- principal-appropriate authentication;
- object/function/property authorization;
- TenantContext and anti-spoofing;
- TLS and credential handling;
- input/output contracts and mass-assignment defense;
- rate/admission/resource bounds;
- business-flow abuse;
- API inventory/version retirement;
- safe third-party/API consumption;
- Problem Details/error disclosure;
- hostile cross-tenant testing;

## D. Diagram / visual explanation

The related visual's twelve tips are complementary layers. SquiFlow maps them to concrete owners rather than one `API security` middleware: edge handles TLS/coarse limits, ZITADEL handles identity, Core/Admin API derive authoritative context, OpenFGA/resource handlers decide relationship permission, domain code decides workflow/state, DTO/validation controls fields and inputs, and DB constraints/RLS provide defense in depth.

## E. How it works — step by step

1. Classify endpoint owner/audience and authentication mechanism.
2. Validate credential/session and derive authoritative tenant/platform context.
3. Apply coarse endpoint/function policy.
4. Load resource under tenant scope and perform OpenFGA/resource/field authorization.
5. Validate request structure, writable fields, business/domain state, concurrency and idempotency.
6. Enforce payload/page/batch/rate/resource/provider budgets.
7. Execute authoritative transaction and return safe structured result/error.
8. Record audit/trace evidence without secrets/PII leakage.
9. Test negative object/function/property and cross-tenant cases through the real ASP.NET pipeline.

## F. Why it matters

SquiFlow's API is the server authority for Workstation sync, Web business operations and future platform control. A valid token alone is especially dangerous in a multi-tenant system because it can be incorrectly treated as permission to any resource reachable by ID.

## G. Trade-offs / limitations

More API controls can increase latency and implementation complexity; insufficient controls create cross-tenant/business abuse. Fine-grained authorization can require resource loading; aggressive rate limits can block legitimate reconnect bursts. The correct design separates security semantics from capacity/fairness and tests both.

## H. Alternatives / comparisons — fit, not winner/loser

```text
OIDC session/token
    -> authenticate human/client identity

API key / OAuth client credentials / mTLS
    -> possible machine-integration credentials by lifecycle/risk

OpenFGA/resource policy
    -> relationship/function/resource authorization

rate limiter
    -> temporary admission/fairness/abuse control

gateway/WAF
    -> coarse edge policy

backend domain/DB checks
    -> authoritative business/tenant correctness
```
These are not interchangeable and can coexist.

## I. Real implementation considerations

Every adoption/change is required to state its owner/authority, failure behavior, recovery path, implementation evidence and operating burden. A source list is not implementation evidence.

### Implications for the Current Implementation

- **KEEP:** ZITADEL/OIDC for human identity, Workstation PKCE/public-client behavior, and separate server-side authorization chain.
- **KEEP:** explicit request/response DTOs, object/function/field authorization, tenant-scoped lookup, rate/resource bounds and safe Problem Details.
- **KEEP:** Admin API remains a separate privileged backend rather than privileged Core routes.
- **IMPROVE NOW (implementation gate):** generated endpoint inventory, cross-tenant/object/property/function hostile tests, injection/SSRF/CSRF/XSS and provider-response tests once endpoints exist.
- **LATER / SCALE TRIGGER:** stronger gateway/WAF or machine-auth variants only where an actual external integration/exposure requires them.
- **AVOID:** `valid token => authorized`, scanner-only security, or gateway policy as a substitute for backend resource/domain authorization.

**What are we actually doing and why?** We are designing API security as a layered backend authority chain because SquiFlow has controlled human/native clients, multi-tenant resource permissions and domain-state rules that cannot be proven by one credential. We would introduce a different authentication/gateway mechanism only for a concrete principal/exposure whose lifecycle and threat model require it.

**Implementation-evidence status:** the repository is still documentation/planning only at the root (no application source tree committed). These are accepted design requirements and future verification gates, not claims that the controls/behavior already exist in running code.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. What is the difference between authentication, authorization and admission control?
2. Why is HTTPS necessary but insufficient?
3. Why must request and response fields be explicitly controlled?

**Critical reasoning**

1. Which SquiFlow endpoint classes need different authentication/audience policies?
2. Why is a request-body tenant ID never authoritative?
3. Where should OpenFGA end and domain/workflow checks begin?
4. How can an API be correctly authenticated yet vulnerable to object-level authorization failure?
5. What business flows need abuse controls beyond RPS?

**Trade-off**

1. When should an external machine integration use an API key versus OAuth client credentials or mTLS?
2. When is 404 non-disclosure preferable to 403?
3. How should reconnect bursts be rate-limited without damaging legitimate sync?

**Failure / edge**

1. OpenFGA is unavailable after identity succeeds. What happens?
2. A provider returns malformed oversized JSON over valid TLS. What happens?
3. A user changes an over-posted `tenant_id` or `approved=true` field. Which layer rejects it?
4. A token is valid but permission was revoked seconds ago. Which source wins?

**Implementation**

1. What endpoint metadata must CI inventory?
2. Which test proves object-level authorization for every client-supplied resource ID?
3. Where are input/body/page/batch limits enforced?
4. Which errors are safe to expose?

**System design interview**

1. Design the security pipeline for `ApproveQuote` and for a provider webhook.
2. Design an API for external machine integration without weakening tenant/domain authorization.

**Challenge**

1. An authenticated tenant admin requests another tenant's file using a guessed object key through an endpoint that passes gateway rate limits and input validation. Explain every independent control that must still prevent disclosure.
