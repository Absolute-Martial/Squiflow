# Skeptical Implementation Gates

**Version:** v0.0.15

For each capability ask the questions below before adding another project, service, queue, cache, offline layer, identity mechanism, isolation tier or configurable state.

## Product/UX

- Is this an actual business action or merely a screen/state label?
- Can a small Owner + Staff business understand it without ERP expertise?
- What happens on first use, empty data, loading, dirty form, submit, partial completion, conflict, session expiry, permission change, dependency outage and retry?
- Can the user tell `LocalCommitted` from `Authoritative`?
- If something waits for another person, how do they discover it and continue?
- Is cancellation actually possible, or is compensation/reversal required?
- If Web is online-only, should a long/valuable form use an online server-side draft rather than relying entirely on browser RAM?

## Accessibility

- Can the entire implemented journey be completed with keyboard only?
- Is focus order/return predictable after navigation, dialog, validation and async completion?
- Are form controls, validation and dynamic statuses exposed semantically to assistive technology?
- Is any critical state communicated only through color/icon/animation?
- Do `LocalCommitted`, `PendingRemote`, `Conflict`, `OutcomeUnknown`, permission failure and device failure have accessible text/explanation?
- Can tenant branding/dynamic forms accidentally destroy labels/contrast/navigation semantics?
- Has the journey been manually smoke-tested, not only scanned automatically?

## Identity/session

- Is this an authentication concern, or are we accidentally putting application authorization into the identity token?
- Are `(issuer, subject)` treated as the stable external identity rather than mutable email?
- Is the issuer configured/trusted, and does discovery metadata match it exactly?
- Is every redirect/callback target explicitly registered/validated rather than acting as an open redirect?
- For native Workstation login, are we using system browser + Authorization Code + PKCE `S256`, without an embedded client secret?
- What does local sign-out terminate, what does server-session revocation terminate, and what does identity-provider logout terminate?
- Does a high-risk action require recent authentication/step-up as well as application permission?
- Are recovery/login flows subject to stronger abuse controls than ordinary API calls?
- What happens to unsynced local business work when a user session/device credential expires?
- Can another person using the same Windows profile inherit the prior user's authenticated session or unsent UI state?

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
- If Platform Admin Web/Core API is down, is recovery a private infrastructure runbook rather than a hidden business endpoint?

## Multi-tenancy/isolation

- Is the current tenant context derived authoritatively, or copied from a client header/payload/hostname without validation?
- Does authentication/role authorization accidentally substitute for actual tenant isolation?
- Is this data tenant-owned, platform-global or deliberately shared reference data?
- Can a repository/query be called without a tenant scope when it touches tenant-owned data?
- Does every read, write, list, search, report, export, telemetry/diagnostic and Worker path preserve tenant scope?
- Is tenant-local uniqueness/indexing actually scoped by `TenantId` where needed?
- Could a pooled database connection retain the previous request's tenant context?
- If PostgreSQL RLS is used, can the runtime role bypass it because it is table owner, superuser or `BYPASSRLS`?
- Do RLS/write policies prevent cross-tenant INSERT/UPDATE as well as SELECT?
- Do backup/migration/support paths use deliberate privileged identities rather than ordinary runtime credentials?
- Is one tenant able to consume all Worker/database/provider capacity and degrade everyone else?
- Would bounded/fair pooled processing solve the problem before creating a physical queue/worker per tenant?
- Is a request for schema/database/stack isolation driven by residency/compliance/SLA/noisy-neighbor evidence, or only fear of future requirements?
- Can domain/application code tolerate a future tenant being placed in dedicated storage without becoming a product fork?
- Is a tenant-placement change handled as a Platform Admin migration/cutover workflow rather than a casual configuration edit?
- In a pooled DB, what are the tenant-specific export/delete/restore semantics and blast radius?

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
- What happens if local DB/staging disk is near/full?
- Does update/restart/sign-out preserve pending durable work?
- Does a local rule rely on a `ServerRequired` fact such as current credit/shared stock?

## Workstation Guard/device integration

- Why does this need a separate helper process rather than in-process code?
- Can Guard remain tiny and free of business/database/framework dependencies?
- Can a crash produce a bounded safe restart instead of a crash loop?
- Is the printer/spooler acceptance result being mistaken for proof of physical paper output?
- Does printer failure leave committed business truth intact and expose retry/alternate-printer action?
- Is another peripheral a confirmed user journey, or are we adding hardware support speculatively?
- What happens when the driver/native helper hangs, leaks memory or disappears during sleep/update?

## Web

- Does this feature genuinely need offline behavior now?
- If not, are we accidentally adding IndexedDB/service-worker/multi-tab complexity with no user value?
- Can ordinary HTTP/CDN caching make it fast enough without an offline business replica?
- What sensitive data would browser JavaScript be able to read?
- Could a server-side draft solve refresh/crash loss without introducing browser offline state?

