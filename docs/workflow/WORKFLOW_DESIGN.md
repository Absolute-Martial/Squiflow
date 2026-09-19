# Workflow Design and Continuation

**Version:** v0.0.20

**Status:** Accepted workflow semantics; workflow runtime and configurable workflow implementation remain `NOT_INTRODUCED`.

Design workflows from the user's continuation journey, not from a diagram alone.

## 1. Continuation-first questions

For every non-terminal state answer:

1. Who can continue it?
2. Where do they discover it?
3. What action continues it?
4. What data is required?
5. What if nobody acts?
6. Is there a deadline/escalation?
7. Can it be reassigned/delegated?
8. Can it be cancelled, or must a compensating action be created?
9. What if two actors act simultaneously?
10. What if permission, underlying entity, rule version, form version or workflow version changes?
11. How can the user/support understand why the item is here?

A state without a meaningful continuation owner/reason may be a field/status rather than a workflow state.

## 2. Small-team usability and deadlock avoidance

A tenant with only Owner + Staff must not need a complicated BPM suite.

Start with simple stage/transition concepts and allow the Owner to configure only what they need.

Example:

```text
Order accepted
→ WaitingForArtwork
→ DesignInProgress
→ WaitingForCustomerApproval
→ ReadyForPrint
→ Completed
```

The Owner may keep the default workflow or add/remove tenant stages through Web Settings.

Every approval/escalation design must ask what happens if the only eligible approver is unavailable, suspended or is the same person who initiated the work. Do not create a configuration that permanently strands work in a two-person tenant without a deliberate fallback/reassignment/Owner recovery policy.

## 3. Canonical state versus tenant workflow stage

Use configurable tenant workflow stages alongside protected canonical business states.

Do not let a custom stage redefine:
- payment success/refund/reversal;
- posted/issued financial truth;
- stock movement truth;
- tenant/security/session state.

## 4. Web-only authoring/publication

Workflow definitions, transitions, stage creation, role/permission mapping, dynamic form binding and publication are changed through Web administration only.

The Workstation:
- receives a published compatible effective workflow/form snapshot;
- can execute allowed local transitions while offline only if the transition is explicitly local-capable;
- cannot create or publish workflow definitions.

## 5. Permission, rule and fact-authority integration

A transition combines:

```text
permission
+ current canonical state
+ current workflow stage/version
+ transition guard/rule
+ required fact authority/freshness
+ concurrency version
```

Example:

```text
permission: quotes.approve
stage/state: Submitted
transition: Submitted → Approved
required facts: current approval/risk facts
```

A transition is not local-capable merely because its rule definition is cached locally.

If a guard depends on a `ServerRequired` fact such as current shared credit exposure, security state or other centrally owned truth, the Workstation must either keep the transition provisional or require connectivity/server authority.

Rule fact classifications come from `docs/rules/NATIVE_RULE_ENGINE.md`.

## 6. Human work discovery

Human work should be discoverable through an appropriate work inbox rather than requiring users to rediscover the original record.

Examples:
- Needs my approval;
- Needs information;
- Assigned to me;
- Due soon;
- Overdue.

For a two-person tenant this can be a compact list inside Web/Workstation rather than a separate enterprise task application.

External email/SMS is optional notification, not the ownership mechanism. If notifications fail, the work must remain discoverable in SquiFlow.

## 7. Waiting and asynchronous continuation

A workflow may wait for:
- human action;
- deadline/time;
- external callback;
- Worker completion.

Waiting is durable state. It is not represented only by an in-memory timer.

Every durable wait defines an idempotent wake/transition behavior so duplicate timer/callback/Worker completion cannot advance the workflow twice.

## 8. Orchestration levels

Do not make one general workflow runtime own every kind of sequencing.

```text
one synchronous business operation
→ capability application/domain code

independent consequence of a committed fact
→ transactional outbox + independently owned DurableJob

small entity lifecycle or tenant stage progression
→ explicit capability-owned state machine

coordinated multi-step process with durable waits
→ explicit capability-owned durable process state first

many reusable long-running definitions, branches, durable joins,
callbacks, compensations and operator recovery paths
→ qualify a workflow engine
```

One action causing another does not by itself earn a general workflow engine. The deciding question is whether the later action is an independent consequence, the next state of one owned process, or part of orchestration complex enough that a dedicated runtime is safer and cheaper than explicit SquiFlow state.

## 9. Typed follow-on actions and durable continuation

A published workflow transition may declare a bounded set of SquiFlow-owned, typed follow-on actions when a real capability requires them. Examples include:

- create or complete a human work item;
- request a named capability command;
- create a durable background job;
- create a deadline/escalation wait;
- record an outbox fact for an independent consequence;
- start or signal a specifically supported child process.

These actions are product contracts, not arbitrary tenant C#/JavaScript/SQL and not direct Quartz, Proto.Actor or provider calls.

For a coordinated process, advancing a step must atomically persist enough authoritative state to recover after a crash:

```text
expected process instance + definition version
→ validate permission, guard and current facts
→ commit transition/result
→ persist next durable intent, wait or outbox fact
→ dispatch only after commit
```

Every continuation has a stable process/step/occurrence identity and idempotency rule. A duplicate callback, timer, Worker result or human command must return the existing semantic result or a clear stale/conflict outcome; it must not advance the process twice.

Do not implement important chains as actor-to-actor sends, Quartz job chaining, process-local events or nested fire-and-forget calls. Those may wake execution after durable state exists, but they are never the continuation authority.

Automatic follow-on transitions require explicit loop and resource protection. The implemented scope must bound fan-out, repeated transitions, elapsed execution, payload size and no-progress cycles. Compensation remains a named business operation; reversing an orchestration step does not imply that an external effect was undone.

