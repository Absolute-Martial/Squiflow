# Skeptical Implementation Gates

**Version:** v0.0.15

Use these questions before adding **or removing** another project, interface, process, queue, cache, offline layer, provider boundary, isolation tier, or configurable state.

The gate works in both directions:
- reject unnecessary architecture ceremony;
- reject over-minimalism that removes required edge-case/recovery/security behavior.

## Product/UX

- Is this an actual business action or merely a screen/status label?
- Can Owner + Staff use it without learning unnecessary ERP terminology?
- What happens on first use, empty state, dirty form, submit, session expiry, conflict, permission change, dependency outage and retry?
- Can Workstation users distinguish `LocalCommitted` from server `Authoritative` state?
- If something waits for another person, who finds it and how is it continued?
- Is `Cancel` actually safe, or is refund/reversal/correction the real action?
- Could an online server-side draft solve valuable-form loss without browser offline architecture?

## Identity — ZITADEL

- Is this authentication, OpenFGA authorization, or tenant data isolation? Are we mixing them?
- Is `(issuer, subject)` the stable external account key instead of mutable email?
- Is issuer/callback/redirect validation strict?
- Does Workstation use ZITADEL through system browser + Authorization Code + PKCE `S256` with no embedded reusable secret?
- Are we accidentally trusting ZITADEL project/role claims as current SquiFlow permission truth instead of using OpenFGA/SquiFlow authorization?
- Is `ZITADEL OrganizationId` being mistaken for authoritative SquiFlow `TenantId` without a deliberate mapping?
- What exactly does local sign-out terminate?
- What happens to pending local work when user/device credentials expire?
- Can another Windows user/profile inherit authenticated state accidentally?
- Does the ZITADEL management service account have only the scopes/roles it actually needs?
- If ZITADEL is unavailable, does the system fail safely rather than converting dependency failure into accidental allow?

## Authorization — OpenFGA

- Is this relationship/permission question a good fit for OpenFGA, or is it really a workflow/domain/financial invariant that belongs in SquiFlow code?
- Is the production request pinned to the intended `authorization_model_id`?
- Is a tenant-created custom role represented as tuples/data rather than a new authorization-model deployment?
- Are OpenFGA tuple identifiers opaque and free of unnecessary PII?
- What is the consistency requirement for this check: ordinary read, mutation, revocation-adjacent, or high-risk admin action?
- Could lower-latency/cached authorization return stale authority after revocation?
- If an OpenFGA tuple write succeeds but the SquiFlow completion/audit write fails, how is the operation reconciled?
- Does the API avoid reporting a role/grant change as applied before OpenFGA state is known/applied?
- Does Desktop remain read-only with respect to permission relationships?
- Can Owner safely delegate this permission without manufacturing platform/cross-tenant authority?
- Does an OpenFGA `allow` still pass through current SquiFlow state/workflow/concurrency checks?
- If OpenFGA is unavailable, which actions fail closed, which can use already-authoritative committed consequences, and which can safely degrade?

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
- Are we treating an OpenFGA relation as a substitute for database tenant isolation?

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
- Does reconnect repeat current OpenFGA + tenant/domain authorization rather than trust the old snapshot?
- Do we really need CRDT semantics for this aggregate?

## Workstation Guard

- Is Guard still able to perform the actual supervision/recovery job, or has resource/minimalism pressure stripped away required behavior?
- Can Guard launch/relaunch Workstation and distinguish intentional shutdown from crash?
- Is hang detection resistant to sleep/hibernate/temporary low-resource stalls?
- Is restart bounded with backoff/safe mode rather than an infinite loop?
- Can Guard survive/observe Workstation failure without depending on Workstation memory?
- If Guard itself crashes, can Workstation continue and can Guard recover later without corrupting local state?
- Does update recovery preserve the local DB/outbox/staged attachments?
- Are Guard diagnostics bounded and privacy-safe?
- Does Guard avoid business rules, OpenFGA permission writes, sync acknowledgment, and central DB credentials?
- Are we adding a second helper process only because a specific native/driver/library fault proves it is needed?

## Currency/business primitives

