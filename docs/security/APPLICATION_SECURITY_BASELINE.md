# Application Security Baseline

**Version:** v0.0.15

This document owns SquiFlow security requirements that sit outside the detailed identity and authorization documents. It consolidates browser, API, data-access, file, outbound-integration, dependency, build, and conditional container controls without creating a second authentication or authorization system.

## 1. Responsibility map

```text
ZITADEL        identity, interactive authentication, MFA/SSO capability
OpenFGA        application roles, permissions, and resource relationships
ASP.NET Core   authentication/session integration and authorization policies
SquiFlow       tenant context, domain/workflow/state/field rules, idempotency/concurrency
Database       constraints, transactions, least-privilege access, tenant isolation defense
Edge/host      TLS, exposure, request-size/WAF/private-access controls where configured
```

Passing one layer never bypasses the others. A valid session, OpenFGA `allow`, firewall rule, trusted hostname, or successful input parse alone is not authority to read or change an arbitrary tenant resource.

## 2. Web/browser baseline

For cookie-backed or server-managed Web sessions:

- use `Secure`, `HttpOnly`, and deliberately selected `SameSite` behavior;
- protect state-changing requests against CSRF/forgery according to the selected ASP.NET/Blazor session topology;
- rotate/expire session material according to `IDENTITY_AND_SESSIONS.md`;
- never place reusable central DB credentials, provider secrets, or long-lived bearer credentials in browser storage;
- keep business truth in server-side/domain persistence, not only circuit/component memory.

For XSS and content handling:

- rely on framework output encoding by default;
- do not render user/customer/supplier/template text as raw HTML merely for formatting convenience;
- if a real journey permits rich content, use a narrow allow-list sanitizer and define whether the sanitized or original form is retained;
- define a Content Security Policy and related response headers for each Web surface during Phase 1/8 hardening;
- keep Tenant Web and Platform Admin Web session/cookie scopes deliberately separated, including custom-domain behavior;
- treat URLs, file names, SVG/HTML-like content, print templates, notification templates, and generated previews as untrusted input.

XSS defense must cover stored, reflected, and DOM/client-side rendering paths. A server-side validation pass is not proof that later template/client rendering is safe.

## 3. API input, output, and resource safety

Every endpoint uses an explicit request/response contract rather than binding arbitrary persistence/domain objects.

Required controls where applicable:

- allow-list writable and readable fields;
- validate structure, size, count, range, format, and business meaning;
- authorize function, tenant, resource/object, and sensitive field separately where needed;
- tenant-scope lookup before returning object existence/details according to the endpoint's disclosure policy;
- enforce request, page, batch, upload, decompression, CPU/memory, and provider-cost limits;
- return stable Problem Details/failure codes without stack traces, SQL, secrets, tokens, or provider-internal data;
- keep API inventory/OpenAPI output aligned with actual Core API versus Admin API route ownership;
- rate/admission control is layered with, not substituted for, authorization.

Mass assignment and excessive data exposure are prevented by contract/field allow-lists, not by hoping clients omit hidden fields.

## 4. SQL/data-access injection and concurrency

- Use parameterized commands, ORM parameter binding, or provider-safe query composition. Never concatenate untrusted values into SQL.
- Dynamic filter, sort, search, report, import, custom-field, and tenant-admin query paths use allow-listed identifiers/operators; SQL parameters cannot make an arbitrary identifier safe.
- Runtime database identities are least privilege and are not superuser/`BYPASSRLS` merely to make application code simpler.
- Application tenant scoping and authorization remain required even when PostgreSQL RLS is used.
- Avoid long transactions spanning user interaction or remote provider calls.
- Use expected versions/optimistic concurrency as the ordinary edit contract, database constraints for uniqueness, and locks/isolation selected for the actual invariant.
- Classify deadlock/serialization conflicts explicitly; when safe, retry the whole transaction with a bounded budget under the existing semantic-idempotency contract.

## 5. Outbound URLs, third parties, and SSRF

Any server-side URL fetch, webhook, callback, object/provider request, import link, or preview feature must define:

- allowed scheme and destination policy;
- DNS resolution/re-resolution and redirect handling;
- private/link-local/loopback/metadata-network restrictions;
- port and request-method restrictions;
- authentication/signature rules;
- timeout, response-size, decompression, redirect, and retry bounds;
- what response content is trusted, parsed, stored, rendered, or logged.

Do not let tenant/user URLs become an unrestricted server-side network client. Provider responses are untrusted inputs even when transport authentication succeeds.

