# Customers duplicate resolution and bounded import owner

Product version remains **v0.0.1**. This owner covers COM-003 and COM-004 only.

COM-004 implementation and local qualification are **COMPLETE** for the declared
capability, CoreApi and PostgreSQL contracts. Raw-source lifecycle, expiry-aware
reads, fenced retirement/recovery and hosted source-only tenant execution are
locally `PRODUCTION_HONEST`. Production/provider qualification remains `BLOCKED`
only on the live OPS-007/OPS-008 Hugging Face evidence; this is not an unresolved
local retention or recovery implementation claim.

The fresh exact `./eng/verify.sh` completed with **exit 0**, **1045 passed / 0 failed /
0 skipped** across **20 test projects**, and a Release build with **0 warnings /
0 errors**. The final gate includes CoreApi **458/458** and Customers PostgreSQL
**51/51**. Standalone pre-upload-fix suites passed 457/457 and 51/51; they are
supporting earlier evidence, not substitutes for the final gate. Post-fix standalone
formatter verification and `git diff --check` passed. Complete local results, source
hashes, original failures and the safety review are retained under
`artifacts/verification/com004-current-20261007/` (`RESULTS.json`, `gate.log`,
`gate-source.json`, `STATIC-REVIEW.md`).

Live Hugging Face upload, download, delete, conditional write, redirects/timeouts, finite capacity, asymmetric provider/database failure and remote reconciliation remain unrun and NOT qualified. Checked-in `ObjectStorage.Enabled=false`; no nonempty relevant provider runtime configuration variables were visible. No private credential, substitute provider or fallback byte archive was introduced.

This is local dirty-tree qualification, not commit/merge/PR integration, remote CI,
coverage, live identity-provider qualification, deployment readiness or production
acceptance. Incoming `eng/verify.sh` runs its unit/container project groups
sequentially; this pass did not change that policy or serialize test cases to hide
failures. Historical gate totals are preserved below only as historical evidence.

## COM-003

Names, email, phone and external registration IDs are **nonunique signals**, never
business identity or merge authority. Names/email/external IDs use Unicode NFKC,
trim and invariant case normalization. Phone input permits only ASCII digits,
common formatting and one optional leading plus, with 3–15 digits. The signal
removes formatting; no country inference, extension parsing or international
number verification is claimed. Exact organization/program representative
relationships are supporting evidence. Bounded fuzzy-name discovery is optional,
not merge authority or an exhaustive search engine.

Outcomes are `PotentialDuplicate`, `KeepSeparate`, `ConsolidateInto`, and `Dismiss`.
`Customers.Duplicates.Resolve` cannot consolidate. The distinct
`Customers.Duplicates.Consolidate` operation checks both customer revisions and
creates an attributable redirect/receipt. A reason is optional, printable,
normalized and bounded to 1,000 characters. Resolution receipts retain the matched
signal/relationship kinds at decision time; they remain immutable and are available
through paged reads and discovery's retained-resolution fields, even if contacts
later change. Old receipts predating the evidence field report empty evidence;
they are not fabricated from today's contacts.
`PotentialDuplicate` may progress to a final decision; final KeepSeparate/Dismiss
decisions are not silently overwritten by another operation.

Consolidation preserves both master identities and every create/contact/lifecycle
receipt. It does not combine contact fields, physically delete customers or
rewrite committed Orders/issued debtor facts. Live representative relationships
move to the selected canonical customer; collisions deactivate the duplicate live
relationship while retaining its old receipt. Inactive relationships remain
history. Current successor pointers are separate from the immutable original
`customer_redirects` ledger and resolution/create receipts. **B→A, C→A followed
by manual A→D atomically advances current A/B/C pointers to D**, incrementing each
changed physical customer's revision once. The ledger still records B→A, C→A,
A→D and the original actors/timestamps. It is never updated/deleted to manufacture
a new historical outcome. Customer attributes, inactive relationships, committed
Orders and issued facts do not change. Active representative collisions become
inactive without changing their retained identity; non-colliding active references
move forward and retain their original command receipts.

Revisions bind the **explicit physical source and survivor IDs**. A request C→B
after B→A resolves the observed survivor A but returns `RevisionConflict` with
`CanonicalCustomerId=A` in the capability result, because B's expected revision
cannot safely authorize A's
revision. The caller must deliberately resubmit C→A with A's current revision;
the resulting current graph is B→A,C→A. The existing HTTP conflict mapper reports
the `RevisionConflict` code; the survivor ID above is not a new conflict-body wire
field. Current customer/canonical queries supply the survivor facts for explicit
resubmission. Even numerically equal revisions are not
interchangeable across identities. Attempting A→B when B→A returns `CycleDetected`.
An already-redirected source cannot be independently repointed by a new command.
First and post-lock receipt lookups precede current graph/revision checks: an
exact-intent retry of the original B→A command still returns its original A result
after A→D, not today's D. Changed intent under that key remains a conflict.

