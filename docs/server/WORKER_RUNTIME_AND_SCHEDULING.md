# Worker Runtime, Durable Jobs, and Scheduling

**Status:** Accepted implementation direction for the first `SquiFlow.Worker` slice.  
**Applies from:** v0.0.18 architecture baseline; executable creation remains Phase 6.  
**Owner boundary:** This document owns the selected Worker execution runtime and scheduler mechanics. `docs/server/CORE_API_AND_WORKER.md` remains the owner of API/Worker responsibility, durable-job semantics, command/event distinction, retries, external effects, and authorization classes. `docs/decisions/DUAL_PROCESSING_AND_IN_PROCESS_COORDINATION.md` remains the owner of Workstation/server dual processing and process-local Channel semantics.

## 1. Decision

SquiFlow selects:

- **Proto.Actor for the initial in-process execution/supervision runtime inside `services/worker`;**
- **Quartz.NET 4.x as the initial durable scheduling engine;**
- **PostgreSQL as durable authority for SquiFlow jobs, schedule definitions, scheduled occurrences, attempts/results and outbox state;**
- Quartz persistent scheduling state in PostgreSQL when Phase 6 implements the first scheduled workload;
- one local Proto.Actor `ActorSystem` inside the Worker process initially;
- no Proto.Cluster, Proto.Remote, Proto.Persistence/event-sourced actor state, Hangfire processing server, TickerQ execution runtime, MassTransit, RabbitMQ, Kafka, or generic event bus as baseline.

Exact package patch versions are pinned when Phase 6 implementation begins and are not architecture vocabulary.

The responsibility split is:

```text
SquiFlow schedule definition
= what business/operational schedule exists and what it means

Quartz.NET
= when the schedule becomes due and how trigger/calendar/misfire mechanics are calculated

SquiFlow scheduled occurrence + DurableJob in PostgreSQL
= durable evidence that a particular intended occurrence/work item exists

Proto.Actor
= in-process dispatch, serialized actor execution, workload isolation and supervision

SquiFlow module handler
= what the business/background operation actually means
```

## 2. Why Proto.Actor is selected for Worker execution

`BackgroundService` remains the .NET host/lifetime primitive, but the Worker is expected to contain several independent workload classes with different concurrency, supervision, resource and ordering needs.

Proto.Actor is selected because it gives the Worker a focused in-process model for:

- sequential message processing per actor;
- explicit actor ownership of local execution state;
- supervision hierarchies;
- message-driven workload dispatch;
- dispatcher isolation when a measured workload needs it;
- lightweight actor lifecycles without turning modules into network services.

This does **not** mean every helper method or maintenance operation becomes an actor. Actors are used where state ownership, serialized concurrency, supervision or message-driven workload isolation provides a concrete benefit.

Proto.Actor remains an implementation/runtime dependency of the Worker. Domain/application contracts do not depend on Proto.Actor types.

## 3. Actor mailboxes are not durable queues

Proto.Actor mailbox contents are process memory and must not become authoritative durable work state.

The default Proto.Actor mailbox is not accepted as an unbounded durable backlog.

For durable work:

```text
PostgreSQL DurableJob exists first
→ bounded claim/admission
→ Worker sends a small execution message, normally referencing JobId
→ actor processes
→ durable result/retry/quarantine state is persisted
```

If the Worker crashes after the job exists but before the actor receives the message, startup/periodic reconciliation discovers the durable job again.

If an actor mailbox rejects/loses a process-local dispatch, the durable job remains recoverable. Mailbox loss may delay execution; it must not delete accepted work.

The exact mailbox implementation/capacity and workload-specific dispatcher settings are measured in the Phase-6 proof. Do not allow an unbounded in-memory backlog to substitute for PostgreSQL admission/claim bounds.

## 4. Supervision is not business retry

Proto.Actor supervision handles actor/process execution failure. It does not by itself decide whether a business/background effect is safe to repeat.

Keep these separate:

```text
actor restart/stop/resume/escalate
= runtime recovery decision

durable RetryScheduled / Failed / Quarantined / OutcomeUnknown
= SquiFlow job/effect correctness decision
```

Example:

```text
actor sends provider request
→ provider may have succeeded
→ actor crashes before local completion commit
```

A supervisor restart must not blindly repeat the external effect. The durable job enters the existing idempotency/reconciliation/`OutcomeUnknown` path when the provider outcome is ambiguous.

Supervision policy therefore cannot replace:

