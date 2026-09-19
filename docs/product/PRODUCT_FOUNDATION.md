# Product Foundation

**Status:** Initial owner for upstream product intent and evidence discipline.  
**Baseline:** Introduced from the Product & Requirements Foundation Batch 1 review, refined by Batch 2, extended by Batch 3 quality/traceability review, refined by Batch 4 integration/measurement/discovery review, and refined by Batch 5 language/adoption/research review.  
**Authority boundary:** This document owns the upstream product foundation: who SquiFlow is trying to serve first, the business outcome it intends to enable, the evidence behind that choice, the smallest coherent product promise, the status of important product assumptions, and the product-level reasons that justify consequential quality characteristics. It does not replace the domain, security, workflow, NFR, architecture, implementation, or verification owner documents.

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
→ quality-characteristic priority / trade-offs
→ architecture only where justified
→ implementation
→ verification
→ observed product outcome
```

## 2. Current product-evidence state

The accepted strategic product direction is a small-team-first, tenant-adaptable business operations platform. SquiFlow is intended to let a tenant select supported capabilities and personalize branding, settings, permissions, rules, workflows, forms, supplementary information and integrations without requiring a forked product codebase for ordinary variation. When those mechanisms cannot safely express a supported material difference, the platform may offer a reviewed trusted implementation variant and a stronger data/processing/runtime isolation profile.

This intent exists because SquiFlow cannot predict and hard-code every way a tenant may operate. It does not mean every imaginable tenant case must be accepted, nor does it authorize arbitrary tenant code, SQL, assemblies or unbounded schemas. Supported variation remains validated, versioned, explainable, resource-bounded and compatible with protected business/security invariants. Detailed owner: `docs/architecture/TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md`.

Branding is one visible personalization capability, not the limit or definition of tenant variation. A strategic platform outcome is that one tenant's selected profile, malformed configuration, integration failure or heavy workload has a bounded and explainable effect on other tenants according to the qualified isolation profile.

The repository currently grounds that platform direction in practical shop/print-oriented examples, Owner + Staff authorization defaults, and a broad set of plausible business capabilities and constraints.

The following are **not yet established as validated product facts merely because they are documented**:

- the exact initial customer population;
- the distinction between buyer, administrator, daily operator, and other affected actors for the first customer;
- the first end-to-end business outcome SquiFlow promises;
- the starting condition and observable completion condition for that promise;
- which currently documented journeys are observed customer behavior versus working product hypotheses;
- which onboarding/import/support steps are necessary for a first customer to reach useful routine operation;
- which business work must remain usable during network loss or central-dependency failure, and for how long;
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

Where quality characteristics materially determine whether the promise is believable, state the business consequence of poor quality and the relevant trade-off. Do not treat `fast`, `real time`, `always available`, `offline`, or `strongly consistent` as self-justifying virtues.

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
→ waiting / queue / bottleneck where work stops moving
→ result
→ confirmation/evidence
→ exceptions, correction, and recovery
```

When users describe friction such as `slow`, `takes too long`, `too many steps`, `hard to follow`, or `we do this outside the system`, do not jump directly to automation or a new feature. Identify where the work actually waits or becomes difficult, what causes that condition, who bears it, and whether the smallest response is visibility, information, responsibility, process simplification, configuration, integration, automation, a manual step, or no SquiFlow change.

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

For consequential discovery evidence, record both **who supplied the evidence** and **whose work/outcome it describes** when those are different. An Owner reporting a Staff problem is useful REPORTED evidence, but it is not equivalent to Staff reporting the problem or to direct observation/artifacts showing Staff encounter it. Do not discard stakeholder evidence; label the actor/source correctly so one person's report does not silently become another actor's observed behavior.

AI/assistant-generated user stories, personas, journey completions, missing requirements, priorities, role taxonomies, domain concepts, and proposed product decisions are **INFERENCE / HYPOTHESIS** unless separately supported by evidence or deliberately accepted as a normative Owner decision. Coherence, detail, confidence, or repetition in generated text does not upgrade its evidence status.

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

Investigate recent concrete cases, including paper, calls, messages, spreadsheets, device interactions, artifacts, workarounds, exceptions, waiting/handoffs, and recovery.

**What it changes:** domain meaning, missing responsibilities, unnecessary structure, integration/manual boundaries, and UX.  
**Current action:** treat repository journeys as documented models whose customer-evidence status must be established separately.

### PFQ-004 — What must happen from adoption to first useful routine result?

Investigate the complete path where consequential: evaluation/approval, setup, initial data entry or bounded import, initial configuration, learning/training, first real use, recovery from the first mistake or interruption, and repeated routine use. Identify which steps can remain assisted/manual, which need strong defaults, and which genuinely block the product promise.

**What it changes:** onboarding/import/configuration/support scope and whether the first customer can actually realize the product promise.  
**Current action:** do not build generic ETL, onboarding, training, or change-management platforms; investigate the first real customer case and use the smallest credible response, including assisted/manual setup when sufficient.

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

