# Product & Requirements Foundation — Batch 4 Decision Patch

**Source IDs:** 079–104  
**Source family:** Thoughtworks  
**Repository baseline reviewed:** `main` at `3aa575124d0bd7b40b01ba09d058e37fe83896b5` with Batch 3 already merged via MR !33  
**Status:** Decision/application record for the approved Batch 4 documentation patch. This review record is evidence and rationale; it does not override owner documents.

## 1. Batch 4 result

Batch 4 focused on discovery/delivery, third-party integration, QA/risk, measurement, documentation, predictability and product-management practice.

The review did **not** justify a generic integration service/framework, QA bureaucracy, fixed discovery process, AI-generated backlog authority, metrics platform, story-point standard, classes-of-service framework, or weakening requirements traceability in favor of tests alone.

The applicable findings were narrower:

1. every external integration needs an explicit responsibility/authority/freshness/failure/recovery contract;
2. outcome, operational, delivery, engineering and consumption metrics must not silently substitute for one another;
3. generated product artifacts are hypotheses/inference unless independently supported or deliberately accepted as a normative decision;
4. reported friction should be decomposed into waiting, handoff, information, authority and bottleneck causes before SquiFlow automates it;
5. documentation, tests, code, source studies and review records have different authority roles;
6. every assistant/source-driven conclusion gets a constructive adversarial pass before acceptance.

## 2. Applied owner changes

### B4-P1 — integration responsibility and authority

**New focused owner:** `docs/integrations/INTEGRATION_RESPONSIBILITY_AND_AUTHORITY.md`

The document defines common questions that every material provider boundary must answer while preserving focused owners for identity, OpenFGA authorization, object storage, backup, notifications/webhooks and future payments/integrations.

It explicitly rejects a global `provider is source of truth` formulation. Authority must be scoped to a named fact/effect.

The document creates no runtime service or generic provider abstraction.

### B4-P2 — documentation truth hierarchy

**Owner changed:** `docs/product/PRODUCT_FOUNDATION.md`

The repository now distinguishes:

```text
owner document = current authoritative semantics
review/decision record = evidence, alternatives and rationale
test/executable evidence = proof of implemented behavior against a requirement
source study = external contribution and interpretation
code = implemented mechanism, which may still be incomplete
```

Tests complement requirements rather than replacing product intent, evidence status or historical rationale.

### B4-P3 — measurement/proxy discipline

**Owner changed:** `docs/requirements/PRODUCT_TO_QUALITY_TRACEABILITY.md`

Measures are classified as product-outcome evidence, operational/NFR evidence, delivery-progress metrics, engineering/code-quality proxies, or authoritative consumption accounting.

A metric must state what it means, why it matters, which decision it can change, what behavior it may incentivize, and what important reality it may fail to capture.

No metrics/analytics platform is introduced.

### B4-P4 — generated discovery remains hypothesis

**Owner changed:** `docs/product/PRODUCT_FOUNDATION.md`

AI/assistant-generated stories, personas, missing requirements, journey completions, priorities and domain concepts remain `INFERENCE / HYPOTHESIS` unless independently supported or deliberately accepted as a normative Owner decision.

### B4-P5 — friction/wait-state analysis

**Owner changed:** `docs/product/PRODUCT_FOUNDATION.md`

The problem-first trace now explicitly looks for waiting/queues/bottlenecks and requires investigation before assuming automation or a new capability is the correct response.

No new PFQ was added; this belongs under PFQ-003 rather than inflating the active question list.

### B4-P6 — constructive adversarial self-review

**Owner changed:** `docs/product/PRODUCT_FOUNDATION.md`

Every material conclusion/patch must challenge the strongest plausible case that it is wrong or overstated, hidden assumptions, simpler and genuinely different alternatives, downstream costs, falsifying evidence and anchoring to the current model.

The rule also has a stop condition: end the adversarial pass when further criticism is unlikely to change the decision, evidence need, scope, safety classification or next reversible action.

This is intended to create skeptical creativity, not paralysis.

## 3. Constructive adversarial review of the study so far

This section deliberately challenges the assistant's own prior work.

### Batch 1 challenge

**Possible overreach:** creating a Product Foundation could turn uncertainty-management into a new documentation layer before real customer discovery exists.

**Why it remains justified:** the document does not claim an initial customer/problem as fact; it primarily records what is unknown and prevents downstream architecture maturity from masquerading as product evidence.

**Constraint:** do not keep expanding it into a comprehensive product-management framework. Real customer evidence should close or simplify sections over time.

### Batch 2 challenge

**Possible overreach:** `configuration is behavioral design` could bias the study against legitimate configurability and overvalue opinionated defaults.

**Counter-case:** different businesses may genuinely need frequent safe variation; strong defaults alone could become rigid or exclusionary.

**Result:** keep the rule as a trade-off, not an anti-configuration principle. Repeated, consequential variation can justify configuration or a reusable capability when evidence supports it.

### Batch 3 challenge

**Possible overreach:** `PRODUCT_TO_QUALITY_TRACEABILITY.md` risks becoming duplicate NFR bureaucracy.