One tenant-scoped transaction advisory gate serializes consolidation, duplicate
resolution and representative link/unlink before ordered customer row locks.
Matching **statement-level** database gates run before individual updates,
representative inserts/updates and redirect-ledger inserts, including contact and
availability edits. Manual individual create and import row acceptance join the
same gate from the adapter, because an individual insert publishes the same
canonical duplicate signals those readers serialize on; those two writers are not
covered by the statement-level gates above. The gate is held inside the caller's
existing transaction, so it is released with it and adds no transaction boundary.
Normal runtime writes require the existing RLS tenant context;
privileged direct maintenance writers must also set that context before mutations.
The gate namespace is `customers:canonical:` plus the tenant UUID, hashed with
PostgreSQL `hashtextextended(...,0)`; a hash collision only adds serialization,
never mixes tenant data. No timeout/retry policy or background task is introduced.

Foreign keys, append-only history guards and deferred end-of-transaction checks
prevent cycles, dangling successors, unflattened current pointers and live
representatives left on a redirected source. Existing partial unique relationship
indexes prevent duplicate active links; collision resolution and pointer advancement
share the same transaction/gate. No active/inactive master/contact semantics are
redefined. New links and edits cannot revive a redirected source. Canonical lookup
joins original/survivor in one statement snapshot rather than racing two separate
READ COMMITTED reads. This is explicit manual consolidation under only
`Customers.Duplicates.Consolidate`, never a fuzzy/automatic merge.
Pre-extension individual create receipts keep their name/email/phone fingerprint
replay compatibility for default individuals; a replay cannot add new attributes.

`ICustomerCanonicalDirectory.ResolveCurrentCustomerAsync` is the host-neutral
integration hook for current mutable attribution. Historical readers use the
original identity/frozen facts. Other capabilities must not update Customers
tables or rewrite their own committed facts to implement canonicalization.

## COM-004 format, retention and validation

`customer-import/v1` is UTF-8, comma-delimited CSV with a required header and
required `name`. Optional fields are `external_id`, `customer_type`, `email`,
`phone`, `address_line1`, `address_line2`, `city`, and `notes`. Unknown/repeated
headers and malformed quoting/encoding are refused. The bounds are **10 MiB** and
**10,000 data rows**; raw CSV cells are additionally capped at 4,096 characters
and the approved column count. Normalized field bounds are capability-owned. Planning validates the
entire file and the entire existing/in-file exact duplicate set before customer
effects. All source values are plain text, never executable spreadsheet formulas.
The canonical empty template is
`modules/customers/Application.Customers/customer-import-v1-template.csv`, also
available through the protected template API.

Raw CSV is staged to a bounded disposable server file, fully validated, then streamed
through the neutral `IObjectStore` contract. The bootstrap adapter is the Hugging Face
Storage Bucket S3-compatible gateway (`https://s3.hf.co/<namespace>`) using path-style
bucket addressing and deployment-supplied HFAK credentials. Application keys are
content-addressed per tenant/import and are immutable even though the provider bucket
itself is mutable; `If-None-Match: *`, expected byte length and SHA-256 verification
enforce that boundary. The adapter follows only bounded HTTPS Hugging Face redirects,
does not log provider response bodies or credentials, and maps timeout/permission/
capacity/unknown outcomes to owned results.

PostgreSQL retains source object metadata, state (`staged`, `quarantined`, `available`,
`unavailable`, `orphaned`, `retired`, `retirement-pending`), default seven-day or
explicit archival retention, and one tenant/provider-scoped byte reservation/accounting
row. Reservation admission locks the usage row, is semantic-idempotent by the import
key, and retains unknown effects for reconciliation. Expired default objects are claimed
and retired only when no active/unknown reservation remains; the import plan, manifest,
row hashes, decisions and results remain after raw bytes retire. Source download is a
separate protected route and checks current tenant/import permission plus available
metadata; object keys alone never authorize access. Staged/quarantined/orphaned bytes
are not published.

The explicit transport election is the single bounded header
`X-Tenant-Import-Retention: default|archive`; absence means the seven-day default.
Malformed encoding, oversized input, invalid CSV, invalid election and capacity
rejection occur before provider publication. No ad-hoc PostgreSQL raw-byte store,
filesystem archive, substitute provider or manufactured credential is added. Manifest hash,
source-row hashes, validated normalized
plan data, duplicate evidence, immutable decisions and results are retained in
tenant-isolated PostgreSQL; they contain PII and require protected reads/backup.
No raw/source PII or provider error text is written to application telemetry.

