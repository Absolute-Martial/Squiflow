# Skeptical Implementation Gates

**Version:** v0.0.15

Use these questions before adding another project, interface, helper process, queue, cache, offline layer, provider abstraction, isolation tier or configurable state.

## Product/UX

- Is this an actual business action or merely a screen/status label?
- Can Owner + Staff use it without learning unnecessary ERP terminology?
- What happens on first use, empty state, dirty form, submit, session expiry, conflict, permission change, dependency outage and retry?
- Can Workstation users distinguish `LocalCommitted` from server `Authoritative` state?
- If something waits for another person, who finds it and how is it continued?
- Is `Cancel` actually safe, or is refund/reversal/correction the real action?
- Could an online server-side draft solve valuable-form loss without browser offline architecture?

## Identity/session

- Is this authentication, application authorization, or tenant isolation? Are we mixing them?
- Is `(issuer, subject)` the stable external account key instead of mutable email?
- Is issuer/callback/redirect validation strict?
- Does Workstation use system browser + Authorization Code + PKCE `S256` with no embedded reusable secret?
- What exactly does local sign-out terminate?
- What happens to pending local work when user/device credentials expire?
- Can another Windows user/profile inherit authenticated state accidentally?

## Permissions/control plane

- Is this permission a stable business action or a UI implementation detail?
- Can Owner safely delegate it?
- Could it expose money, stock, margin, security or another tenant?
- Is permission assignment Web-only?
- Does Desktop merely consume effective permissions rather than grant them?
- What happens to pending/offline commands after revocation?
- Does an authorization change advance `TenantAuthorizationRevision` atomically with its durable evidence?
- If a permission result is cached later, how is stale revocation prevented?
- If Core API/Admin Web is down, is recovery a private infrastructure runbook rather than a hidden business endpoint?

## Multi-tenancy/isolation

- Is TenantContext authoritative, or copied from a client-supplied TenantId/Host/header without validation?
- Is this data tenant-owned, platform-global or deliberately shared reference data?
- Can tenant-owned data be queried without tenant scope accidentally?
- Do reads, writes, reports, exports, jobs and object metadata preserve tenant scope?
- Is tenant-local uniqueness actually scoped by tenant?
- Can a pooled DB connection retain a previous tenant's context?
- If PostgreSQL RLS is used, can runtime credentials bypass it?
- Can one tenant consume all shared Worker/DB/provider capacity?
- Is a dedicated DB/stack request based on a real compliance/SLO/customer requirement rather than future fear?

## API/resource authorization

- Does the endpoint authorize the function, specific object/resource and sensitive fields separately where needed?
- Is knowledge of an object ID being mistaken for permission?
- Can resource lookup be tenant-scoped before finer authorization?
- Is a semantic requirement such as `RefundPayment` clearer than generic `Update`?
- Are request/response fields explicitly allowed, or can hidden properties be mass-assigned/exposed?
- Are list/search/report reads authorized too?

## API/reliability

- Can this mutating request be retried after response loss?
- What is its semantic idempotency key?
- What happens if the same key is reused with changed intent?
- Which failures are actually transient?
- Are retry attempts/elapsed time bounded?
- Are several retry layers multiplying calls?
- Are body/page/batch/CPU/memory/provider-cost limits explicit where needed?
- Does user-configured URL handling introduce SSRF?

## Local-first Workstation

- Does the user really need this command offline?
- Is it local-capable, provisional, or server-required?
- What commits locally before network use?
- What does the server still validate?
- What happens after weeks/months offline?
- What if local data is tampered with?
- Does update/restart/sign-out preserve pending durable work?
- What if local DB/staging disk is full?
- Does a local rule depend on current central credit/stock/security facts?
- Do we really need CRDT semantics for this aggregate?

## Device/process isolation

- Does this work truly need another process, or can it run safely in `SquiFlow.Workstation`?
- Is the proposed helper solving a measured crash/hang/native-leak/update problem, or merely following a pattern?
- If process isolation is required, what is the narrow job contract and why does the helper need any business authority?
- Can the external/native library be bounded/cancelled in-process first?
- Is Windows spooler acceptance being mistaken for proof of physical paper output?
- Does printer failure leave committed business truth intact?
- Is another peripheral a confirmed customer journey or speculative hardware support?