**Result:** Batch 4 adds an explicit bridge-restraint rule. It stores only the upstream business reason/trade-off and links to the focused owner. If detailed retry/security/sync/SLO semantics accumulate there, they must move back to their owners.

**Alternative considered:** put all product-to-quality rationale directly into `NON_FUNCTIONAL_REQUIREMENTS.md`. Rejected for now because that would mix product evidence/priority with the NFR owner's cross-cutting technical semantics and make NFRs claim evidence they do not own.

### Batch 4 challenge

**Possible overreach:** a shared integration document can become the generic abstraction that the repository repeatedly rejects.

**Result:** the new document owns questions and semantic boundaries only. It does not define an adapter hierarchy, integration service, event bus, SDK wrapper or lifecycle framework. Provider-specific behavior remains in focused owners.

**Alternative considered:** put the common rules into notifications/external delivery. Rejected because identity, authorization, storage, backup and payments have different authority structures and should not be modeled as notification delivery.

### Study-wide anchoring challenge

**Risk:** repeated source review may reward ideas that are easy to map onto existing SquiFlow documents while failing to expose entirely different product responsibilities.

**Mitigation retained:** every batch must continue the independent problem-first view before mapping findings into current owners. A good finding is allowed to simplify, remove, narrow, keep work manual, or expose a missing responsibility rather than merely add a document or capability.

### Study-wide skepticism challenge

**Risk:** the new adversarial-review rule itself could create endless debate and make every provisional decision feel unsafe.

**Mitigation:** use the stop condition. Reversible product decisions can proceed provisionally with labelled uncertainty; only high-harm/irreversible commitments require stronger closure.

## 4. Source findings that materially support the patch

The following supplied sources were especially relevant. They are practitioner/methodology evidence, not SquiFlow customer validation.

| ID | Source | Applied implication |
|---|---|---|
| 079 | Thoughtworks client discovery/delivery | Make discovery outcomes/scope changes explicit rather than silently drifting. |
| 080–081 | Third-party integration series | Identify provider/SquiFlow responsibilities, trust, data/resource authority and breaking-change behavior before implementation. |
| 083 | Successful discovery | Understand goals, actors, context, dependencies and pain points without adopting a fixed discovery ceremony. |
| 085 | What to measure and how | Define what a measure means, why it matters and what decision it can change. |
| 087 | Questionable product-quality metrics | Coverage/test count can be useful proxies but must not become product-quality truth; its anti-traceability implication is rejected. |
| 093 | Metric-driven management | Technical task progress can hide end-to-end product responsibility. |
| 094 / 100 | Agile documentation series | Documentation should have purpose, context and limited duplication; consequential decisions need rationale/history. |
| 097 | BA question bank | Questions are prompts for discovery, not a mandatory checklist. |
| 098 | Bug triage | Consequence/exposure matter more than labels alone. |
| 101 | Hyperagility / generated stories | Generated stories/priorities may aid ideation but are not customer evidence. |
| 102 | Kill a product | Sunk effort must not protect a weak product hypothesis. |
| 104 | Reducing cycle time | Investigate waiting/bottlenecks before adding resources or automation. |

Source 092 exposed only limited introductory material during review and was not used for a material requirement.

## 5. Cross-owner consistency result

The current identity/session, tenant authorization, object-storage/backup and notifications/webhooks owners were inspected before introducing the shared integration contract.

Their existing semantics already satisfy the new common rule in the important areas:

- ZITADEL authentication is explicitly separated from SquiFlow membership and OpenFGA/domain authorization;
- OpenFGA permission is explicitly separated from tenant DB isolation and business-state validity;
- object-provider bytes are separated from SquiFlow object ownership/history semantics;
- notification/provider failure is separated from the originating business fact;
- NFRs already require external-delivery failure isolation and `OutcomeUnknown` where acknowledgement is ambiguous.

Those documents therefore were not duplicated merely to add the new shared checklist. If a future integration changes one of their contracts, the Product Foundation cross-owner rule requires a direct update.

## 6. Explicitly not adopted from Batch 4

This patch does **not** add or require:

- generic integration service/framework;
- API gateway expansion or enterprise service bus;
- one provider abstraction for every dependency;
- automated/AI backlog authority;
- generic Product Owner role/process;
- fixed discovery duration or checklist ceremony;
- QA bureaucracy or test-count targets;
- story-point or hours-remaining standards;
- classes-of-service framework;
- metrics/data/analytics platform;
- requirements replaced by tests;
- automatic workflow optimization.

## 7. Verification

Checked after application:

- no runtime architecture or code is changed;
- no provider becomes globally authoritative merely because it is external;
- provider-specific owners retain exact semantics;
- Product Foundation explicitly labels generated analysis as inference/hypothesis;
- proxy metrics remain scoped to the decisions they actually inform;
- the new adversarial review includes alternatives and falsification, plus a stop condition against paralysis;
- no new high-priority PFQ is added;
- Batch 3's traceability bridge is narrowed rather than expanded into a second NFR catalogue.

Future batches must apply the same constructive adversarial review before patches are accepted.