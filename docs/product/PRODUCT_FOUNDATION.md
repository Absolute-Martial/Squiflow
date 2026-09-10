# Product Foundation

**Status:** Initial owner for upstream product intent and evidence discipline.  
**Baseline:** Introduced from the Product & Requirements Foundation Batch 1 review.  
**Authority boundary:** This document owns the upstream product foundation: who SquiFlow is trying to serve first, the business outcome it intends to enable, the evidence behind that choice, the smallest coherent product promise, and the status of important product assumptions. It does not replace the domain, security, workflow, NFR, architecture, implementation, or verification owner documents.

## 1. Why this document exists

SquiFlow already has detailed descriptions of domain behavior, invariants, failure handling, security, local-first authority, deployment, and implementation sequencing. That detail must not be mistaken for customer validation or product priority.

The repository is the source of truth for what SquiFlow currently documents and accepts. It is not, by itself, evidence that a customer need has been observed, that a journey is complete, or that a documented capability should be part of the first product promise.

Use this reasoning order for material product decisions:

```text
business goal
→ target customer / actor
→ observed problem or opportunity
→ desired outcome
→ evidence and assumptions
→ smallest coherent product responsibility
→ domain capability / invariant / permitted variation
→ functional and quality requirements
→ architecture only where justified
→ implementation
→ verification
```

## 2. Current product-evidence state

The repository currently documents a small-team-first direction, practical shop/print-oriented examples, Owner + Staff authorization defaults, and a broad set of plausible business capabilities and constraints.

The following are **not yet established as validated product facts merely because they are documented**:

- the exact initial customer population;
- the distinction between buyer, administrator, daily operator, and other affected actors for the first customer;
- the first end-to-end business outcome SquiFlow promises;
- the starting condition and observable completion condition for that promise;
- which currently documented journeys are observed customer behavior versus working product hypotheses;
- which onboarding/import/support steps are necessary for a first customer to reach useful routine operation;
- the product-success evidence that should determine whether the first product bet continues, narrows, changes, defers, or stops.

Until evidence closes these questions, do not fill them with plausible invented stories.

## 3. Product promise

Before a customer-facing release is treated as a coherent product offering, record a promise in this form:

```text
For [initial customer / actor],
SquiFlow helps complete [business outcome],
from [starting condition]
to [observable result],
under [important operating conditions].

It does not yet promise [important exclusions].
```

Every populated field must be supported by repository evidence, customer/domain evidence, or an explicitly labelled hypothesis.

A product promise is not complete merely because each listed feature works independently. Identify any missing step that prevents the customer from reaching the promised observable result even when all implemented features behave correctly.

A manual or external step may remain part of the selected scope when that is deliberate, safe, and compatible with the promise.

## 4. Current-model pass and problem-first pass

For material discovery, use two distinct views.

### Current-model pass

Understand what SquiFlow currently describes: actors, capabilities, journeys, terminology, invariants, configuration, requirements, boundaries, accepted decisions, and open decisions.

### Problem-first pass

Independently describe the relevant business work without using SquiFlow's existing entities, modules, services, screens, or architecture as the organizing model.

Trace where useful:

```text
trigger or need
→ actor and intended outcome
→ information required and origin
→ action or decision
→ rules and authority
→ state or information change
→ handoff or external interaction
→ result
→ confirmation/evidence
→ exceptions, correction, and recovery
```

Then compare the two views to identify supported current structure, missing responsibility, unnecessary structure, semantic mismatch, configuration fit, missing reusable capability, customer-specific exception, or unresolved uncertainty.

This is a diagnostic exercise, not permission to redesign SquiFlow from scratch.

## 5. Product evidence and status vocabulary

Keep evidence provenance separate from decision status.

### Evidence provenance

Use the project evidence taxonomy where applicable:

- **SOURCE** — an external source's claim or method;
- **REPOSITORY** — what SquiFlow currently documents;
- **OBSERVATION / MEASUREMENT** — direct observation, artifact, transaction trace, test, measurement, or other credible evidence of actual behavior;
- **INFERENCE** — a reasoned conclusion that is not direct evidence;
- **EXTERNAL KNOWLEDGE / CAVEAT** — relevant outside knowledge whose applicability must be checked;
- **UNKNOWN** — evidence is insufficient or unavailable.

For business-process evidence, distinguish where material:

- **PRESCRIBED** — what a process or policy says should happen;
- **REPORTED** — what participants say happens;
- **OBSERVED** — what direct observation or credible artifacts show happened.

### Product-understanding status

Use these statuses without collapsing them:

```text
DISCOVERED QUESTION
something consequential may be unexplained

HYPOTHESIZED NEED
a plausible need has been identified but is not yet established

SUPPORTED NEED
credible evidence shows the need exists for the relevant customer/context

ACCEPTED PRODUCT RESPONSIBILITY
SquiFlow has deliberately committed to serving the need

IMPLEMENTED CAPABILITY
working software provides the responsibility

VERIFIED OUTCOME
evidence shows the capability enables the intended customer result
```

An accepted repository statement does not automatically mean `SUPPORTED NEED`. An implemented capability does not automatically mean `VERIFIED OUTCOME`.

