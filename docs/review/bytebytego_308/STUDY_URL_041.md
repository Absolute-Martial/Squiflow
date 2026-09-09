# URL 041 — How Do We Design a Secure System?

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: a comparison, checklist, pattern catalog, popularity claim, maturity ladder, or source diagram never selects SquiFlow architecture by itself. The review must first identify what SquiFlow is actually doing at the corresponding boundary, why that mechanism exists, what authority it owns, what it costs, and what evidence would justify changing it.

## A. Identification

- **URL occurrence:** `041`
- **PDF page:** `285`
- **Source URL:** `https://blog.bytebytego.com/p/ep108-how-do-we-design-a-secure-system`
- **Source access:** public newsletter section accessible.
- **Related supplied visual:** archive page 65, `12 Tips for API Security`.
- **Visual inspected:** PDF page `285` at full size.

## B. Core concept

### SOURCE

The accessible newsletter says secure-system design should be a default architectural discipline rather than an afterthought. Its checklist includes authentication, authorization, encryption, vulnerability management, audit/compliance, network security, terminal security, emergency response, container security, API security, third-party vendor management, and disaster recovery. The page also associates the topic with a related API-security visual.

### INFERENCE

The source is most useful as a reminder that security spans architecture and operations. It is not a threat model and it does not tell SquiFlow which products or controls should be deployed at every boundary.

### EXTERNAL KNOWLEDGE / CAVEAT

Security checklists are necessary but insufficient. Controls should be derived from assets, actors, trust boundaries, attack paths, business impact, and recovery obligations. The related visual's `Use OAuth2`, `Use API Gateway`, WebAuthn, allow-listing, and API-key guidance are examples, not universal requirements. OAuth 2.0 is primarily an authorization framework; SquiFlow's human authentication remains OIDC through ZITADEL, with MFA/WebAuthn capability owned by the identity provider rather than a custom second identity stack.

## C. Important concepts

- threat model and trust-boundary inventory;
- authentication versus authorization;
- transport/data-at-rest protection and key lifecycle;
- least privilege and tenant isolation;
- secure input/output/file/template handling;
- dependency and software-supply-chain security;
- third-party/provider risk and failure;
- audit evidence versus lossy telemetry;
- incident response and break-glass recovery;
- backup/restore and disaster recovery;
- secure defaults and hostile verification;

## D. Diagram / visual explanation

The supplied visual groups twelve API-security tips: HTTPS, OAuth2, WebAuthn, leveled API keys, authorization, rate limiting, API versioning, allow-listing, OWASP API risks, API gateway, error handling, and input validation. For SquiFlow these boxes map to different owners. TLS belongs at edge/transport; OIDC identity belongs to ZITADEL; application authorization belongs to TenantContext + OpenFGA + domain rules; rate/admission controls protect finite resources; versioning protects compatibility; safe errors/input controls belong to the API pipeline. One box cannot substitute for the others.

## E. How it works — step by step

1. Identify assets and trust boundaries: tenant/business data, payment/stock authority, identity sessions, provider credentials, admin control plane, Workstation local durable state.
2. Authenticate the principal through the appropriate identity mechanism.
3. Derive authoritative SquiFlow tenant/platform context; do not trust request-supplied tenant identity.
4. Apply function/resource/relationship authorization and current domain/workflow rules.
5. Validate input, field exposure, resource budgets, file/template/URL behavior, and concurrency/idempotency.
6. Protect external transport and secrets; isolate public edge, Core API, Admin API, provider and recovery paths.
7. Record authoritative audit where required and bounded observability for diagnosis.
8. Fail closed for security-critical dependencies and preserve a private, least-privilege recovery path.
9. Verify with hostile cross-tenant/security tests and restore/incident exercises rather than relying on scanner status.

## F. Why it matters

SquiFlow handles multi-tenant business data, long-offline Workstations, administrative control, external providers, files/templates, and future money/stock workflows. The dominant security risk is not absence of one security product; it is a gap between layers where a valid credential, route, tenant ID, provider response, or cached result is accidentally treated as full authority.

## G. Trade-offs / limitations

Layered controls reduce single-control bypass risk but increase design, test and operating burden. Stronger isolation can cost latency and complexity; aggressive security middleware can break legitimate workflows; overly broad logging can leak sensitive data. The goal is explicit risk ownership, not maximal controls everywhere.

