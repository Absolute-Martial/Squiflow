# Technology Fit and Usage Review Rule

**Status:** Authoritative review-method refinement for the exhaustive ByteByteGo 308-page study.

## Why this rule exists

Comparison articles such as `REST vs GraphQL`, `Redis vs Memcached`, `Docker vs Kubernetes`, `Kafka vs RabbitMQ`, or `JWT vs PASETO` must **not** be treated as winner/loser decisions for SquiFlow.

A comparison is evidence about capabilities, trade-offs, and failure/operational characteristics. The SquiFlow architecture decision is made separately from the real SquiFlow boundary, workload, constraints, and accepted invariants.

The correct reasoning is:

```text
source comparison
    -> what each option is good at
    -> what each option costs / makes harder
    -> what SquiFlow actually needs at this boundary
    -> what SquiFlow already uses/plans here and why
    -> whether one option, several complementary options, or neither is justified
    -> evidence / POC / adoption gate where needed
```

Not:

```text
A vs B article
    -> declare one universal winner
    -> use it everywhere
```

## Required questions for every technology/pattern exposure

In addition to the SOURCE / INFERENCE / EXTERNAL KNOWLEDGE sections already required, every future technology-oriented review must answer:

1. **What is it?** What problem class does the technology/pattern solve?
2. **Where does it excel?** Which workloads/boundaries benefit from its specific strengths?
3. **Where is it weaker or more expensive?** Include correctness, security, compatibility, debugging, deployment, capacity, and operations costs.
4. **What does SquiFlow currently use at the corresponding boundary?** Do not infer only from the comparison article; inspect the current project architecture/docs/code where relevant.
5. **Why are we using the current mechanism?** Record the concrete reason, not merely `baseline` or `deferred`.
6. **Exactly where do we use it or plan to use it?** Name the runtime/surface/flow, such as Workstation sync, tenant Web reads, Admin dashboards, external partner API, durable Worker delivery, identity/session, cache, database, or deployment.
7. **What does that technology buy us on that exact surface?** Record the specific benefit: simpler business command semantics, read composition, streaming, binary efficiency, provider interoperability, durable routing, operational recovery, etc.
8. **Could another technology improve a different SquiFlow surface?** Name the actual surface/use case rather than making a global technology judgment.
9. **Can technologies be complementary?** Mixed protocol/data/deployment choices are allowed when boundaries genuinely differ.
10. **What evidence would justify adopting it?** Measurement, POC, client requirement, scale trigger, failure requirement, or operational need.
11. **What would remain unchanged if it were adopted?** For example, GraphQL does not replace authorization; gRPC does not replace sync/idempotency; RabbitMQ does not replace semantic idempotency.
12. **What is the current status?** Use wording such as `USED`, `PLANNED CANDIDATE`, `FIT FOR SPECIFIC SURFACE`, `NOT CURRENTLY NEEDED`, `NEEDS MEASUREMENT`, or `REJECTED FOR A SPECIFIC REASON` rather than treating `not baseline` as a permanent ban.

The review should be able to answer this sentence for every important selected technology:

> **We use `<technology>` in `<surface/boundary>` because `<specific property>` solves `<specific SquiFlow requirement>`, while `<other technology>` may still be better for `<different surface>`.**

The same sentence must be answerable for a current non-selection:

> **We are not using `<technology>` at `<surface/boundary>` yet because `<specific requirement>` is absent or unproven; it becomes a candidate when `<specific trigger/evidence>` appears.**

## Project decision rule

SquiFlow may deliberately use more than one approach when they solve different problems better.

Examples:

```text
same-process business module call
    -> in-process

ordinary resource/command API
    -> HTTP / REST-task style when it gives the clearest contract

complex client-driven read composition
    -> GraphQL may be the better surface if the real UI/query workload benefits

Workstation sync or high-frequency streaming RPC
    -> gRPC may be the better transport if representative evidence supports it

live UI notification
    -> SignalR/WebSocket-style signaling, never durable truth

long-running / after-commit consequence
    -> durable async job/outbox/Worker
```

The existence of one does not prohibit the others.

## Correction / refinement for archive entry 060 — REST API vs GraphQL

The exhaustive study's previous shorthand, `KEEP REST baseline / GraphQL deferred`, must **not** be interpreted as `REST wins and GraphQL loses`.

The corrected architectural interpretation is:

### REST/task-oriented HTTP strengths for SquiFlow

Useful where SquiFlow benefits from:
- explicit resource and semantic command contracts;
- straightforward HTTP status/idempotency/cache semantics;
- OpenAPI-style inventory/tooling;
- external/partner integration familiarity;
- bounded request/response shapes;
- file/resource/status-oriented operations.

These are reasons to use it on suitable surfaces, not reasons to force every API through REST.

### GraphQL strengths that may be valuable to SquiFlow

