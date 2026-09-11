# Discovery Complexity and Research Evidence

**Version:** v0.0.17

**Status:** Focused operational companion to `docs/product/PRODUCT_FOUNDATION.md`.  
**Authority boundary:** `PRODUCT_FOUNDATION.md` remains the upstream owner for product intent, evidence discipline, product questions, and product-promise decisions. This document owns the detailed discovery rules for locating complexity/friction, classifying exploratory artifacts, and handling customer-research evidence safely. It does not create a UX process, ResearchOps function, design methodology, or product feature.

## 1. Why this document exists

SquiFlow aims to keep small-business operation understandable without pretending that real business work is always simple.

A difficult workflow can be caused by:

- the interface itself;
- the end-to-end journey and handoffs;
- information distributed across people/artifacts/systems;
- external integrations or providers;
- changing user intent or exceptions;
- physical environment and interruptions;
- organizational/institutional rules or authority;
- unnecessary complexity introduced by SquiFlow itself.

Before adding a feature, workflow, configuration option, integration, or automation, identify which of these is actually creating the friction.

This operationalizes PFQ-003 in `docs/product/PRODUCT_FOUNDATION.md` rather than adding another top-level product question.

## 2. Essential versus accidental complexity

Use this distinction:

```text
Essential complexity
= complexity inherent in completing the business outcome safely and truthfully

Accidental complexity
= ceremony, duplication, technical leakage, redundant steps,
  unnecessary configuration, unclear ownership, or other burden
  introduced by the product/process without protecting a real need
```

The goal is not `make every workflow look simple`.

The goal is:

```text
remove accidental complexity
+
make essential complexity understandable, actionable,
and proportionate to the actor who encounters it
```

Examples of potentially essential complexity include:

- `OutcomeUnknown` for an ambiguous financial effect;
- `Saved locally` versus `Server accepted`;
- pending synchronization/review;
- current-authority requirements for sensitive permissions, stock, credit, or hard limits;
- explicit correction/reversal history instead of destructive editing.

Do not hide these states merely to reduce visible options or steps when the actor needs them to make a safe decision.

## 3. Locate the level of pain before choosing the response

When a user reports that something is difficult, slow, confusing, or requires work outside the system, classify the problem at the lowest level that explains it.

### Interaction-level pain

A control, form, label, state presentation, or local interaction is confusing or burdensome.

Possible responses may include wording, information hierarchy, validation, defaults, affordance, or interaction redesign.

### Journey-level pain

The problem spans several steps, people, systems, waits, approvals, artifacts, or handoffs needed to reach the outcome.

Possible responses may include responsibility/visibility changes, removal of duplication, workflow simplification, a bounded integration, or keeping a deliberate manual step.

### Relationship/system-level pain

The problem recurs across interactions because the business lacks stable authority, language, policy, information ownership, or another structural condition.

Possible responses may require a domain/policy decision rather than a screen or workflow change.

A local UI redesign cannot fix an authority problem. A workflow engine cannot resolve disputed business meaning by itself. An integration cannot repair an unclear product responsibility.

## 4. Complexity-source prompts

When useful, inspect five sources of complexity as **diagnostic prompts**, not as a taxonomy SquiFlow must model:

```text
Integration
- external systems, providers, devices, handoffs, dependencies

Information
- volume, fragmentation, freshness, ambiguity, provenance, missing context

Intention
- changing goals, exceptions, alternative desired outcomes

Environment
- physical setting, interruptions, network/device constraints, shared workspaces

Institutional
- authority, policy, contractual/legal rules, organizational practice, incentives
```

A problem may span several prompts. Do not create a new domain entity, service, tenant type, or configuration category merely because one prompt applies.

For each material complexity ask:

1. Is it required by the business outcome or a protected invariant?
2. Is it caused by the current manual/business process rather than by SquiFlow?
3. Would SquiFlow introduce new complexity if it attempted to solve it?
4. Who bears the complexity today and who would bear it after the proposed change?
5. What is the smallest response that preserves the required meaning and safety?

## 5. Exploration artifacts have different purposes

Do not treat every design artifact as a specification or commitment.

### Disposable exploration

Purpose: think, expose alternatives, discover assumptions, or communicate rough possibilities.

Expected behavior:
- intentionally cheap;
- may be incomplete or contradictory;
- can be thrown away without migration or preservation work;
- does not become evidence merely because effort or polish was invested in it.

### Evidence prototype

Purpose: answer a named uncertainty through observation or technical proof.

Expected behavior:
- fidelity is only as high as needed to distinguish meaningful alternatives;
- learning goal and evidence limits are explicit;
- observations are recorded with normal evidence provenance;
- it may be discarded after the question is answered.