Possible duplicates include **exact names**, email, phone and external IDs both
against existing customers and other plan rows. Every such row needs an explicit
`CreateNew`, `MapToExisting`, or `Reject` decision. Decisions bind row number and
source hash. Mappings must reference a validated row and an active canonical
customer in the same tenant; nonexistent/extra mappings are errors, never ignored.
Mapping does not overwrite the existing customer's attributes. A duplicate which
appears before acceptance rejects the now-outdated plan without changing its
retained validation flags; a deliberate new plan is required. A duplicate which
appears after acceptance rejects an implicitly-new row rather than silently
merging or guessing. That row-level check and its insert are one serialized unit
under the tenant canonicalization gate, so a manual create cannot commit the same
normalized signals between them. Rejection is a bounded error code, never a merge
or a guess, and no unique constraint on normalized name, email, phone or external
registration ID exists: ordering the writers is the whole claim, and canonical
uniqueness stays operator/consolidation meaning. Accepted decisions cannot be
changed by a new request key.

Row statuses are `Pending`, `Imported`, `MappedToExisting`, `Rejected`, and
`Failed`. Each retained row exposes a stable RowId, RowNumber and SourceRowHash
(ImportId is the owning plan/page identity),
decision, result customer ID, attempts and bounded error codes. Created customer
ID is the stable row ID. Customer creation and the committed row result share one
transaction. Status counts distinguish terminal processed rows from Pending/Failed
remaining rows. Preview/result reads page at **1–50 rows**, never an entire 10,000
row API response. API plan/accept responses contain bounded metadata/work only.

## Acceptance, execution and recovery

`ExecuteCustomerImport` performs a fresh neutral authority check and **only accepts
durable work**. It does not import customers in the HTTP request or launch an
untracked task. A per-import transaction advisory gate serializes acceptance;
one durable work row per import pins its immutable decision fingerprint and actor.
Completed acceptance can replay its retained intent after a mapped customer's later
consolidation; replay does not rebind historical row results to the new survivor.

`RunCustomerImportBatch` owns execution meaning, using
`ICustomerImportWorkStore` and `ICustomerImportAuthority`. A host invokes
`ExecuteAsync(tenantId, workerId, limit, drainToken)` with a stable worker identity,
at most 50 rows and a 2-minute lease. The separate protected
`POST /api/v1/tenants/{tenantId}/customers/imports/run-batch` command awaits one
bounded batch; it does not run inside acceptance and does not schedule background
work. **It is not the autonomous scheduling mechanism**. It is nevertheless gated by
the same `CustomerImportExecutionState` admission check as acceptance, with the same
`503` / `import_executor_unavailable` problem returned **before** any body parse or
claim, so a disabled, starting, failed-discovery or draining executor cannot be
reached through manual acceleration either. The database persists
owner, generation, lease, status and retry delay.
Each effect transaction locks/checks the current
generation before its row and commits effect/result atomically. An expired claim
can be recovered by another process/instance; the old generation is fenced out.

The row-completion `UPDATE` carries the same fencing predicate as the pre-row
gate: work id, worker, generation, claimed status and an unexpired lease must all
still hold, and exactly one row must be affected or the claim is lost. The
pre-row lock is an ownership pre-check, not the commit authority; the commit
point re-asserts ownership rather than relying on the `FOR UPDATE SKIP LOCKED`
claim handoff, which is not part of any durable contract. A commit-side
non-cancellation fault records the bounded row-failure attempt in its own fenced
transaction, because a failed completion or commit cannot record its own
outcome; a terminal row (the commit may have succeeded) matches nothing.
Infrastructure-class SQLSTATEs (class 08 connection exception,
`57P01`/`57P02`/`57P03`, `53300`, `40001`, `40P01`) are not attributable to the
accepted row intent, so they never consume a row attempt: the transaction rolls
back, the claim is released and the row stays claimable. Genuine business row
failures still consume the bounded budget. A claim that expires mid-row cannot
complete its row and loses the whole attempt; an unreachable database can still
prevent recovery accounting, which remains an operator-visible
`database_unavailable` deferral rather than a row success.

Before **every row**, `ICustomerImportAuthority.CheckAsync` must verify active
account, active tenant/current membership and current import permission, returning
the membership-derived context and authoritative authorization revision. Missing,
denied, unavailable or changed authority performs no next effect. Changed authority
pauses work with `authority_changed`; deliberate re-acceptance of the exact same
intent after a fresh check can resume under the new revision. Graceful drain stops
claiming/processing and uses a bounded 5-second release budget; hard termination
recovers by lease expiry, not a callback. Failed rows retry after 5 seconds, with
at most **3 durable row attempts**; exhausted Failed rows require operator review
and a new deliberate plan, not an infinite retry. Provider outages do not invent
row success or erase already committed results, and they do not spend a still-valid
row's bounded attempt budget either.