## 10. Workflow-engine-derived semantics and adoption boundary

SquiFlow should learn from Elsa and Temporal without recreating or embedding either platform prematurely. The native baseline may adopt these semantics in SquiFlow-owned terms where a real workflow earns them:

- a durable wait/bookmark identity;
- correlation between a process instance and external callbacks or child work;
- immutable definition-version pinning;
- transactionally committed continuation/outbox records;
- explicit suspended, completed, cancelled and incident/fault outcomes;
- bounded scanning and recovery of interrupted instances;
- operator-visible current step, reason, history and available recovery action.

This does not select a workflow engine as a runtime and does not authorize a generic activity SDK, visual canvas, dynamic expression language, general HTTP trigger surface, parallel execution engine, separate workflow service or duplicate scheduler/job authority.

Elsa and Temporal solve different dominant problems:

| Concern | Elsa fit | Temporal fit |
|---|---|---|
| Definition model | programmatic plus persisted/designed definitions | developer-authored deterministic workflow code |
| Runtime shape | embeddable .NET runtime or separate Elsa server | separate Temporal Service plus SquiFlow Worker processes |
| Durability model | persisted instances, bookmarks, inbox/outbox and execution records | event history, deterministic replay, task queues, durable timers and signals |
| Tenant-facing configurable workflows | closer fit, subject to SquiFlow validation and authority boundaries | requires a SquiFlow definition interpreter or generated/deployed workflow code |
| Code-first cross-system orchestration | capable | strongest fit when the separate control plane is justified |
| Operations | can share the .NET/PostgreSQL application footprint | service, namespace, persistence, visibility, security, monitoring and server/schema upgrade lifecycle |

Elsa becomes a package candidate when a concrete long-running process demonstrates several of the following and explicit SquiFlow process state is no longer economical:

- many durable steps lasting hours, days or weeks;
- multiple external signals and correlated waits;
- durable branching, fan-in or nested processes;
- compensation across already completed effects;
- reusable tenant-authored definitions beyond bounded stages/actions;
- operator-driven resume, alteration or incident recovery;
- orchestration persistence/recovery code becoming materially harder to test and operate than the engine.

Before adoption, a PostgreSQL proof must establish atomic persistence boundaries, tenant isolation, duplicate-wake behavior, definition pinning, cancellation/compensation, interrupted-instance recovery, upgrade compatibility, observability and coexistence with SquiFlow DurableJob/Quartz/Proto.Actor responsibilities. Elsa would coordinate SquiFlow commands; it would not own payment, stock, security, authorization, rule/fact authority or irreversible-effect meaning.

Temporal becomes a candidate when the dominant problem is developer-authored, mission-critical orchestration with long durable execution, many signals/timers/child workflows, deployment-safe workflow evolution and recovery across repeated process/host failure, and when operating a separate orchestration service is justified.

Temporal adoption is a replacement decision for each admitted process class:

```text
SquiFlow business transaction + outbox/start intent
→ idempotent Temporal Workflow start using a stable Workflow ID
→ Temporal owns orchestration history, task queues, timers and workflow retries
→ SquiFlow Temporal Activities invoke typed capability commands
→ SquiFlow PostgreSQL remains business authority
```

For that process class, do not also use Quartz chaining, SquiFlow DurableJob orchestration or Proto.Actor mailboxes as competing workflow authority. Those mechanisms may remain for unrelated non-Temporal workloads only when their ownership is explicit.

A Temporal proof must cover the business-transaction-to-Workflow-start gap, stable Workflow IDs and duplicate starts, deterministic replay tests, worker/workflow deployment versioning, activity idempotency and `OutcomeUnknown`, tenant isolation, payload encryption/redaction, history/retention limits, server and schema upgrades, backup/recovery, observability, and supported operation on the intended rack topology. Temporal event history is orchestration authority; it does not replace SquiFlow's payment, stock, security, authorization, rule/fact or audit authority.

## 11. Cancellation/correction

Each state must classify whether cancellation is:
- immediately safe;
- safe only before an irreversible effect;
- compensation/reversal required;
- not allowed.

Do not show `Cancel` when the real business action is a refund, reversal or corrective document.

## 12. Versioning and active instances

Active workflow instances remain pinned to their definition version unless an explicit migration exists.

Do not silently apply a new transition graph to old in-progress work.

The same principle applies to bound dynamic-form definitions/rule versions where the historical instance must remain explainable.

## 13. Stage rename/removal/retirement

A tenant may rename or remove a stage from the **future published definition**, but old instances/audit history may still reference the old stage/version.

Therefore:
- published definitions/stage identifiers are immutable/versioned;
- a renamed stage in a new definition does not rewrite old instance history;
- retired stages remain renderable by historical label/version metadata;
- deleting a draft stage is allowed only when no published/instance history depends on it, or it becomes a retirement rather than physical deletion;
- migrations of active instances are explicit, tested and audited.

This prevents `Unknown stage 17` after an Owner simplifies a workflow.

## 14. Concurrent actors

Two actors may act simultaneously. A transition must use expected state/version and idempotency so only a valid transition commits.

If one user approves while another cancels/rejects, the loser receives a stale/conflict result and reloads the current workflow state.

## 15. Definition completeness

A workflow definition is incomplete if any non-terminal path lacks:
- continuation actor/discovery;
- required form/data;
- permission/rule/fact authority;
- deadline/no-action behavior where relevant;
- retry/idempotency for async wakeups;
- cancellation/compensation meaning;
- definition/form/rule version explanation;
- conflict and recovery UX;
- historical rendering after configuration changes.
