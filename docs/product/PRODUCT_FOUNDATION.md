# Product Foundation

**Status:** Initial owner for upstream product intent and evidence discipline.  
**Baseline:** Introduced from the Product & Requirements Foundation Batch 1 review and refined by Batch 2.  
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

Problem-first discovery is not technology-blind discovery. After the business work and problem have been described independently, bring engineering, UX, domain, operational, and technical constraints into discovery early enough for feasibility or enabling technology to distinguish real response options. Do not let the current architecture define the problem, and do not postpone feasibility thinking until product research is supposedly finished.

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

Release is not the end of product validation. For a material product responsibility, identify what evidence after implementation will show whether the intended customer/business outcome actually occurred, who or what will inspect that evidence, and what action follows if the outcome is not achieved. Passing technical tests proves implemented behavior; it does not, by itself, prove product success.

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

### PFQ-006 — What behavior should SquiFlow's configuration encourage?

For each materially configurable area, what do Owners genuinely need to vary, how often, which defaults cover the common case, who bears the resulting setup/cognitive burden, and when a repeated variation signals a missing reusable capability instead of another setting?

**What it changes:** workflow/rule/form/permission configuration scope, default design, onboarding burden, and whether configuration is being used to avoid making a clearer product decision.  
**Current action:** prefer strong defaults and the smallest bounded variation that expresses demonstrated differences safely; do not equate maximum configurability with product flexibility.

### PFQ-007 — What makes the first customer suitable as a product-learning customer?

Can SquiFlow observe the customer's real work, connect it to the selected product promise, constrain risk, obtain outcome evidence, distinguish customer-specific exceptions from reusable needs, and define what would justify wider generalization?

**What it changes:** first-customer selection, pilot scope, generalization of domain/configuration decisions, and when evidence from one business may influence the common model.  
**Current action:** treat first-customer findings as context-bound evidence unless repeated evidence or a justified invariant/capability argument supports generalization.

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

## 11. Configuration is behavioral design, not only expressive power

A configuration mechanism is not justified merely because it can safely represent variation.

For tenant-facing permissions, workflows, rules, forms, pricing/policy, feature visibility, and similar configurable behavior, evaluate:

- which behavior the interface makes easiest;
- which behavior it makes difficult or invisible;
- whether a strong default avoids configuration for the common case;
- how much setup and ongoing cognitive burden the Owner carries;
- how much complexity an Owner can unintentionally transfer to Staff;
- whether users can understand the effect of a change before publication;
- whether accumulated configuration makes support, explanation, correction, or migration disproportionately difficult;
- whether the same requested variation repeatedly appears and is better represented as a reusable product capability;
- whether configuration is being used to avoid resolving an important semantic/product distinction.

Use this rule:

```text
configurable
≠ justified variation design

preferred target
= strong common semantics
+ strong defaults
+ protected invariants
+ smallest bounded variation supported by evidence
```

This strengthens the existing bounded-configuration direction. It does not authorize arbitrary tenant scripting or a universal configuration platform.

## 12. First-customer learning boundary

The first real customer can be a valuable product-learning context, but one customer's behavior must not silently become the universal SquiFlow model.

Before treating a first-customer engagement as evidence for broader product decisions, record where practical:

```text
customer/context
→ selected product promise
→ important exclusions
→ learning questions
→ protected invariants and exposure limits
→ evidence to collect
→ success/outcome evidence
→ customer-specific assumptions
→ generalization risks
→ conditions for broader adoption
```

A first customer's terminology, workflow, staffing pattern, pricing habit, supplier process, or operational workaround is evidence about that context. Generalize only when repeated evidence, a shared underlying responsibility, or a justified invariant/capability argument supports it.

In particular:

```text
first print-oriented customer
≠ proof of TenantType.PrintShop

one customer's requested setting
≠ proof of universal configurability need
```

A bounded pilot may intentionally keep some work manual or external while protecting financial, security, tenant-isolation, historical-truth, and other hard invariants.

## 13. Product-response risk check

Before accepting a material product response, examine at least four distinct questions:

### Value

Does the relevant customer/actor actually need or value the outcome enough for SquiFlow to prioritize it?

### Usability

Can the relevant actors accomplish the work without disproportionate setup, cognitive, training, navigation, or recovery burden?

### Feasibility

Can SquiFlow build and operate the responsibility safely within current technical, resource, reliability, security, compatibility, and support constraints?

### Business viability

Can SquiFlow realistically sell, support, service, secure, operate, and where applicable legally/contractually provide this responsibility without undermining the selected product focus?

These are diagnostic responsibilities, not mandatory job titles, team structures, or a requirement to adopt an external Product Operating Model.

For the accepted response, identify the important unresolved risk, the evidence needed to close it, and whether the decision should proceed provisionally, be restricted, be tested, be deferred, or be blocked.

## 14. Outcome ownership after release

Implementation creates an opportunity to verify a product hypothesis; it does not complete that verification automatically.

For material product responsibilities, preserve this continuation:

```text
implemented capability
→ actual use
→ observed customer/business outcome
→ compare with product promise
→ investigate mismatch
→ continue / improve / narrow / change / defer / stop
```

The responsibility may be fulfilled by the same small team; this section does not require a dedicated Product Manager, Product Ops role, analytics platform, or permanent experimentation infrastructure.

Use the smallest credible evidence mechanism appropriate to the product promise. Qualitative observation, support evidence, transaction/business artifacts, targeted measurement, or a combination may be enough. Do not create metrics because they are easy to collect; measure what can distinguish whether the intended result occurred.

## 15. Relationship to existing owner documents

- `docs/domain/BUSINESS_MODEL.md` owns the current practical business/domain model and scope.
- `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md` owns shared business semantics.
- `docs/workflow/WORKFLOW_DESIGN.md` owns workflow semantics.
- `docs/rules/NATIVE_RULE_ENGINE.md` owns bounded rule behavior.
- `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md` owns cross-cutting quality requirements.
- `docs/decisions/CURRENT_DECISIONS.md` owns accepted direction.
- `docs/decisions/OPEN_DECISIONS.md` owns current unresolved implementation decisions.
- `MASTER_IMPLEMENTATION_PLAN.md` and `docs/implementation/PHASES_AND_GATES.md` own technical implementation sequencing.

This document sits **upstream** of those owners for product intent and evidence. It does not override an accepted material product/domain/architecture decision silently. Material changes still require explicit approval and must update the appropriate owner document.