The commit-point fence and its retry classification are guarded permanently by
`StaleClaimGenerationAndWorkerCannotCompleteARowTheCurrentOwnerHolds`,
`ExpiredClaimCannotCompleteItsRowAndTheRowKeepsItsWholeAttemptBudget`,
`InfrastructureClassFaultsDoNotConsumeTheRowsBoundedAttemptBudget` and
`CommitSideFaultRecordsABoundedRowFailureInsteadOfRetryingWithoutAnAttempt`.
Changing the row-completion fencing predicate, the claim parameters it re-asserts
or the SQLSTATE classification requires rerunning those four real PostgreSQL
guards together with the fenced restart and bounded-attempt guards above them.

The migration marks pre-fenced accepted work `legacy_work_requires_replan` rather
than executing legacy mutable mapping intent. Its retained plans/results remain;
new execution requires a deliberate new plan. Destructive downgrade refuses to
discard retained import plans. No new process/project is earned: an in-process
hosted executor calls the same runner. A separate Worker, if later earned,
owns composition/lifecycle only, not another import business implementation.

## Autonomous CoreApi composition and privileged discovery

Source admission: reuse standard .NET 10 hosted lifecycle and the already-admitted
EF/Npgsql transactional/hostile-test mechanisms. The FSH background-jobs mechanism
remains rejected by the [adoption ledger](../review/FULLSTACKHERO_BACKEND_ADOPTION_LEDGER.md);
no Hangfire, actor mailbox, Quartz trigger or generic queue becomes durable import
authority. CSV-plan meaning, row atomicity, current authority and generation fencing
remain Customers-owned. This one sequential, non-scheduled import workload does
not activate the generic [Worker topology/scheduler](../server/WORKER_RUNTIME_AND_SCHEDULING.md).
No upstream source or new dependency was copied. New workload classes, scheduling
semantics or process isolation require corresponding focused runtime admission.

`CustomerImportHostedExecutor` uses the standard .NET 10 `BackgroundService`
already provided by the host framework. It owns one sequential awaited loop,
one process-local worker identity, a disposable scope per discovery/batch and
no detached tasks, retained tenant dictionary or in-memory work queue. This is
one scoped workload, not a new generic Worker, Proto.Actor or Quartz mechanism.
The standard host cancels and awaits the loop at shutdown; the existing runner's
five-second cleanup is awaited before its scope is disposed. Timeout/drain is
cooperative, not a promise of forced termination or instant provider cancellation.
Fatal unexpected errors stop the service/standard host with a safe message;
provider/database/operation-timeout retries emit only bounded failure codes,
never provider exception contents or source PII.

`ICustomerImportWorkDiscovery` is a capability-neutral **privileged executor
boundary**, not a business tenant directory or authority grant. Its versionless
in-process cursor is an exclusive tenant UUID; a page contains only at most
50 distinct tenant UUIDs and the next UUID cursor. PostgreSQL ordering, not
client culture or process memory, determines continuation. A full page may
produce a final empty page before wrapping. Each poll handles one bounded page,
one bounded batch per candidate and then delays; cursor wrap revisits retained
work without starving later tenant UUIDs behind a permanently busy first tenant.
Process restart simply begins discovery again. Candidates match the existing
claim's due/lease/status/attempt/authority-pause conditions, including a job whose
last row committed before finalization. No tenant-table enumeration, RLS-disabled
runtime connection or supplied tenant bypass is introduced.

`20261007080000_CustomerImportAutonomousDiscovery` adds a narrowly scoped
`customers.discover_runnable_import_tenants(uuid,integer)` security-definer
function with fixed `search_path=pg_catalog`, qualified table references,
`row_security=on`, database-enforced page bounds and revoked PUBLIC execution.
Only the trusted migration/function owner has SELECT policies across
`customers.import_work` and `customers.import_rows`, necessary to discover
cross-tenant candidate IDs even with forced RLS and a non-BYPASSRLS owner.
Those policies grant no runtime table access and no authority over Tenancy.
The runtime must not inherit or be allowed to assume that trusted owner role.
No role or BYPASSRLS attribute is created by the migration. Ordinary runtime
table reads/writes retain their tenant policies; only an explicit EXECUTE grant
exposes the bounded candidate-ID function. Ownership/policy-role changes must be
requalified together; changing the function owner alone is unsupported.
The migration adds an operational partial discovery index; retained table/column
models remain unchanged and reuse the prior immutable migration target model.

`CurrentCustomerImportAuthority` uses public IdentityAccess/Tenancy contracts to
check an active account, active tenant and membership-derived context on each
call. It checks `ITenantCustomerAuthorization.CanImportCustomersAsync` against
the configured higher-consistency provider adapter and reads the authoritative
Tenancy authorization revision on both sides of that check. A missing revision,
denial or revision change fails closed; a provider outage remains an explicit
unavailable exception and does not become a durable permission-denial pause.
Cancellation is checked again before an allowance is returned. There is no
saved token, assumed Owner grant, cached account/tenant allowance or invented
authorization revision. As with request authorization, this is a fresh check
before each row, not an atomic lock spanning the external provider and row commit.