GraphQL can be better for surfaces that genuinely need:
- client-selected field projections;
- nested/aggregate reads across several domain modules;
- rapidly changing Web/Admin dashboard read requirements;
- reduction of repeated over-fetch/under-fetch and client-side read composition;
- one typed query schema for flexible UI read models.

Potential SquiFlow candidates to investigate when their real UI exists include:
- tenant Web dashboard/overview reads spanning customer/order/quotation/payment/inventory summaries;
- Platform Admin read dashboards that combine several operational/control-plane views;
- configurable reporting/read screens whose requested projection varies materially by screen/use case.

These are **candidate read surfaces**, not an automatic commitment. Their actual data shapes, authorization needs, DB query behavior, and UI performance must be measured/prototyped.

### Where GraphQL does not automatically replace existing mechanisms

GraphQL adoption would not by itself replace:
- semantic idempotency for business commands;
- expected-version/concurrency rules;
- TenantContext and OpenFGA/resource/field authorization;
- durable Workstation synchronization semantics;
- gRPC evaluation for streaming/high-frequency sync or service RPC;
- durable async job/outbox semantics;
- current authoritative database/domain rules.

GraphQL mutations are technically possible and are not forbidden, but material SquiFlow commands still need explicit business intent, idempotency, authorization, concurrency, audit, and failure semantics regardless of API style.

### Current interpretation

```text
REST/task HTTP
    = useful current ordinary command/resource interface style

GraphQL
    = positive candidate for specific read-composition surfaces where it provides concrete benefit

Decision
    = per boundary/use case, not from the comparison article itself
```

GraphQL Federation remains a separate question: it should be considered only if there is a real federated schema/service ownership problem, not simply because GraphQL is useful for one UI surface.

## Rule for future archive batches

When an article says `A vs B`, the study must preserve both sides' strengths and weaknesses and then perform a **SquiFlow usage-fit analysis**. It must not convert the comparison into a project-wide winner/loser decision.

When a technology is already used or planned in SquiFlow, the review must explicitly state **where it is used, what requirement it solves there, and why that property is preferable for that particular surface**. When it is not currently used, the review must state the specific missing requirement or adoption trigger rather than merely saying `deferred`.

For technologies that are both useful in different places, the review should explicitly map the coexistence rather than forcing consolidation. Example:

```text
REST/task HTTP -> explicit commands/resources/external compatibility
graphQL        -> flexible/nested client-driven reads where useful
gRPC           -> streaming/high-frequency typed RPC where useful
SignalR        -> live notification/wakeup where useful
outbox/Worker  -> durable asynchronous consequences
```

Material adoption/removal or a change to an accepted owner architecture still follows the existing rule: surface the proposed change and get user approval before silently rewriting the owner decision. This review-method refinement itself is user-approved and applies immediately to the remaining archive study.

## Retroactive application

This rule is **not only forward-looking**. Archive entries `001-060` have been re-audited under it in:

`RETROSPECTIVE_TECHNOLOGY_FIT_AUDIT_001_060.md`

That retrospective audit is authoritative for interpreting older shorthand in `STUDY_001_010.md` through `STUDY_051_060.md`. In particular:

- `AVOID` means avoid the stated misuse/current boundary, not reject the technology globally;
- `LATER` / `not baseline` means no current requirement at that surface, not permanent exclusion;
- a currently selected mechanism is documented by **what we use, where we use it, and why it fits**, not because an article comparison crowned it the winner;
- a currently unselected mechanism is documented by the **missing requirement or trigger**, not by a generic negative label;
- complementary use is allowed and should be preferred when different technologies solve different boundaries better.

Future 10-article checkpoints must also verify that no earlier wording has accidentally been interpreted as a universal winner/loser rule.

## Promoted architecture-governance owner

The review method above now feeds the canonical cross-cutting selection process in:

`docs/architecture/WORKLOAD_STRATEGY_SELECTION.md`

For a **material** technology/pattern choice, the fit review must additionally provide enough evidence for that owner to answer, where relevant:

```text
workload / boundary
-> authority + HardInvariants
-> latency/freshness/completeness/capacity/offline/compatibility profile
-> simplest credible current mechanism
-> plausible alternatives
-> total failure/recovery/compatibility/operations/business cost
-> selected primary strategy
-> degraded/fallback + recovery/reconciliation where meaningful
-> rejected alternatives and why
-> verification evidence
-> falsification/revisit trigger
```

This does not turn every source review into an ADR or require a numeric score. The extra structure is required only when the review supports or challenges a consequential architecture choice.

A technology that wins one benchmark or one dimension still does not win the architecture decision if it violates a hard invariant or creates disproportionate system/recovery/operational cost. Conversely, a more complex mechanism can be correct when the business or reliability consequence of the simpler option is materially worse.