- Is a currency code configurable instead of hardcoded?
- Does an issued/posted monetary record retain the currency code needed to interpret history?
- Are we accidentally building an FX/multi-currency subsystem before a customer needs it?
- Is a generic Money/Unit/Clock helper/framework adding more complexity than a few explicit shared rules/types?
- Are document numbers being confused with stable internal IDs?
- Is posted history being destructively edited instead of corrected/revised?

## `IObjectStore` / Hugging Face

- Is the interface SquiFlow-shaped or merely a copy of the Hugging Face/S3 SDK?
- Does business code depend only on `IObjectStore`/SquiFlow-owned object types?
- Can a future paid adapter satisfy the contract without rewriting domain/application code?
- Are provider-specific migration/admin capabilities allowed to stay provider-specific instead of bloating the runtime interface?
- Is this object retained business data, temporary export/diagnostic, or disposable staging?
- How does it fit inside the current ~100 GB private Hugging Face envelope?
- What happens before capacity is exhausted?
- Is business DB metadata still authoritative for tenant/resource ownership?
- Are issued/historical objects protected with immutable/versioned application keys?
- Can a large upload starve normal API/sync traffic?

## `IBackupTarget` / Kaggle

- Is backup orchestration an infrastructure recovery concern rather than business-domain code?
- Does third-party Kaggle code stay behind `IBackupTarget`?
- Is the artifact encrypted **before** Kaggle receives it?
- Could raw DB/CSV/customer files accidentally be uploaded directly?
- Is key/recovery material kept outside Kaggle and itself recoverable?
- Can the artifact be listed/downloaded/checksum-verified through the provider contract?
- Has a real restore succeeded?
- What does the backup include besides application rows: objects, idempotency/job state, rules/config, deploy/recovery metadata, ZITADEL/OpenFGA recovery evidence as applicable?
- Can a future paid backup provider replace Kaggle without rewriting backup orchestration?
- Are Kaggle storage/version limits treated as finite?

## Physical hardware/operations

- Which actual rack node/hardware was this assumption tested on?
- Does `stateless` accidentally imply failover that does not exist?
- What happens if the only active node loses disk/power/network?
- What are DB, RAM, temp-disk and network budgets on actual lower-spec hardware?
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
- Could logs/crash dumps/Guard evidence leak tenant/customer/secret data?
- Can support move from symptom to correlated evidence to safe recovery?
- Can ZITADEL/OpenFGA/provider outages be distinguished from SquiFlow application defects?

## Verification

- Which test layer proves the invariant: pure logic, real DB adapter, ZITADEL integration, OpenFGA store/model, Guard process, Workstation store, ASP.NET pipeline, provider contract, cross-runtime journey, actual hardware, or restore drill?
- Is a mock/in-memory substitute hiding the real transaction/locking/provider behavior?
- Are Tenant A/Tenant B fixtures present for isolation attacks?
- Does CI claim a restore/hardware test it never ran?
- Is an interface present because there is a real replacement boundary (`IObjectStore`, `IBackupTarget`) or only to make mocking easier?
- Is a required edge case being skipped because the implementation has been made 'minimal'?

## Architecture/complexity

- What responsibility would be lost if this project/process/interface were removed?
- What unnecessary ceremony appears if it is kept?
- Does the boundary protect a real replacement, process-failure, security, deployment, or compatibility concern?
- Would a concrete class/function be clearer where no such boundary exists?
- Are `Manager → Service → Executor → Handler` layers merely forwarding calls?
- Are we adding infrastructure because an article/catalog mentioned it rather than because SquiFlow has the problem?
- Are we removing infrastructure merely because 'minimal' sounds better even though SquiFlow has the problem?
- Can resource optimization be measured/tuned instead of deleting required behavior?
- Is architecture review itself delaying Phase 0 after the risk is understood?

## Documentation consistency

- Which focused document owns this topic?
- Is an old review/source file being mistaken for current accepted architecture?
- Is an OPEN item described somewhere else as selected?
- Has a changed decision (Guard, `IObjectStore`, `IBackupTarget`, ZITADEL, OpenFGA) left stale contradictory wording in another current doc?

An implementation is not better because it has more layers, and it is not better because it has fewer. Prefer the smallest design that **fully preserves the actual business/security/recovery invariant**.