## H. Alternatives / comparisons — fit, not winner/loser

```text
identity provider controls (OIDC/MFA/WebAuthn)
    -> establish human identity/session

TenantContext + OpenFGA + domain rules
    -> application authority

edge/WAF/rate/size policy
    -> exposure and abuse/resource protection

DB constraints/RLS/least privilege
    -> persistence defense in depth

scanners/SAST/DAST/dependency checks
    -> detection evidence

manual threat review + hostile journey tests + restore/incident drills
    -> prove end-to-end behavior
```
These mechanisms are complementary. A WAF, gateway, scanner, MFA factor, or database policy does not globally replace the others.

## I. Real implementation considerations

Every adoption/change is required to state its owner/authority, failure behavior, recovery path, implementation evidence and operating burden. A source list is not implementation evidence.

### Implications for the Current Implementation

- **KEEP:** current responsibility map: ZITADEL identity, OpenFGA relationship/permission decisions, ASP.NET integration, authoritative TenantContext/domain checks, DB constraints/tenant defense, edge TLS/exposure controls.
- **KEEP:** separate Core API and Admin API security/availability planes and private break-glass recovery rather than one privileged route set.
- **KEEP:** application security baseline for XSS/CSRF/injection/SSRF/files/secrets/cache/provider responses and hostile verification.
- **IMPROVE NOW (implementation gate):** once source exists, prove endpoint classification, cross-tenant negative paths, cookie/CSRF behavior, SQL/identifier injection, XSS/template/file/SSRF controls, security-provider outage, secret redaction and restore/incident paths.
- **NEEDS MEASUREMENT:** WAF, extra edge controls, file scanning/sandbox isolation, SIEM-style capability, container hardening tooling and other controls must be tied to actual exposure/risk/deployment.
- **AVOID:** choosing security products from a checklist or claiming scanner/pen-test/gateway/token success proves secure business authorization.

**What are we actually doing and why?** We are using layered identity, authorization, tenant/domain, persistence, edge and recovery controls because no single layer proves all facts needed for a multi-tenant business operation. We would change or strengthen a layer when a concrete threat model, incident, compliance/customer requirement, or hostile test shows the current boundary is insufficient.

**Implementation-evidence status:** the repository is still documentation/planning only at the root (no application source tree committed). These are accepted design requirements and future verification gates, not claims that the controls/behavior already exist in running code.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. Which source categories are preventive controls, detective controls, and recovery controls?
2. Why are authentication and authorization separate?
3. What does encryption protect that authorization does not?

**Critical reasoning**

1. What are SquiFlow's highest-impact assets and trust boundaries today?
2. Which SquiFlow security decisions are documented but not yet implementation-proven?
3. Why must a valid ZITADEL session still pass TenantContext, OpenFGA and domain-state checks?
4. Which private recovery capability is needed if the public edge or identity provider is unavailable?
5. Where can provider responses/files/templates become an attack path even with TLS?

**Trade-off**

1. When would a WAF materially improve SquiFlow and when would it merely add another operator surface?
2. When should file scanning or sandbox conversion be introduced?
3. What security evidence belongs in audit versus ordinary telemetry?

**Failure / edge**

1. OpenFGA times out during a sensitive command. What happens?
2. A trusted custom domain routes to the wrong tenant context. What prevents cross-tenant access?
3. An operator must recover the rack while public identity is unavailable. What is allowed and how is it evidenced?
4. A scanner is green but an object ID from Tenant B is accepted by Tenant A. Which control failed?

**Implementation**

1. What executable metadata proves every privileged endpoint is classified?
2. How are secrets kept out of builds/logs/dumps?
3. Which hostile tests exercise files, templates, SSRF, injection and cross-tenant access?
4. How is security-provider outage distinguished from application defect?

**System design interview**

1. Design the security authority chain for a tenant refund and a platform worker-control operation.
2. Design recovery when edge/DNS/TLS/ZITADEL/OpenFGA failures overlap.

**Challenge**

1. A request arrives over valid TLS with a valid OIDC identity, a guessed Tenant B order ID, stale cached permission data, and a syntactically valid refund payload. Explain every independent decision that must still occur before any effect commits.