- semantic idempotency;
- provider idempotency/reference keys;
- retry classification;
- no-progress detection;
- poison-work quarantine;
- reconciliation;
- durable completion evidence.

## 5. Initial actor topology

Do not build one actor type per entity or a giant permanent actor tree in advance.

A plausible first shape is:

```text
WorkerRoot
├── DurableWorkSupervisor
│   ├── Document/CPU workload actors only when implemented
│   ├── Integration actors only when implemented
│   └── Reconciliation actors only when implemented
└── SchedulerIntegration
```

The exact topology follows the first real workloads.

Prefer a small number of workload actors/pools/partitioned actors over millions of long-lived entity actors unless measured state/ordering requirements prove that finer identity is valuable.

## 6. Dispatcher and resource policy

Proto.Actor's normal ThreadPool-backed dispatcher is the initial baseline for ordinary asynchronous I/O-bound handlers.

Do not create dedicated dispatchers merely because Proto.Actor supports them.

Introduce a separate dispatcher/execution pool only when a workload is proven to need isolation, for example:

- CPU-heavy image/PDF transformation;
- unavoidable blocking native/driver code;
- strict isolation from latency-sensitive integration/reconciliation work.

Rules:

- use asynchronous provider/database APIs for I/O rather than blocking actor threads;
- bound CPU-heavy concurrency independently from I/O concurrency;
- do not allow one workload class or tenant to monopolize the shared Worker;
- measure queue age, execution time, actor restarts, resource saturation and admission delay before tuning throughput/dispatcher settings.

## 7. Quartz.NET is the scheduling engine, not the Worker

Quartz.NET is selected because SquiFlow needs scheduling semantics that grow beyond a process-local timer:

- durable trigger state;
- cron/simple/calendar/recurrence schedules;
- explicit timezone support;
- misfire semantics;
- restart recovery;
- later clustering support if multiple scheduler nodes are ever justified;
- PostgreSQL persistent-store support.

Quartz does **not** execute SquiFlow business/background handlers directly as the final effect owner.

The normal scheduled path is:

```text
Quartz trigger becomes due
→ small SquiFlow Quartz adapter executes
→ validate/reconcile published SquiFlow schedule revision
→ atomically materialize or find ScheduledOccurrence + DurableJob
→ commit
→ make durable work discoverable to Proto.Actor dispatch
→ return from Quartz job
```

The actor/handler later executes the durable job.

This keeps `Quartz job succeeded` distinct from `business/background consequence succeeded`.

## 8. SquiFlow schedule meaning versus Quartz trigger state

SquiFlow owns the business/operational schedule definition. Quartz owns technical trigger calculation/execution state.

A SquiFlow schedule definition retains the smallest information required to explain/rebuild the schedule, such as:

```text
ScheduleId
OwnerModule / JobKind
Tenant/platform scope
Enabled
ScheduleRevision
Recurrence/trigger intent
Business timezone where material
Misfire policy
Overlap policy
Payload/config reference
PublishedAt / audit identity
Quartz trigger identity/version mapping
```

Quartz tables are infrastructure state. They are not the only copy of the business meaning of a tenant/platform schedule.

Publishing/changing/disabling a schedule updates the SquiFlow schedule revision and reconciles the derived Quartz trigger deliberately. If Quartz trigger state is lost/corrupt while SquiFlow schedule state is intact, the supported recovery path can rebuild/reconcile the technical triggers rather than inventing business meaning from Quartz tables.

Do not let tenant-facing code write Quartz tables directly.

## 9. Durable scheduled occurrence

A trigger firing is not itself durable business work.

Before the actual handler executes, create/find a stable occurrence identity and durable job.

Conceptually:

```text
ScheduleId
+ ScheduleRevision
+ IntendedFireIdentity/Instant
= stable occurrence identity
```

The exact key representation is implementation-specific, but database uniqueness/idempotency must prevent two scheduler firings from silently creating duplicate semantic jobs.

For one intended occurrence:

```text
Quartz fires twice
or scheduler restarts
or two scheduler nodes later race
→ one semantic ScheduledOccurrence
→ one semantic DurableJob/effect identity
```

## 10. Misfire policy

Do not expose raw Quartz misfire enum values as SquiFlow product/domain vocabulary.

SquiFlow owns semantic policies such as:

```text
RunOnceAfterRecovery
SkipMissed
BoundedCatchUp   (only when every missed occurrence genuinely matters)
```

