# Skeptical Implementation Gates

**Version:** v0.0.15

For each capability ask the questions below before adding another project, service, queue, cache, offline layer, identity mechanism or configurable state.

## Product/UX

- Is this an actual business action or merely a screen/state label?
- Can a small Owner + Staff business understand it without ERP expertise?
- What happens on first use, empty data, loading, dirty form, submit, partial completion, conflict, session expiry, permission change, dependency outage and retry?
- Can the user tell `LocalCommitted` from `Authoritative`?
- If something waits for another person, how do they discover it and continue?
- Is cancellation actually possible, or is compensation/reversal required?

## Identity/session

- Is this an authentication concern, or are we accidentally putting application authorization into the identity token?
- Are `(issuer, subject)` treated as the stable external identity rather than mutable email?
- Is the issuer configured/trusted, and does discovery metadata match it exactly?
- Is every redirect/callback target explicitly registered/validated rather than acting as an open redirect?
- For native Workstation login, are we using system browser + Authorization Code + PKCE `S256`, without an embedded client secret?
- What does local sign-out terminate, what does server-session revocation terminate, and what does identity-provider logout terminate?
- Does a high-risk action require recent authentication/step-up as well as application permission?
- Are recovery/login flows subject to stronger abuse controls than ordinary API calls?

## Permissions/control plane

- Why is this permission needed?
- Is it a stable business action or a UI implementation detail?
- Can the tenant Owner safely delegate it?
- Could granting it expose money, stock, cost/margin, security or another tenant?
- Is the assignment performed only through Web administration?
- Is the Desktop merely consuming a versioned effective permission snapshot rather than changing it?
- If this changes server/runtime/provider behavior, why is it not restricted to Platform Admin Web?
- What happens to queued/offline work after permission revocation?
- Does a role/grant/membership change advance `TenantAuthorizationRevision` atomically with audit/outbox invalidation?
- If any permission result is cached, what prevents the Zanzibar-style “new enemy” failure after revocation?

## Resource/API authorization

- Does the endpoint authorize the **function**, the **specific object/resource**, and any **sensitive properties** separately where needed?
- Is a client-provided object ID being mistaken for authority?
- Can the resource lookup be tenant-scoped before fine authorization to reduce cross-tenant existence leakage?
- Does the endpoint use ASP.NET Core resource-based `IAuthorizationService` when the decision depends on loaded data?
- Is a semantic requirement such as `RefundPayment` or `ApproveQuote` more accurate than a generic CRUD `Update`?
- Does Create authorization use a real parent/scope/context rather than a fake persisted object?
- Are request/response DTOs allowlisted, or can hidden fields be mass-assigned/exposed?
- Do list/search/report queries apply authorization too, rather than filtering only in the browser?

## OWASP/API surface

- Is the endpoint represented in generated API inventory/OpenAPI, with audience/policy/owner/version/retirement information?
- Could an old/beta/debug version bypass newer authorization?
- Are page/body/upload/batch/CPU/memory/provider-cost limits explicit?
- Does one request trigger expensive image/document/rule/provider work that rate limiting alone does not control?
- Does this expose a sensitive business flow that automation can abuse even when the caller is authenticated?
- Does any user-configured URL create SSRF risk or blindly follow redirects?
- Is third-party provider data validated with the same skepticism as direct user input?
- Could cache/CORS/error/debug configuration disclose private data or internal detail?

## Local-first Workstation

- Does the user truly need this command offline?
- Is it local-capable, local-provisional or server-required?
- What is safely committed locally before network use?
- What does server authority still validate?
- What happens if the device is offline for months?
- What happens if local data is tampered with?
- Do we really need CRDT/multi-master semantics, or does aggregate-specific conflict policy solve the problem more safely?
- Can the user understand history/conflict/pending state after remote changes arrive?
- Does the Workstation permission snapshot include an authorization revision, while server sync still reauthorizes authoritatively?

## Web

- Does this feature genuinely need offline behavior now?
- If not, are we accidentally adding IndexedDB/service-worker/multi-tab complexity with no user value?
- Can ordinary HTTP/CDN caching make it fast enough without an offline business replica?
- What sensitive data would browser JavaScript be able to read?

## Data/transactions

- Who owns the data and what is authoritative locally/remotely?
- What is the transaction boundary?
- What prevents duplicate execution?
- What happens under concurrent edits/multiple devices?
- Is a generic last-write-wins policy unsafe for this aggregate?
- Does the read path need authoritative, fresh, eventually-consistent or cached data?

## Worker/external effects

- What async work is created?
- Is it a committed business consequence, a deferred actor action, or a platform-control command?
- If the user's permission is later revoked, should this particular job still execute because the business fact was already committed, or must it reauthorize at execution?
- What if it crashes, stalls, loses its lease or resumes after another worker took over?
- What if an external effect succeeded but acknowledgement was lost (`OutcomeUnknown`)?
- Can the job be paused/drained safely?
- Can an operator retry it without creating duplicate business effects?
- Is the critical control available only from Platform Admin Web?
- Is the third-party response bounded, validated and timeout-controlled?

## Architecture/complexity

- Why does this need a separate project/process/service?
- Would an in-process module with a clean contract be simpler?
- Are we adding infrastructure merely because it may be useful someday?
- Does this abstraction have more than one real implementation/use case?
- Is a provider-specific reference project being mistaken for a product decision?
- Can we postpone this until measurements/user demand justify it?
- Are we copying Zanzibar's planet-scale relation/index/cache machinery when a small role/scope model is enough?

## Operations/security

- What gets audited/traced without leaking sensitive data?
- What are resource bounds?
- What happens during version skew/upgrade/long offline?
- How is the data backed up, restored, retained and deleted?
- Can a support/admin action be explained and rolled back/corrected?

An implementation is not complete merely because its happy path works, and an architecture is not better merely because it has more components.