## Data/transactions

- Who owns the data and what is authoritative locally/remotely?
- What is the transaction boundary?
- What prevents duplicate execution?
- What happens under concurrent edits/multiple devices?
- Is a generic last-write-wins policy unsafe for this aggregate?
- Does the read path need authoritative, fresh, eventually-consistent or cached data?
- Are Money/Currency/Quantity/Unit/Time represented consistently or re-invented per module?
- If a DB restore loses an idempotency receipt but keeps the effect, can a retry duplicate it?

## Files/capacity

- Which storage class does this object belong to: retained business object, temporary export, diagnostic, cache/staging or backup?
- What is the retention/expiry policy?
- How does this fit inside the current approximately 100 GB object-storage envelope?
- What happens near the warning/critical/hard capacity limit?
- Can optional data be expired without deleting retained customer/business truth?
- Is the backup independent enough from the primary business-object store?
- Can a large upload/restore saturate the site's uplink and starve API/sync traffic?
- Are decompression/resource bombs and misleading file types bounded before risky parsing?
- Can an offline Workstation still need an object that central GC wants to delete?

## Hardware/physical operations

- Which actual node/hardware was this capacity/durability assumption measured on?
- Does `stateless` accidentally imply failover that is not deployed?
- What happens if the only active node loses PSU, disk, network or power?
- Is UPS/power-loss protection present, or is the accepted risk explicit?
- What are DB connection, Worker, RAM, temp-disk and network budgets on the actual lower-spec rack?
- Who receives the alert and who can physically/private-admin recover the rack?
- Is there a replacement/spare/redeploy procedure?
- What are the real RPO/RTO and maintenance/support promises?

## Worker/external effects

- What async work is created?
- Is it a committed business consequence, a deferred actor action, or a platform-control command?
- If the user's permission is later revoked, should this particular job still execute because the business fact was already committed, or must it reauthorize at execution?
- What if it crashes, stalls, loses its lease or resumes after another worker took over?
- What if an external effect succeeded but acknowledgement was lost (`OutcomeUnknown`)?
- Can the job be paused/drained safely?
- Can an operator retry it without creating duplicate business effects?
- Is the critical control available only from Platform Admin Web in normal operation?
- Is the third-party response bounded, validated and timeout-controlled?
- Does a notification/webhook failure incorrectly rewrite the originating business transaction?

## Observability/support

- Is the telemetry provider quota/retention treated as finite?
- Can telemetry outage/quota exhaustion fill local disk or create a retry storm?
- Are authoritative audit events stored durably rather than only in managed logs?
- Can logs/crash dumps/diagnostics leak another tenant or secrets/customer content?
- Can support move from symptom → correlated evidence → safe recovery, or do we only have raw logs?
- Does an alert reach the actual person who can act on it?

## Verification

- What exact test layer proves this invariant: domain, application, real adapter, API, Workstation, end-to-end, failure injection, release hardware or manual exercise?
- Is an in-memory/mock DB hiding a transaction/isolation/locking bug that only the candidate provider can reveal?
- Does CI claim success for a hardware/restore scenario it never ran?
- Is the failure/recovery outcome observable and reproducible?
- Are Tenant A/Tenant B fixtures present for isolation attacks?
- Are skipped Workstation versions and rolling Web/API/Worker compatibility actually tested?

## Architecture/complexity

- Why does this need a separate project/process/service?
- Would an in-process module with a clean contract be simpler?
- Are we adding infrastructure merely because it may be useful someday?
- Does this abstraction have more than one real implementation/use case?
- Is a provider-specific reference project being mistaken for a product decision?
- Can we postpone this until measurements/user demand justify it?
- Are we copying Zanzibar's planet-scale relation/index/cache machinery when a small role/scope model is enough?
- Are we creating per-tenant schemas/databases/queues/stacks before any isolation requirement justifies their operational cost?
- Is architecture review itself delaying Phase 0 after the current risk is already understood?

## Documentation consistency

- Which focused document owns this topic?
- Is this file duplicating a detailed contract that belongs elsewhere?
- If two current docs disagree, which owner is corrected and which summary is updated?
- Is a review/source document being mistaken for an accepted architecture decision?
- Is an OPEN item accidentally described elsewhere as selected/current?

## Operations/security

- What gets audited/traced without leaking sensitive data?
- What are resource bounds?
- What happens during version skew/upgrade/long offline?
- How is the data backed up, restored, retained and deleted?
- Can a support/admin action be explained and rolled back/corrected?
- What is the blast radius of a tenant-isolation bug or noisy-neighbor event?
- Can a dedicated tenant still run the same SquiFlow release instead of becoming a custom fork?

An implementation is not complete merely because its happy path works, and an architecture is not better merely because it has more components.