The Quartz adapter maps those semantics to the supported trigger family and verifies behavior.

Never let a long outage create an unbounded catch-up storm.

A bounded catch-up policy declares maximum occurrences/time horizon/admission behavior.

## 11. Overlap policy

Every recurring workload whose previous occurrence can still be running defines its overlap semantics.

Possible SquiFlow policies include:

```text
NoOverlap
AllowOverlap
Coalesce
```

Do not infer overlap safety from Quartz or actor serialization alone.

For operations such as reconciliation/maintenance, `NoOverlap` is the expected ordinary default unless the workload proves parallel occurrences are safe and useful.

The exact policy belongs to the schedule/job definition and durable claim rules.

## 12. Timezone and clock semantics

Distinguish:

```text
technical elapsed/UTC schedule
```

from:

```text
business-local recurring schedule
```

A business schedule such as `08:00 local business time` retains the relevant timezone/rule semantics so DST and timezone changes can be handled deliberately.

Quartz calculates trigger times using the published timezone/recurrence intent, but SquiFlow retains the schedule meaning and occurrence identity needed for audit/recovery.

Wall-clock time does not replace idempotency, fencing, sequence/version or lease evidence.

## 13. Manual run, pause, retry and schedule controls

Normal operational/platform controls flow through:

```text
Platform Admin Web
→ Admin API
→ authorization/risk/audit
→ versioned SquiFlow schedule/job command
→ Quartz/job reconciliation where applicable
```

Do not expose a Quartz dashboard, Quartz HTTP API or direct Quartz database mutation as a normal platform-control bypass.

A future diagnostic/admin view may display Quartz state through an internal adapter, but material commands still pass through Admin API and SquiFlow authority.

A manual `Run now` creates an explicit auditable occurrence/job identity; it does not pretend to be the original scheduled occurrence unless the command intentionally performs misfire recovery for that occurrence.

## 14. Quartz persistence and schema operations

Use Quartz's persistent ADO job store with PostgreSQL when scheduled work is implemented.

Exact table prefix/schema/connection/pool settings are Phase-6 implementation details.

Production schema changes are applied through the same version-controlled migration/release discipline as other infrastructure schema. Do not grant broad runtime DDL merely to call automatic schema provisioning in production without an explicit operational reason.

Quartz persistence is operational scheduler state; normal SquiFlow business backup/restore/rebuild procedures must document whether Quartz tables are restored or rebuilt/reconciled from SquiFlow schedule state for the selected recovery scenario.

## 15. Quartz and SquiFlow retry are separate

Quartz may retry/re-fire trigger execution if the small scheduling adapter fails before durable occurrence materialization.

After `ScheduledOccurrence + DurableJob` is durably committed, SquiFlow's durable-job retry lifecycle owns the business/background execution.

Do not stack:

```text
Quartz retry
× actor restart
× durable job retry
× provider SDK retry
```

without one explicit retry owner per failure boundary.

The goal is bounded recovery, not retry multiplication.

## 16. Channels after Proto.Actor selection

`System.Threading.Channels` remains accepted for lightweight process-local coordination where it fits, especially Workstation sync wake-up/backpressure.

Inside `SquiFlow.Worker`, Proto.Actor becomes the primary workload dispatch/supervision runtime.

Do **not** introduce:

```text
Channel<DurableJob>
→ generic dispatcher
→ Proto.Actor mailbox
```

merely to stack two in-memory queues for the same responsibility.

A Channel may still be used for a distinct local mechanism if measurement/implementation makes that responsibility clearer, but durable job correctness never depends on either Channel or actor mailbox contents.

## 17. Proto.Persistence is not baseline

Do not use Proto.Persistence/event sourcing/snapshot journals for ordinary SquiFlow business or durable-job authority.

SquiFlow already uses PostgreSQL current state + explicit business history/outbox/job records. Introducing actor journals would create another persistent authority and event-sourcing lifecycle without a demonstrated need.

Actor-local runtime state should normally be reconstructable from durable SquiFlow state.

Revisit Proto.Persistence only for a concrete actor-owned state machine where its recovery model is demonstrably simpler/safer than the existing durable state model and does not create competing business authority.

## 18. Proto.Remote and Proto.Cluster are not baseline

The first Worker uses one local ActorSystem.

Do not enable Proto.Remote/Proto.Cluster merely because the framework supports distribution.

Revisit when evidence shows a real need such as:

- multiple Worker nodes/processes need actor placement/identity beyond simple durable-job competing claims;
- a workload genuinely benefits from long-lived distributed actor identity/state locality;
- cross-node message-driven coordination is simpler/safer than database-backed work ownership;
- the additional gRPC/membership/partition/failure/upgrade/operations surface is justified on the real deployment.

Quartz clustering is a separate decision from Proto.Cluster. One does not imply the other.

## 19. Graceful shutdown and restart

Worker shutdown must preserve durable truth:

```text
stop accepting/materializing new local dispatch where practical
→ stop/standby scheduler according to selected host lifecycle
→ stop new durable claims
→ allow a bounded drain window
→ persist completed/retry/outcome-unknown state
→ stop ActorSystem
→ let uncompleted leases expire/reconcile
```

Never mark a durable job complete because an actor/message was merely accepted into memory.

Killing the Worker at any point must leave the database sufficient to determine pending/running/lease-expired/reconciliation work after restart.

## 20. Health and observability

Expose enough evidence to distinguish:

- Worker process healthy but backlog growing;
- scheduler healthy but trigger/materialization lagging;
- Quartz persistence unavailable;
- durable job store unavailable;
- actor restart/crash loop;
- dead letters/mailbox/admission pressure;
- oldest pending/claimed/retry job age;
- schedule misfires and bounded catch-up;
- quarantine/poison work;
- provider/dependency latency/failure;
- CPU-heavy workload saturation;
- tenant/workload fairness pressure.

Liveness must not claim the Worker is useful merely because the process is alive. Readiness/functional health should account for dependencies required by the selected workload.

Telemetry remains non-authoritative; job/schedule/business state remains in durable SquiFlow/Quartz stores according to the boundary above.

## 21. Security and tenant isolation

Actors do not create an alternate authorization/isolation model.

Durable jobs retain the tenant/platform scope, originating semantic identity, correlation/causation, handler/payload version and authorization class required by `CORE_API_AND_WORKER.md`.

A Worker actor never trusts a tenant identifier solely because an in-memory message contains it. It loads/operates through the same scoped module/persistence contracts and tenant isolation rules as the accepted server architecture.

Proto.Actor messages and Quartz job data must not carry reusable credentials/secrets when an identifier/reference is sufficient.

## 22. Alternatives considered

### TickerQ

Positive lightweight candidate with PostgreSQL/EF Core persistence and modern scheduling/job features.

Not selected because its job execution/retry/runtime responsibilities overlap more directly with the selected Proto.Actor + SquiFlow durable-job model. Revisit if Quartz proves disproportionately complex and the first workload would benefit from TickerQ replacing, rather than duplicating, part of the Worker execution model.

### Hangfire

Not selected for the Proto.Actor-based Worker because Hangfire intentionally owns persistent enqueueing, recurring/delayed scheduling, worker processing, retries and dashboard concerns. Running Hangfire Server beside Proto.Actor and SquiFlow DurableJob would create competing queues/retry/execution authorities.

Hangfire would make more sense only under a future superseding decision where Hangfire itself replaces the current Worker execution model.

### Custom `PeriodicTimer`/DB scheduler

Still appropriate for trivial internal maintenance timers, but not selected as SquiFlow's primary durable scheduling engine because accepted requirements already include restart recovery, durable trigger state, timezones, misfires, overlap, schedule revisions and future multi-node safety. Reimplementing those semantics would create avoidable scheduler-specific code.

### MassTransit / RabbitMQ / Kafka

Remain deferred delivery/broker/stream candidates. Proto.Actor + Quartz does not justify them. Reopen only under the explicit broker/stream triggers already recorded in the repository.

## 23. Adjacent Worker technology/status audit

The following surrounding choices are now explicit so Phase 6 does not accidentally add another framework for an already-owned responsibility:

| Concern | Initial direction |
|---|---|
| Worker host/lifetime | .NET Generic Host / `BackgroundService` integration |
| Work dispatch/supervision | Proto.Actor local ActorSystem |
| Durable job/outbox authority | PostgreSQL SquiFlow tables/state |
| Durable scheduling mechanics | Quartz.NET 4.x persistent store on PostgreSQL |
| Business schedule meaning | SquiFlow versioned schedule definition |
| Scheduled occurrence truth | SquiFlow durable unique occurrence + job |
| Workstation local wake/backpressure | bounded `System.Threading.Channels` |
| Generic Worker Channel queue | not baseline beside Proto.Actor |
| Actor persistence/event sourcing | not baseline |
| Actor remoting/clustering | not baseline |
| Quartz clustering | not baseline initially; independent future trigger |
| Broker/event bus | not baseline |
| Workflow/Saga engine | not baseline; explicit module state machine first |
| Generic distributed lock service | not baseline; use DB constraints/leases/fencing where needed |
| Background admin/dashboard | SquiFlow Admin Web → Admin API; no direct framework bypass |
| Dead-letter/poison authority | durable SquiFlow quarantine/reconciliation state, not only actor dead letters |
| CPU-heavy isolation | bounded workload-specific dispatcher/pool only when measured |
| I/O execution | async APIs; no thread-blocking wrapper as default |
| Retry | finite, classified, one owner per boundary |
| Graceful drain | bounded, durable-state-first, lease/recovery aware |

## 24. Phase-6 proof obligations

Before accepting the Worker/scheduler implementation, prove at least:

- one durable job survives Worker termination before actor dispatch;
- one durable job survives actor failure during execution;
- actor restart does not silently duplicate an external semantic effect;
- durable job retry and supervisor restart are observably distinct;
- actor mailbox/admission cannot grow without bound under a reconnect/batch burst;
- one scheduled occurrence survives Worker/Quartz restart;
- duplicate Quartz firing/race produces one semantic occurrence/job;
- selected misfire policies behave correctly after a long outage;
- `NoOverlap`/other selected overlap policy is enforced by durable semantics;
- timezone/DST tests exist for every business-local schedule type implemented;
- schedule edit/disable/re-enable uses explicit revisions and reconciles Quartz state;
- Quartz persistence loss/corruption has a documented restore/reconcile path;
- Worker shutdown does not acknowledge unfinished durable jobs merely because actors were stopped;
- one tenant/workload cannot starve all other durable work under the selected fairness policy;
- direct Quartz/Proto.Actor framework types do not leak into domain/application contracts;
- Admin API remains the privileged control surface for pause/retry/quarantine/reconcile/schedule changes;
- no Proto.Cluster/Remote/Persistence, broker, Hangfire/TickerQ server, or second durable queue exists without a separately approved workload decision.

## 25. Revisit triggers

Revisit this decision when evidence demonstrates one of the following:

- Quartz scheduling overhead/operability is materially higher than the first workload justifies;
- Proto.Actor adds more complexity than it removes for actual Worker workloads;
- Worker throughput/resource measurements require different dispatcher/actor partitioning;
- several Worker nodes make Quartz clustering or a different scheduler materially safer;
- distributed actor identity/state makes Proto.Cluster materially simpler than DB-backed claims;
- a broker is needed for independent durable consumers/routing/replay/throughput/cross-node coordination;
- a workflow engine is needed for long-running orchestration whose state/compensation/visibility no longer fits explicit SquiFlow durable state machines economically;
- dependency licensing/maintenance/security/support changes materially alter the trade-off.

A revisit changes only the mechanism whose responsibility is no longer well served; it must preserve SquiFlow durable authority, idempotency, tenant isolation, audit and recovery contracts unless a separate material decision explicitly supersedes them.

## Source basis

Primary/current references consulted for the selection:

- Proto.Actor overview and actor model: https://proto.actor/protoactor/what-is-protoactor/
- Proto.Actor mailboxes: https://proto.actor/protoactor/mailboxes/
- Proto.Actor dispatchers: https://proto.actor/protoactor/dispatchers/
- Proto.Actor failure/supervision: https://proto.actor/protoactor/messages/failure/
- Proto.Actor durability caveats: https://proto.actor/protoactor/durability/
- Proto.Actor persistence: https://proto.actor/protoactor/persistence-proto-persistence/
- Proto.Cluster: https://proto.actor/protoactor/cluster/
- Quartz.NET 4.x documentation: https://www.quartz-scheduler.net/documentation/quartz-4.x/
- Quartz.NET 4 persistent-store/clustering configuration: https://www.quartz-scheduler.net/documentation/quartz-4.x/configuration/reference
- Quartz.NET recurrence/timezone/misfire behavior: https://www.quartz-scheduler.net/documentation/quartz-4.x/tutorial/recurrencetrigger.html
- Quartz.NET cron misfire behavior: https://www.quartz-scheduler.net/documentation/quartz-4.x/tutorial/crontriggers.html
- TickerQ documentation: https://tickerq.net/docs/what-is-tickerq
- Hangfire feature model: https://www.hangfire.io/features.html