### Startup, admission and resource policy

Main composition calls `AddCustomerImportExecution(configuration,
databaseConfiguration, objectStorageConfiguration)` after the normal
capability/authorization registrations.
It registers the scoped authority, hosted executor, execution configuration and
`CustomerImportExecutionState`. **Acceptance and the protected run-batch command must
both reject with a safe 503 before parsing/accepting if `IsAcceptingWork` is false.**
Disabled, starting, failed discovery and draining executors cannot admit new work. The
first successful privileged discovery opens admission; a subsequent discovery failure
closes it. Drain is terminal for this process state and cannot be reopened by a late
callback. Existing work remains durable and resumes on a correctly configured later host.

### Retention has exactly one deleter, so its absence fails startup and readiness

Retained raw customer PII has exactly one remover: `ReconcileCustomerImportSource`,
which only the hosted executor invokes. Nothing in the request path, another process,
a provider lifecycle rule or a schedule deletes raw bytes. Therefore:

- `ObjectStorage:Enabled=true` with `CustomerImports:Execution:Enabled=false` **fails
  startup** during composition with a bounded configuration error. That combination
  uploads expiring `available` sources whose only deleter can never run, which is
  unrecoverable PII retention, and it must not be reachable with a green probe. The
  reverse direction (execution enabled, object storage disabled) stays valid: retirement
  simply finds nothing to delete.
- `CustomerImportExecutionReadinessCheck` is part of the `readiness` tag set. It reports
  healthy when execution is disabled and reports the executor admission state when it is
  enabled, so a host that lost discovery admission cannot stay ready while raw sources
  stop being deleted. This is a deliberate trade-off: a freshly started host is
  unhealthy for at most one discovery cycle until its first privileged discovery
  succeeds. A ready host therefore also asserts that deletion is actually happening.
  It does not probe the provider, the database or any stored byte.

An authorization-provider outage during acceptance or run-batch is a dependency
failure, not an internal error: both handlers catch
`AuthorizationProviderUnavailableException` and return the same safe
`503` / `authorization_unavailable` problem used by the other current endpoints,
rather than falling through to the generic `500` / `internal_error` handler.

Configuration uses `CustomerImports:Execution`:

| Setting | Default | Allowed |
|---|---|---|
| `Enabled` | `false` (safe when absent) | Explicit boolean; enable only with shared wiring/schema/grants, and it is **required** when `ObjectStorage:Enabled=true` |
| `PollIntervalSeconds` | 5 | 1–300 |
| `TenantPageSize` | 10 | 1–50 |
| `BatchRows` | 25 | 1–50 |
| `OperationTimeoutSeconds` | 20 | 1–60, per awaited discovery or batch |

The executor reuses the existing bounded/resetting primary Npgsql data source,
never creates another pool and has at most **one concurrent database operation**.
Enabled execution requires `Database:MaximumPoolSize >= 2`; it is not a guarantee
of dedicated interactive capacity or cross-replica fairness. Shared pool waits
are covered by the operation deadline. Standard `HostOptions:ShutdownTimeout`
(default 30 seconds) must cover the configured operation deadline plus the
five-second cleanup budget. Enabled execution also requires the standard
`HostOptions:BackgroundServiceExceptionBehavior=StopHost` (its default), not
silently ignored fatal executor failures; invalid settings fail startup. There is no second
custom shutdown timer/configuration or unbounded concurrency setting.

## Composed host and deployment contract

- Call `app.MapCustomerDuplicateImportEndpoints()`.
- Supply `AuthorizedCustomerDuplicateRead`, `AuthorizedCustomerDuplicateResolve`,
  `AuthorizedCustomerDuplicateConsolidate`, `AuthorizedCustomerImport` application
  authorization classifications. Endpoints resolve current account/membership and
  declared authority through `TenantCustomerEndpoint.ResolveAsync` **before parse**.
- Call `AddCustomerImportExecution(builder.Configuration, databaseConfiguration,
  objectStorageConfiguration)` from Program and use the execution-state admission
  check above on acceptance **and** on the protected run-batch command. That
  registration also refuses `ObjectStorage:Enabled=true` with execution disabled.
  No permissive authority default is provided. Customers provider registration
  includes `ICustomerImportWorkStore`, `ICustomerImportWorkDiscovery`,
  `RunCustomerImportBatch` and `ICustomerCanonicalDirectory`. The protected
  run-batch API is optional manual acceleration, not the autonomous executor.
- Apply `20261006170321_CustomerImportFencedExecution` and then
  `20261007080000_CustomerImportAutonomousDiscovery` through the existing migrator.
- Apply the additive `20261007110000_CustomerForwardCanonicalization` after those
  migrations. It changes trigger/function semantics only, retaining their previous
  immutable EF target model and the current snapshot. No runtime UPDATE/DELETE on
  `customer_redirects` or receipt tables and no new grants are needed. A downgrade
  refuses current pointers which have advanced beyond their original ledger target;
  it does not rewrite evidence to restore the old incoming-source ban.