## Currency/business primitives

- Is a currency code configurable instead of hardcoded?
- Does an issued/posted monetary record retain the currency code needed to interpret history?
- Are we accidentally building an FX/multi-currency subsystem before a customer needs it?
- Is a generic Money/Unit/Clock helper/framework adding more complexity than using the platform types plus a few explicit business rules?
- Are document numbers being confused with stable internal IDs?
- Is posted history being destructively edited instead of corrected/revised?

## Hugging Face object storage

- Is this object a retained business object, temporary export/diagnostic, or disposable staging data?
- How does it fit inside the current ~100 GB private Hugging Face envelope?
- What happens before the capacity limit is reached?
- Is the business DB still the authority for tenant/resource ownership?
- Are issued/historical objects protected with immutable/versioned application keys instead of silently overwriting mutable bucket paths?
- Can a large upload starve normal API/sync traffic?
- Are provider-specific Hugging Face types contained inside infrastructure code?
- Why do we need an `IObjectStorage` today? Can containment suffice until the actual paid-provider migration starts?

## Kaggle backup

- Is the artifact encrypted **before** Kaggle receives it?
- Could raw DB/CSV/customer files accidentally be uploaded directly?
- Is backup key/recovery material kept outside Kaggle and itself recoverable?
- Can the remote artifact be downloaded and checksum-verified?
- Has a real restore succeeded?
- What does the backup currently include, and what required state would still be missing after a rack loss?
- Are Kaggle storage/version limits treated as finite?
- Are we ready to migrate to purpose-built paid backup storage at the first paying customer or earlier if requirements demand it?

## Physical hardware/operations

- Which actual rack node/hardware was this assumption tested on?
- Does `stateless` accidentally imply failover that does not exist?
- What happens if the only active node loses disk/power/network?
- What are DB, RAM, temp-disk and network budgets on the actual lower-spec hardware?
- Who receives the alert and who can physically/private-admin recover it?
- Is there a restore/redeploy procedure?
- What RPO/RTO can we actually promise?

## Worker/external effects — once Worker exists

- Why is this asynchronous rather than an ordinary synchronous transaction?
- Is the job a committed business consequence, deferred actor action, or platform-control command?
- If permission later changes, should this exact job still run?
- What if it crashes after an external effect but before local acknowledgement (`OutcomeUnknown`)?
- Can retry duplicate the effect?
- Can it stall indefinitely?
- Does one tenant starve others?

## Observability/support

- Is telemetry capacity/retention finite?
- Can exporter failure fill local disk or create retry storms?
- Is authoritative business/security audit stored independently from best-effort telemetry where correctness requires it?
- Could logs/crash dumps leak tenant/customer/secret data?
- Can support move from symptom to correlated evidence to safe recovery?

## Verification

- Which test layer proves the invariant: pure logic, real DB adapter, Workstation store, ASP.NET pipeline, cross-runtime journey, actual hardware, or restore drill?
- Is a mock/in-memory substitute hiding the real transaction/locking/provider behavior?
- Are Tenant A/Tenant B fixtures present for isolation attacks?
- Does CI claim a restore/hardware test it never ran?
- Are we creating interfaces only so tests can mock them? Could a real integration test be simpler and more valuable?

## Architecture/complexity

- Why does this need a separate project/process/service?
- Why does this need an interface?
- Is the interface backed by two real implementations or a real dependency inversion, or is it speculative/mock-driven?
- If a provider may change later, can we simply contain provider calls now and extract a seam during the actual migration?
- Would a concrete class/function in the current module be clearer?
- Are `Manager → Service → Executor → Handler` layers merely forwarding calls?
- Are we adding infrastructure because a catalog/article mentioned it rather than because SquiFlow has the problem?
- Does this abstraction have enough behavior/ownership to justify its name?
- Can we postpone it until measurement/customer demand exists?
- Is architecture review itself delaying Phase 0 after the risk is already understood?

## Documentation consistency

- Which focused document owns this topic?
- Is an old review/source file being mistaken for current accepted architecture?
- Is an OPEN item described somewhere else as selected?
- Has a removed baseline component (Guard, dedicated accessibility work, generic abstraction) survived in another current doc?

An implementation is not better because it contains more layers. Prefer the smallest design that preserves the actual business/security/recovery invariant.