## 6. Files, artwork, documents, and templates

Follow `FILES_AND_OBJECT_STORAGE.md` for ownership and storage. In addition:

- validate declared and detected type, size, and expected structure where the journey requires it;
- generate SquiFlow-owned object keys rather than trusting path/file names;
- prevent path traversal and executable placement;
- scan/quarantine content when the selected risk, file type, and available tooling justify it;
- render/convert untrusted documents/images in a bounded process or Worker path when parser risk/resource use justifies isolation;
- never allow preview/print failure to mutate already committed business truth;
- keep template variables allow-listed and output-channel encoded/sanitized;
- do not expose one tenant's object merely because its storage key is known.

## 7. Secrets, cryptography, and transport

- Production external traffic uses HTTPS/TLS and fails closed; there is no HTTP/certificate-validation bypass for recovery.
- Use platform/provider cryptographic implementations and documented key lifecycles rather than custom cryptography.
- Secrets are supplied through the selected secret/configuration mechanism, excluded from source/build logs/artifacts, least privilege, and rotatable.
- Workstation contains no reusable native client secret, central DB credential, or platform-management credential.
- Kaggle receives only locally encrypted opaque backup artifacts; backup encryption keys remain separately recoverable.
- Logs/traces/crash dumps do not contain access tokens, cookies, provider secrets, raw credentials, or unrestricted customer content.

## 8. Dependencies, build, and conditional container security

CI/release controls include, as practical for the phase:

- secret detection;
- dependency/vulnerability review with an explicit triage path;
- reproducible build inputs and immutable artifact checksum/provenance;
- signed/verified packages or artifacts where the selected distribution mechanism supports them;
- no production secret baked into source, desktop package, container layer, or build artifact.

If containers are selected for a deployment profile:

- use trusted, minimal, version/digest-pinned base images;
- run as non-root/least privilege unless a documented requirement prevents it;
- keep secrets outside the image;
- minimize writable filesystem/capabilities/ports and define health/resource limits;
- scan the final image and its dependencies, not only source manifests;
- promote the same verified image/artifact between environments rather than rebuilding different bytes.

These conditional controls do not preselect Docker, Kubernetes, or a container registry.

## 9. Caching and derived-data security

A cache is disposable, bounded, tenant-scoped, and non-authoritative.

- Cache keys include the authority/scope dimensions needed to prevent tenant or permission mixing.
- Sensitive authorization/payment/stock/credit results are not served as current truth from stale data.
- Invalidation/freshness and outage/bypass behavior are explicit.
- Cache contents and diagnostics follow the same PII/secret rules as their source data.
- Cache failure or cold start may reduce performance, but must not grant access, lose committed truth, or corrupt the authoritative record.

No Redis/Memcached/browser cache is required by this policy.

## 10. Failure and incident behavior

- ZITADEL/OpenFGA/edge/provider failure never converts into accidental allow.
- Security control failure is observable without logging sensitive material.
- Suspicious or high-impact actions keep authoritative audit evidence separate from lossy telemetry.
- Platform Admin and private break-glass paths remain separate from ordinary tenant authority.
- Recovery uses known-good configuration/artifacts and preserves evidence appropriate to the incident; emergency access is least privilege, time/need bounded where feasible, and not a hidden product API.

## 11. Verification gate

The relevant phase adds hostile tests for:

- SQL/identifier/filter/sort injection;
- stored/reflected/client-side XSS and unsafe rich/template content;
- CSRF against cookie-authenticated mutations;
- mass assignment and excessive field exposure;
- cross-tenant object/file/report/list access;
- malicious Host/forwarded headers and custom-domain confusion;
- SSRF through redirects, DNS/private ranges, alternate schemes, and oversized responses;
- file path/type/size/decompression/parser abuse;
- secrets in source, build output, logs, traces, dumps, and client artifacts;
- dependency/container misconfiguration where used;
- cache key isolation, stale sensitive state, stampede/outage/cold-start behavior where a cache exists;
- security-provider outage and recovery without an allow bypass.

Automated scanning supports but does not replace journey/resource/tenant tests, provider integration tests, review, and recovery exercises.

## 12. Explicit non-goals

This baseline does not add:

- a custom password, JWT, PASETO, MFA, SSO, or authorization platform;
- a mandatory WAF, SIEM, Redis, service mesh, Kubernetes, or security microservice;
- arbitrary rich HTML/script execution in tenant forms/templates;
- a generic secrets/provider abstraction before an actual replacement/deployment boundary requires one;
- a claim that a passing scanner proves application security.