- Grant only `EXECUTE ON FUNCTION
  customers.discover_runnable_import_tenants(uuid,integer)` to the CoreApi runtime
  login, besides the tenant-isolated table rights below. PUBLIC and other business
  logins receive no function execution. Keep runtime and migration-owner roles separate.
- Runtime role needs SELECT/INSERT on imports/import_rows/import_work,
  duplicate_cases/duplicate_command_receipts/customer_redirects. Additional UPDATE
  columns: individuals `(normalized_name, normalized_email, normalized_phone,
  redirect_target_individual_id, revision)` plus existing contact/lifecycle columns;
  representatives `(individual_id, availability, revision, changed_by_account_id,
  changed_at)`; duplicate_cases `(evidence, outcome, resolved_by_account_id,
  resolved_at, reason)`; import_rows `(requires_decision, duplicate_evidence,
  decision, mapping_customer_id, status, customer_id, error_code, error_message,
  processed_at, attempts)`; import_work `(status, completed_at, last_error,
  authorization_revision, generation, worker_id, lease_expires_at, next_attempt_at)`.
  Raw-source additions require SELECT/INSERT on object_storage_usage,
  import_source_objects and object_storage_reservations, plus source_object_key on
  imports and the narrow lifecycle/accounting updates defined by the deployment grant.
  No raw bytes are stored in PostgreSQL and no table needs DELETE or runtime DDL.

## Evidence and requalification

Earlier dated totals in this section are historical; current raw-source qualification is recorded below.

Permanent focused guards are `CustomerDuplicateAndImportTests`,
`CustomerDuplicatesAndImportsPostgresTests` (real PostgreSQL 17),
`CustomerForwardCanonicalizationPostgresTests` (real PostgreSQL 17),
`CustomerImportExecutorTests`, `CustomerImportAutonomousPostgresTests`, new
`CustomerDuplicateImportEndpointTests`, and the embedded-runtime-SQL architecture
guard. They cover normalization/quoting/bounds, explicit decisions/nonunique names,
tenant isolation, immutable mapping intent, retained/current reference separation,
survivor races/chains, exact counts, expired-lease restart/stale-generation fencing,
Failed retry/exhaustion, actual mid-effect database-connection termination and
restart, last-row finalization, membership suspension, denied/outdated authority
and graceful drain. Discovery tests exercise actual denied EXECUTE grants and
cross-tenant negative RLS reads/writes, bounded SQL inputs, exhausted/leased/paused
work exclusion, fixed search path including a temporary-table shadow attempt,
and a non-superuser/non-BYPASSRLS function owner. Autonomous tests use the real
hosted service, restricted shared PostgreSQL pool, public current-account/tenant/
membership/revision adapters and committed retained work. A controlled permission
port distinguishes deny/outage; these tests do not qualify the real OpenFGA
provider by mocking its behavior. Every hosted test has explicit bounded teardown.
No separate Worker process/restart responsibility is claimed.

On 2026-10-07, the exact focused
`dotnet test tests/integration/Application.Customers.Postgres.Tests/Application.Customers.Postgres.Tests.csproj --no-restore --configuration Release`
run passed **41/41** tests with zero failures/skips and no build warnings/errors. Because shared CoreApi endpoint
classification was still incomplete, the two new executor test source files and
actual production executor/authority/configuration files were compiled in a
temporary .NET 10 test harness under `/tmp/opencode/customer-import-execution-check`
using the repository's build settings and pinned packages. Its exact
`dotnet test /tmp/opencode/customer-import-execution-check/Application.ImportExecution.Check.csproj --no-restore --configuration Release`
run passed **33/33** tests with zero failures/skips and no build warnings/errors,
including nine real PostgreSQL hosted-executor cases. This is isolated evidence,
not qualification of the actual CoreApi executable/HTTP composition. Re-run those
same source tests in `Application.CoreApi.Tests` after shared integration; no full
repository gate is claimed by this work. The three focused architecture checks
for dependency/provider isolation, embedded runtime SQL and neutral active
identities also passed. Formatting verification passed for the new executor/
authority/configuration/test sources and touched Customers adapter source files.

### Forward canonicalization evidence (2026-10-07)

The exact Release-focused Customers PostgreSQL run passed **15/15** tests,
including the seven new `CustomerForwardCanonicalizationPostgresTests` cases and
existing duplicate/contact/representative/import-replay regressions. The new
guards compare retained audit snapshots against advancing current pointers, prove
explicit-survivor revision conflicts/cycle denial, preserve inactive/colliding
relationships and original command replays, race ten distinct actors' consolidation/
link/unlink operations alongside canonical reads, reject incomplete flattening at
commit, and exercise migration from pre-forward history plus refused lossy downgrade.
The migration test also verifies no pending model changes/migrations and exactly
one inherited context attribute on the new migration. No historical target model
or existing snapshot was regenerated.