### PFQ-008 — Which work must remain usable when connectivity or central dependencies are unavailable?

For the first product promise, identify the affected actor/work, business consequence if it stops, acceptable interruption duration, required data accuracy/freshness, current-authority requirement, and whether the operation should be local-capable, local-provisional, or server-required.

**What it changes:** Workstation scope, sync/recovery obligations, local data requirements, dependency/degraded-mode behavior, and the product meaning of `local-first`.  
**Current action:** preserve the accepted local-first Workstation architecture, but do not classify additional operations as offline-capable merely because the desktop can technically execute them.

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

## 15. Quality characteristics follow business consequences

Architecture characteristics are not independent technical virtues. A product may need high availability, freshness, accuracy, offline continuity, low latency, explainability, recoverability, simplicity, or another quality because failure of that quality would prevent or materially damage the selected business outcome.

For every material quality requirement that can drive architecture or operating cost, record where known:

```text
business outcome / journey
→ consequence if the quality is poor
→ quality characteristic
→ relative priority / trade-off
→ measurable scenario or evidence
→ requirement owner
→ architecture consequence if any
→ verification evidence
```

Examples of trade-offs that must be decided from the business context rather than slogans include:

```text
freshness vs accuracy
latency vs current authority
availability vs fail-closed security
local continuity vs current shared stock/credit truth
maximum configurability vs simple operation
immediate synchronization vs bounded reliable recovery
```

The detailed NFR semantics remain owned by `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md`; the product-level reason and priority belong here and in the traceability record described by `docs/requirements/PRODUCT_TO_QUALITY_TRACEABILITY.md`.

## 16. Product-outcome evidence has a semantic contract

When SquiFlow uses a metric, observation, artifact, or derived measure to decide whether a product outcome occurred, define enough meaning to prevent several teams/documents from silently measuring different things under the same name.

Where material, record:

```text
measure / evidence name
→ decision or outcome it informs
→ meaning
→ population / scope
→ authoritative or observational source
→ calculation / interpretation
→ time window
→ freshness
→ known uncertainty / error
→ who interprets it
→ what decision it may change
```

Do not build a metrics platform merely to satisfy this rule.

Keep these concepts distinct unless a documented link exists:

```text
product-outcome evidence
≠ operational telemetry
≠ delivery-progress metric
≠ engineering diagnostic / code-quality proxy
≠ authoritative resource-consumption accounting
```

## 17. Cross-owner consistency and decision propagation

A material accepted change must not be made correct in one document while leaving dependent owner documents contradictory, misleading, or unable to enforce it.

For each accepted material product/domain/architecture change, perform a dependency check across the owners that may be affected, including as applicable:

- product foundation;
- domain/business semantics;
- permissions/security/identity;
- workflow/rules/forms;
- integrations/external providers;
- NFRs and degraded modes;
- local-first/sync authority;
- architecture/current decisions;
- implementation phases/gates;
- verification/testing;
- operations/support.

Use this rule:

```text
accepted decision
→ update primary owner
→ identify dependent contracts
→ update every dependent owner whose meaning changed
→ record why unaffected owners remain unchanged
→ verify the resulting repository has one coherent story
```

Do not touch unrelated documents merely for symmetry. A dependent owner needs a change only when leaving it unchanged would create contradiction, ambiguity, an unenforced obligation, or a materially stale assumption.

## 18. Constructive adversarial review

Every material analysis conclusion, proposed requirement, owner-document patch, or architecture implication gets a skeptical review before acceptance. The purpose is to improve the decision and expose alternatives, not to block progress indefinitely.

At minimum ask:

```text
What is the strongest plausible case that this conclusion is wrong or overstated?
Which hidden assumptions does it depend on?
Am I confusing a source recommendation, repository convention, or generated inference with evidence?
What simpler response could solve the same problem?
What genuinely different response have I failed to consider?
What would this decision make harder, more expensive, or less reversible?
Which actor or downstream owner bears the cost?
What evidence would falsify or materially change the conclusion?
Is the current SquiFlow model constraining the option space unnecessarily?
```

For assistant-produced work specifically, treat the assistant's synthesis as something to challenge, not as an authority. When the review finds a material weakness, revise the finding, narrow it, mark it uncertain, defer it, or reject it before applying a patch.

The review must also preserve creativity. A challenge should surface at least one credible alternative or contrary interpretation when one exists; it should not merely argue that nothing can be known.

Use this stop rule:

> Stop adversarial review when further criticism is unlikely to change the current decision, required evidence, scope, safety classification, or next reversible action. Record the remaining uncertainty and proceed rather than turning skepticism into paralysis.

Protected invariants still require stronger proof than reversible product-shaping choices.

## 19. Documentation truth hierarchy

Different artifacts answer different questions and must not silently replace one another:

```text
owner document
= current authoritative SquiFlow semantics / decision for its topic

decision or review record
= evidence, alternatives, challenge, and why the decision was made

test / executable evidence
= proof that implemented behavior satisfies a defined requirement in a stated environment

source study
= what an external source contributes and how it was interpreted

code
= current implemented mechanism, which may still be incomplete or inconsistent with accepted intent
```