### Delivery specification

Purpose: communicate accepted behavior/constraints to implementation when a durable specification is actually useful.

Expected behavior:
- reflects an accepted product/domain decision rather than an unexplored idea;
- references the applicable owner requirement/decision;
- is maintained only while it provides implementation value.

Use this rule:

```text
artifact fidelity
≠ evidence strength

artifact effort
≠ obligation to keep it

working prototype
≠ accepted product responsibility
```

## 6. AI/generated exploration restraint

AI-generated screens, flows, code, domain diagrams, stories, prioritization, or prototypes remain **INFERENCE / HYPOTHESIS** unless supported by separate evidence or deliberately accepted as a normative decision.

More detailed prompting may improve compliance with the prompt. It does not establish that:

- the problem exists;
- the terminology is correct;
- the workflow matches real work;
- the proposed interaction is usable in context;
- the solution is valuable or viable;
- the architecture is justified.

A generated artifact can be a cheap disposable exploration or evidence prototype when its limitations are explicit. Generated polish must not raise its evidence status.

## 7. Customer-research evidence handling

Product discovery may require interviews, observation, screenshots, recordings, business artifacts, transaction traces, support evidence, or other real customer material. This evidence can contain personal, financial, security-sensitive, or commercially confidential information.

Before collecting material evidence, use the smallest safe approach appropriate to the question.

### Permission and transparency

Where applicable:
- obtain permission for interviews/observation;
- obtain explicit permission before audio/video/screen recording;
- explain the purpose of collecting the material and how it will be used;
- do not collect covertly merely because collection is technically possible.

### Data minimization

Collect only information needed to answer the research question.

Prefer:
- redacted screenshots/artifacts;
- synthetic examples where real details add no evidence value;
- notes about the observed behavior rather than copies of entire customer datasets;
- the smallest excerpt needed to preserve evidence.

### Sensitive material

Treat as sensitive when present, including:
- personal names/contact details;
- customer/supplier records;
- passwords, tokens, credentials, API keys or secrets;
- payment/account details;
- identity/security information;
- confidential prices/contracts/business documents;
- private artwork/files;
- internal business processes whose disclosure could harm the participant/business.

### Repository rule

The SquiFlow GitLab repository is public. Therefore:

```text
DO NOT commit raw customer PII,
credentials/secrets,
payment/account data,
private customer/supplier records,
or confidential customer artifacts
into the public repository.
```

Repository evidence records should contain sanitized conclusions, provenance, relevant context, decision impact, and—when needed—redacted/synthetic examples rather than the sensitive raw source.

If raw evidence must be retained outside the repository, record only the minimum reference needed to locate it under the applicable private storage/access process. Do not invent or promise a retention period before the actual research/storage/legal context is known; minimize retention and delete raw evidence when it no longer has justified research value.

### Access and reuse

Do not reuse customer research material for unrelated purposes merely because it has already been collected. Restrict access to people who need the raw evidence for the stated work.

Research safety does not convert reported/observed evidence into product truth. Evidence still requires normal interpretation, adversarial review, and context-bound generalization.

## 8. Research evidence record

For consequential customer evidence, preserve the smallest useful subset of:

```text
research question / decision affected
participant/business context
who supplied the evidence
whose work/outcome it describes
evidence mode: reported / observed / artifact / measurement
what was actually seen/heard/measured
what was redacted or unavailable
interpretation / inference
alternative interpretation
privacy/confidentiality handling where material
decision implication
revisit/generalization limit
```

Do not turn this into a compulsory form for trivial conversations. Use it when losing provenance could materially change a product/domain decision.

## 9. Relationship to other owners

- `docs/product/PRODUCT_FOUNDATION.md` remains the upstream product/evidence authority and owns PFQ-003 plus the evidence taxonomy.
- `docs/domain/BUSINESS_MODEL.md` owns accepted practical domain behavior and the small-business usability rule.
- `docs/domain/BUSINESS_TERMS.md` owns consequential semantic conclusions/disputes after discovery surfaces them.
- `docs/requirements/PRODUCT_TO_QUALITY_TRACEABILITY.md` owns business-to-quality traceability when discovered complexity creates a material quality requirement.
- focused security/privacy/domain owner documents remain authoritative for product/runtime privacy and security behavior; this file only governs research-evidence handling.

## 10. Restraint

This document does **not** require:

- a UX department;
- ResearchOps staff;
- a formal design process;
- a mandatory research repository product;
- a prototype for every decision;
- parallel design for every decision;
- a design-system maturity model;
- a new product capability merely because research finds complexity.

Use these rules only as far as the consequence of being wrong justifies the evidence cost.