Commands inspected for this delta:

```sh
dotnet test tests/integration/Application.Customers.Postgres.Tests/Application.Customers.Postgres.Tests.csproj --no-restore --configuration Release --filter 'FullyQualifiedName~ForwardCanonicalization|FullyQualifiedName~CanonicalizationRejects|FullyQualifiedName~ManyActorConsolidation|FullyQualifiedName~DatabaseGuardsRollBack|FullyQualifiedName~ConsolidationMovesOnlyCurrentRepresentatives|FullyQualifiedName~AdditiveCanonicalizationMigration|FullyQualifiedName~ContactAndReviewReceipts|FullyQualifiedName~DuplicateConsolidation|FullyQualifiedName~ContactRevisionRace|FullyQualifiedName~KeepSeparateEvidence|FullyQualifiedName~ImportPersistsPlanHashes|FullyQualifiedName~RepresentativeRelationship|FullyQualifiedName~RepresentativeLinkWaits|FullyQualifiedName~ContactEditIsRevision'
dotnet test tests/unit/Application.Customers.Tests/Application.Customers.Tests.csproj --no-restore --filter 'FullyQualifiedName~CustomerDuplicateAndImportTests|FullyQualifiedName~CustomerRepresentativeTests'
dotnet test tests/architecture/Application.Architecture.Tests/Application.Architecture.Tests.csproj --no-restore --filter 'FullyQualifiedName~RuntimeSqlRemainsInEmbeddedAdapterResources'
```

The unit run passed **17/17** and the embedded-runtime-SQL guard **1/1**. Focused
format verification and `git diff --check` passed. The standalone EF CLI
`migrations has-pending-model-changes` command was attempted but could not discover
contexts because the pre-existing `CustomerImportAutonomousDiscovery` repeats its
base migration's inherited `DbContextAttribute`. That import-owned file was not
changed in this delta; the real-database migration/model guard above passed.
The repeated context attribute was removed in receiving integration; a permanent
`MigrationRegistryTests` regression checks every migration's one effective owner.
The standalone EF CLI is not installed on the receiving PATH; real migration/model
tests and registry checks are the performed guard, not a claimed CLI pass.
Shared HTTP/auth/DI/grants are composed; final normal-gate evidence is retained
in `docs/development-tasks/TASK_STATUS.md` and `README.IMPLEMENTATION.md`. Changes to the tenant
gate key/order, successor/ledger distinction, forward validation, revision binding,
receipt precedence or representative mutation paths require rerunning these
guards and main's integrated gate.

### Current raw-source local qualification (2026-10-07)

The final 1045-test gate and current 51-case real PostgreSQL suite qualify the
listed local claims. Permanent guards include
`ImportSourceRetirementClaimsAreFencedRecoverableAndReleaseUsageOnce`,
`RawSourceLifecycleMigrationRollsBackAndReappliesItsOwnedObjects`,
`RawSourceLifecycleDowngradeRefusesNonEmptyAccounting`,
`ExpiredAvailableSourceIsBlockedBeforeObjectStoreRead`,
`ArchivedAvailableSourceWithoutExpiryCanBeRead`,
`ProviderReadFailuresReturnNoStore503AndDisposeSuppliedContent`, and
`AutonomousHostDiscoversExpiredSourceOnlyTenantAndRetiresWithControlledDeleteOutcome`.

Claims use a five-minute, generation/lease-ID-fenced retirement claim, recoverable
expired pending state, active/unknown reservation exclusion and one-time retained
usage release. Expired default sources are unavailable before provider access;
available archives have no expiry. Missing/retired sources are not-found; staged,
quarantined, unavailable, orphaned and pending sources cannot be downloaded.
Failed provider reads dispose supplied content and return safe no-store 503.
Retirement changes lifecycle/accounting fields only, retaining source metadata,
manifest/plan rows, decisions and results. Real PostgreSQL tests inspect forced RLS,
restricted-role retirement/accounting and safe empty rollback/reapply; nonempty
accounting refuses downgrade with SQLSTATE `55000`. Source-only tenant discovery
and retirement are exercised by the actual hosted executor with controlled delete
outcomes. See `artifacts/verification/com004-current-20261007/STATIC-REVIEW.md` for exact static/dynamic boundaries and non-claims.

A current controlled PUT regression reproduced the earlier disposed-hasher defect.
The adapter now finalizes/caches its digest before native StreamContent disposal,
retaining length/hash verification and caller stream ownership. The focused adapter
suite passed 4/4 and the final gate re-exercised it. The first post-fix test attempt
expected one HEAD, while the real verification path uses two; that test-only
request-count assumption was corrected without weakening byte/outcome/read-back or
ownership assertions. Both failures are retained. These checks do not qualify live
provider signing, streaming, private access or recovery.