For important journey/scenario statements, record enough provenance to determine whether they are documented, reported, observed, inferred, or validated rather than silently treating all narrative examples as customer evidence.

## 6. Roles are not personas

`Owner` and `Staff` are current default authorization templates. They are not automatically behavioral personas or proof of how work divides inside a target business.

Discovery must separately investigate, when consequential:

- who purchases or approves SquiFlow;
- who configures/administers it;
- who performs the daily work;
- who receives the business outcome;
- who bears operational, financial, security, or compliance burden;
- who has authority to accept a trade-off.

Do not add Manager, Designer, Sales, Stock, Print Operator, or other role/persona structures merely because such labels are plausible. Add distinctions only when real work, authority, incentives, or permissions require them.

## 7. Implementation phases are not product-release promises

`MASTER_IMPLEMENTATION_PLAN.md` and `docs/implementation/PHASES_AND_GATES.md` define technical implementation order and proof obligations. A completed implementation phase does **not** by itself mean SquiFlow has reached a coherent, sellable, or customer-complete product release.

A technical vertical slice may intentionally prove only part of a later customer outcome.

Before presenting a version as a customer-facing product promise, verify separately that:

- the stated starting condition is supported;
- the relevant actors can continue the work end to end;
- required information can be obtained or captured;
- necessary external/manual steps are explicit and acceptable;
- material failure/correction/recovery paths are understood;
- the promised observable result can actually be reached;
- the user has evidence that the result occurred;
- important exclusions are stated honestly.

This distinction does not change the current sequential technical phase plan. It prevents implementation milestones from becoming implicit product claims.

## 8. Reversibility and uncertainty

Do not call an assumption safe merely because it keeps the current design unchanged.

For important uncertainty choose deliberately among:

- proceed provisionally when cheaply reversible;
- run an experiment/POC when evidence can distinguish real alternatives;
- restrict exposure when the uncertainty can be bounded;
- defer commitment while preserving options;
- block affected implementation when being wrong can create unacceptable financial, legal, security, tenant-isolation, data-loss, historical-truth, or expensive-migration harm.

Product discovery may move quickly. Protected invariants do not become experiments merely because the team is learning.

## 9. Highest-impact unresolved product questions

Keep this list short. Expand it only when a new question can materially change near-term action.

### PFQ-001 — Who is the initial customer ecosystem?

What exact business context is first, and who are the buyer, administrator, daily operator, and other materially affected actors?

**What it changes:** positioning, onboarding, defaults, UX priorities, actor/permission research, and the product promise.  
**Current action:** keep Owner/Staff as authorization templates; do not invent personas.

### PFQ-002 — What is the first end-to-end business outcome SquiFlow promises?

**What it changes:** release completeness, scope, journey priority, and which missing steps are genuine blockers.  
**Current action:** continue technical slices, but do not equate a collection of implemented capabilities with a complete customer offering.

### PFQ-003 — How is the selected work actually performed today?

Investigate recent concrete cases, including paper, calls, messages, spreadsheets, device interactions, artifacts, workarounds, exceptions, and recovery.

**What it changes:** domain meaning, missing responsibilities, unnecessary structure, integration/manual boundaries, and UX.  
**Current action:** treat repository journeys as documented models whose customer-evidence status must be established separately.

### PFQ-004 — What must happen from adoption to first useful routine result?

**What it changes:** onboarding/import/configuration/support scope and whether the first customer can actually realize the product promise.  
**Current action:** do not build generic ETL/onboarding platforms; investigate the first real customer case.

### PFQ-005 — What evidence is sufficient to continue, narrow, change, defer, or stop a product responsibility?

**What it changes:** prevents both endless discovery and premature requirement acceptance.  
**Current action:** preserve explicit evidence labels, prefer investigations that distinguish real alternatives, and use revisit triggers when more analysis is unlikely to change the current action.

## 10. Scope reduction and stop conditions

Discovery is allowed to conclude that:

- a capability is not justified;
- a segment should wait;
- a workflow should remain manual;
- an existing distinction creates more burden than value;
- a proposed configuration mechanism is unnecessary;
- an existing structure should be simplified;
- a product hypothesis needs reconsideration.

For major product bets record what evidence would cause SquiFlow to **continue, narrow, change, defer, or stop**. Record material opportunity cost when pursuing an option delays more valuable work.

Do not treat every discovered need as a commitment to serve it.

## 11. Relationship to existing owner documents

- `docs/domain/BUSINESS_MODEL.md` owns the current practical business/domain model and scope.
- `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md` owns shared business semantics.
- `docs/workflow/WORKFLOW_DESIGN.md` owns workflow semantics.
- `docs/rules/NATIVE_RULE_ENGINE.md` owns bounded rule behavior.
- `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md` owns cross-cutting quality requirements.
- `docs/decisions/CURRENT_DECISIONS.md` owns accepted direction.
- `docs/decisions/OPEN_DECISIONS.md` owns current unresolved implementation decisions.
- `MASTER_IMPLEMENTATION_PLAN.md` and `docs/implementation/PHASES_AND_GATES.md` own technical implementation sequencing.

This document sits **upstream** of those owners for product intent and evidence. It does not override an accepted material product/domain/architecture decision silently. Material changes still require explicit approval and must update the appropriate owner document.
