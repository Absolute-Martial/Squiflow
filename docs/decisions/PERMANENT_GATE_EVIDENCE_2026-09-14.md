# Decision — Permanent Gate Evidence and Regression Protection

**Date:** 2026-09-14  
**Status:** Accepted clarification of production-honest phase governance

## Decision

Passing an **active** SquiFlow phase/subphase does not merely prove that a property was true at review time.

For every material claimed property:

```text
claim
→ falsifiable evidence
→ permanent or recurring regression guard
→ requalification trigger
```

The canonical owner is:

`docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

This complements, rather than replaces:

`docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`.

## Why

The production-honesty model fixed the breadth-versus-depth ambiguity, but a second gap remained: a gate could still be satisfied by a one-time review/test/drill and silently regress later.

A production property must be either continuously protected or deliberately re-proven on an explicit recurring/release/operator cadence appropriate to its cost and risk.

A later correction also clarified that this permanence model applies only to **real active claims**. Pre-assigning evidence/cadence to not-yet-introduced future phases would invert the dependency and turn speculation into governance constraint. Detailed future phase specificity is therefore earned when real responsibilities arrive. See `docs/decisions/EARNED_PHASE_GOVERNANCE_2026-09-14.md`.

## Accepted evidence cadence

Active claims use one or more explicit cadences:

```text
PER_COMMIT
PER_MR
SCHEDULED
PRE_RELEASE
RELEASE_CANDIDATE
OPERATOR_DRILL
PRODUCTION_MONITOR
```

Cheap deterministic/architecture/security checks should fail quickly. Expensive provider/process/restore/hardware exercises may run less frequently, but `manual once` is not a permanence strategy.

No cadence is assigned merely because a future phase label exists.

## Transitional states

Active subphase ordering may create intermediate states. Any reachable intermediate state with missing later guarantees has an explicit transitional contract:

- what exists;
- what is allowed;
- what is disabled/forbidden;
- guarantees already qualified;
- guarantees explicitly absent;
- technical enforcement preventing accidental use;
- the real closing condition.

This is particularly important for Phase 1, where successful authentication before full tenant authorization/session/reconciliation does not grant permission to expose insecure business mutation.

Future direction-only phases do not predeclare transition contracts because no reachable intermediate state exists yet.

## Deferred-item safe absence

A `NOT_INTRODUCED` security/recovery/compatibility item records what the system does while the item is absent and why that behavior is safe, to the degree the current system can honestly know/enforce it.

Provider/framework defaults are not silently accepted architecture. If current behavior comes from a real default, it is consciously accepted/configured/pinned where practical and verified.

If no safe behavior exists in the item's absence for current reachable scope, the item is not deferrable and the current responsibility is `BLOCKED`.

Future provider/mechanism-specific absence tests are not invented before that provider/mechanism exists.

## Authorization/policy changes during active operations

For mutable authority/configuration that can change while real operations are pending, each real operation class declares the authoritative decision point: proposal, server admission, durable acceptance, execution attempt, before external effect, before commit, or explicit checkpoints.

No universal `revocation cancels everything` or `authorization once forever` rule is adopted.

Current authority is rechecked where the owning invariant requires it; already committed historical facts are not silently rewritten by later policy change.

Do not predeclare decision points for operation classes that do not yet exist.

## Threat modeling

Trust-boundary changes receive systematic threat review using STRIDE or equivalent categories as useful vocabulary. Hostile tests are derived from real data/authority flows rather than only reviewer creativity.

A threat finding affecting a reachable claimed surface is fixed or `BLOCKED`, not carried as later hardening.

Future threat concerns may be retained as anticipation, but not as a canonical hostile-test inventory for a surface whose actual data flow/topology is unknown.

## Failure injection

Failure injection is recurring evidence for active responsibilities that depend on process/network/storage/provider behavior. Exercises define protected steady state/invariants, inject bounded realistic faults, verify degradation/recovery, and minimize blast radius.

SquiFlow does not adopt large-scale production chaos experimentation by slogan; experiments match the actual deployment profile and begin in controlled/representative environments.

No failure matrix is prewritten for providers/processes/storage mechanisms that are not yet introduced.

## SLOs and error budgets

Operational targets such as latency, availability, backlog age, transfer latency, and recovery time may use measured SLIs/SLOs/error budgets once the workload exists.

Hard correctness/security invariants do not receive an error budget. Examples include:

- cross-tenant leakage;
- unauthorized privileged effects;
- SquiFlow-caused semantic double charge;
- corruption/loss of acknowledged state inside the declared durability contract;
- secret/key disclosure;
- silent unsupported-version reinterpretation.

Exact SLO values are never predeclared for future workloads.

## Architecture enforcement

Once concrete .NET assemblies exist, important dependency/ownership rules are mechanically protected in CI/MR verification. NetArchTest is one possible mechanism, not a mandatory package; an equivalent architecture-test/static mechanism is acceptable.

The architecture rule remains authoritative, not the tool.

## Phase-1 standards correction

The identity/security phase grounds its OAuth/OIDC claims in the applicable standards rather than `production-shaped` wording.

Relevant baseline guidance includes:

- RFC 9700 — OAuth 2.0 Security Best Current Practice;
- RFC 8252 — OAuth 2.0 for Native Apps;
- RFC 7636 — PKCE;
- OpenID Connect Core for OIDC-specific semantics.

RFC 9449 defines OAuth 2.0 DPoP; it is not the generic browser-based-app specification. DPoP remains a topology/threat-model-dependent mechanism, not an automatic baseline requirement.

## Relationship to external engineering guidance

This decision adopts principles consistent with the engineering guidance reviewed for this change:

- stability/failure semantics should be explicit rather than hidden behind `works normally`;
- gates should prefer permanent automated checks when practical;
- failure experiments should be reproducible/recurring rather than one-time ceremonies;
- SLO/error-budget thinking applies to operational reliability targets, not hard safety/security invariants;
- architecture boundaries should be executable where tooling can enforce them;
- small falsifiable implementation batches are preferred over broad gates satisfied by assertion.

External books/articles are supporting reasoning, not normative repository authority. SquiFlow's focused owners and tested contracts remain the governing source.

## Governance self-honesty

The permanence model itself follows production honesty:

```text
real responsibility
→ real claim
→ evidence
→ cadence
```

not:

```text
future phase number
→ speculative evidence/cadence
→ future implementation constrained by speculation
```

Phase 2–10 currently remain direction-only. Their earlier detailed evidence/subphase planning was retired from canonical phase documents and preserved as non-authoritative anticipation in `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md`.