Earlier raw-source evidence recorded 45/45 neutral checks, 10/10 endpoint checks,
49/49 PostgreSQL checks and an 850-test/16-project gate. Those totals are historical
only and are superseded for current local qualification by the final run above.
Provider configuration must be qualified with non-sensitive fixture bytes before
OPS-007/OPS-008 can be accepted for live use.

Changes to normalization, CSV/plan/decision compatibility, row identity,
claim/fence/retry/discovery/drain semantics, authority revision, mutable reference owners, runtime
grants, PII retention, or PostgreSQL/runtime version require requalification of
the corresponding guard and normal repository gate. Generic ETL, automatic merge,
global signal uniqueness, exhaustive fuzzy discovery,
identity-provider provisioning and historical debtor rewriting remain
`NOT_INTRODUCED`.

### Import acceptance serialization evidence (2026-10-08)

Row-level duplicate rejection was a check-then-insert pair with no serialization:
`HasCurrentImportDuplicate` is a bare `SELECT EXISTS(...)`, `customers.individuals`
carries no unique index on the normalized signals, `LockImport` orders only per
`(tenant_id, import_id)`, and the statement-level database gates cover individual
**updates** rather than inserts. A concurrent manual create of the same normalized
email could therefore commit between the check and the insert and the row would be
imported anyway. `ProcessNextImportRowAsync` now holds the existing tenant
canonicalization advisory lock across that check and its insert, and
`CreateIndividualAsync` takes the same lock, so acceptance and manual create share
one serialization point. No new advisory key, transaction boundary, retry policy or
unique constraint was introduced; no migration was changed.

Permanent guards are `DuplicateAppearingAfterAcceptanceRejectsTheImplicitlyNewRowInsteadOfInsertingASecondOne`
(concurrent manual create paused while holding the gate; the import then rejects the
row as `new_duplicate_requires_new_plan`, exactly one customer carries that
normalized email) and `ManualCreateWaitsBehindImportAcceptanceGateWithoutImposingNormalizedEmailUniqueness`
(reverse ordering; the create is observed waiting on the canonicalization lock and
two customers may legitimately share a normalized email). Both were confirmed to
fail against the pre-fix adapter.

Commands inspected for this delta:

```sh
dotnet build Application.slnx --configuration Release
dotnet test tests/integration/Application.Customers.Postgres.Tests/Application.Customers.Postgres.Tests.csproj --nologo
```

The build reported 0 warnings/0 errors and the real PostgreSQL suite passed
**53/53**, zero skipped. Only that suite and the solution build were run here; no
whole-repository `./eng/verify.sh` gate and no other test project is claimed by this
delta. Changing the gate key/order, the row-level check, individual create or any
requalification trigger above requires rerunning these guards.

Known non-claims: canonical uniqueness, automatic merge and cross-signal duplicate
detection remain `NOT_INTRODUCED`, and trusted direct maintenance writers that
bypass the adapter still bypass this application-level gate.
### Admission and dependency-failure fixes

Three defects in the accepted path were corrected together because each one left a
declared guarantee unenforced at the host boundary:

1. Real object storage could be enabled while the only deleter stayed at its safe
   default, so expiring raw customer PII accumulated with no remover.
2. The protected run-batch command was ungated by the executor admission state.
3. An authorization-provider outage during acceptance or run-batch returned a
   generic `500` on routes that declare `503`.

Permanent focused guards are `ObjectStorageCompositionRequiresTheImportExecutorThatDeletesRawSources`,
`ImportExecutionReadinessReportsTheDeleterAdmissionStateOnlyWhenEnabled`,
`RunBatchRefusesWorkWhileTheExecutorDoesNotAdmitItBeforeParsingOrClaiming`,
`RunBatchReportsAuthorityDeniedWithoutProcessingAnyRow`,
`RunBatchRunsOneBoundedBatchWhileTheExecutorAdmitsWork`,
`AuthorizationProviderOutageOnAcceptReturnsServiceUnavailableNotInternalError`,
`AuthorizationProviderOutageOnRunBatchReturnsServiceUnavailableNotInternalError` and
`ImportTemplateIsRefusedWithoutCurrentImportPermission`,
`ImportTemplateReturnsTheCanonicalCsvToCurrentImportAuthority`, all in
`CustomerDuplicateImportEndpointTests`, plus the retained executor/hosted-service
suites for admission and drain semantics. Known non-claims: these guards prove host
admission, failure mapping and composition; they do not prove live Hugging Face
delete behavior, provider-side lifecycle or cross-replica deletion fairness, and the
readiness check asserts the admission signal rather than a provider round trip.
Requalify on any change to the object-storage/execution configuration link, the
run-batch route mapping or ordering, the `import_executor_unavailable` /
`authorization_unavailable` problem codes, or the readiness check set.