Prefer one focused owner for current semantics. Do not duplicate the same authoritative rule across several documents merely for convenience. References are preferable when the dependent document's contract has not changed.

Tests complement requirements; they do not explain product intent, evidence status, historical rationale, or every non-executable constraint. Documentation should be kept only where it has a clear purpose and can be maintained.

## 20. Relationship to existing owner documents

- `docs/domain/BUSINESS_MODEL.md` owns the current practical business/domain model and scope.
- `docs/domain/BUSINESS_TERMS.md` owns consequential business-term meaning, distinction, aliases, ambiguity, semantic evidence/status, and historical interpretation; focused domain owners still own behavior and invariants.
- `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md` owns shared business semantics.
- `docs/workflow/WORKFLOW_DESIGN.md` owns workflow semantics.
- `docs/rules/NATIVE_RULE_ENGINE.md` owns bounded rule behavior.
- `docs/integrations/INTEGRATION_RESPONSIBILITY_AND_AUTHORITY.md` owns the shared responsibility/authority/failure questions every external integration must answer; provider-specific semantics remain in their focused owners.
- `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md` owns cross-cutting quality requirements.
- `docs/requirements/PRODUCT_TO_QUALITY_TRACEABILITY.md` owns the lightweight bridge from product/business consequence to quality requirement/verification evidence; it does not override NFR semantics.
- `docs/decisions/CURRENT_DECISIONS.md` owns accepted direction.
- `docs/decisions/OPEN_DECISIONS.md` owns current unresolved implementation decisions.
- `MASTER_IMPLEMENTATION_PLAN.md` and `docs/implementation/PHASES_AND_GATES.md` own technical implementation sequencing.

This document sits **upstream** of those owners for product intent and evidence. It does not override an accepted material product/domain/architecture decision silently. Material changes still require explicit approval and must update the appropriate owner document and any materially dependent owners.

## 21. Proportional prototyping and design evidence

A prototype is an evidence instrument, not automatically a mini-product, a required phase, or proof that the underlying customer need exists.

Before choosing prototype fidelity, identify the uncertainty to resolve and use the lowest-cost representation capable of distinguishing the meaningful alternatives.

Examples:

```text
terminology / information hierarchy / rough workflow
→ sketch, paper flow, wireframe, or lightweight clickable mock may be enough

interaction detail / visual trust / complex state comprehension
→ higher-fidelity interactive prototype may be justified

printing / local database / offline recovery / provider integration / performance
→ executable spike or real vertical slice may be the cheapest credible evidence
```

No prototype is required when the uncertainty is already sufficiently closed or implementation itself is the cheapest reversible experiment.

Prototype observations must still be classified by evidence provenance. A participant succeeding with a prototype may support usability/interaction conclusions; it does not by itself establish market demand, product priority, business viability, or successful real-world operation.

## 22. Adoption is part of reaching the outcome, not a separate enterprise program

When the first product promise depends on adoption work, treat setup and transition as part of the end-to-end journey rather than assuming value begins once software is deployed.

Where relevant inspect:

```text
evaluation / approval
→ initial setup
→ data entry or bounded import
→ initial configuration
→ learning / explanation
→ first real work
→ first mistake / interruption / recovery
→ repeated routine use
→ useful business result
```

A discovered adoption blocker does **not** automatically justify an onboarding feature. Consider the smallest credible response: stronger defaults, assisted setup, a bounded import, contextual guidance, a short reference, training, a manual procedure, or no product change.

Do not introduce a learning-management system, enterprise change-management process, generic onboarding platform, or automatic migration framework without evidence that the product promise requires one.

## 23. Semantic discovery before long-lived commitment

Capture the participant's own language during discovery before normalizing it into SquiFlow terminology.

When terms such as `job`, `order`, `request`, `sale`, `customer`, `account`, `completed`, `fulfilled`, or similar language could change domain identity, lifecycle, authority, workflow, reporting, permissions, or historical meaning, resolve or explicitly mark the ambiguity before hardening it into persistence, public APIs, durable message contracts, workflow states, or irreversible migrations.

Use `docs/domain/BUSINESS_TERMS.md` for consequential semantic conclusions and unresolved disputes. A vocabulary difference alone does not prove a new entity, role, workflow engine feature, or `TenantType`.

## 24. Design-system/style-guide trigger

SquiFlow does not currently require a formal cross-platform design system merely because Web and Workstation use different UI technologies.

Preserve semantic consistency now: the same business concept, important state, danger, correction, permission consequence, and local/server authority meaning should not diverge between surfaces without an explicit reason.

Revisit stronger shared style/component infrastructure when evidence shows repeated pattern implementation, semantic/state divergence, user errors caused by inconsistency, significant repeated implementation cost, reusable accessibility/visual-quality requirements, or multiple contributors making ad-hoc patterns difficult to govern.
