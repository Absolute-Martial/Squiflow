# Product & Requirements Foundation — Batch 6 Decision Patch

**Coverage:** sources `131–156`  
**Source families:** Nielsen Norman Group `131–152`; Product Talk `153–154`; Carnegie Mellon SEI `155–156`  
**Repository baseline reviewed:** `main @ 36b0d5f2f4d2c83181c253648f2d92256f16cec6`  
**Status:** Decision/review record. Current semantics remain owned by the focused owner documents changed or referenced by this patch.

## 1. Batch conclusion

Batch 6 does not justify a new product capability, architecture pattern, design-system program, ResearchOps function, prioritization framework, roadmap process, or architecture-review bureaucracy.

The durable findings are narrower:

1. SquiFlow should remove accidental complexity without hiding essential business/domain complexity;
2. reported friction should be located at interaction, journey, or structural/system level before selecting a response;
3. integration, information, intention, environment, and institutional complexity are useful diagnostic prompts, not domain taxonomy;
4. exploratory artifacts should be distinguished from evidence prototypes and delivery specifications so polish/sunk effort does not become commitment;
5. real customer research needs minimal permission, data-minimization, sensitive-data, access/retention, and public-repository handling rules;
6. SEI quality-attribute material independently supports the existing product→quality→architecture→verification traceability and therefore does not justify another architecture methodology.

## 2. Source-to-decision traceability

| ID | Source implication | SquiFlow disposition |
|---|---|---|
| 131 | Design-system maturity is multidimensional and contextual. | ALREADY ADDRESSED / DEFER formal design system until evidence triggers it. |
| 132 | Feedback loses value when contributors do not learn what happened to it. | APPLY lightly through decision/review rationale; no critique ceremony required. |
| 133 | Rough/disposable artifacts reduce premature commitment and sunk-cost attachment. | APPLY: distinguish disposable exploration from evidence prototypes and delivery specifications. |
| 134 | Product information can be fragmented across research/support/analytics/planning. | ALREADY ADDRESSED through evidence triangulation; no information-pipeline platform. |
| 135 | Experienced teams adapt/compress design reasoning rather than following one fixed process. | ALREADY ADDRESSED; preserve reasoning obligations, not process ritual. |
| 136 | Postmortems can expose systemic learning from surprising failure/success. | APPLY only when outcome materially surprises us; no mandatory postmortem process. |
| 137 | A real design system needs stewardship/governance. | DEFER with the design system itself; no `design-system enforcer` role now. |
| 138 | Detailed prompts improve generated interface output but do not replace judgment. | ALREADY ADDRESSED: generated output remains inference/hypothesis. |
| 139 | AI prototyping can follow instructions while missing contextual trade-offs. | ALREADY ADDRESSED; AI prototype remains hypothesis/evidence instrument, not authority. |
| 140 | Designer/developer collaboration improves when both own product outcome. | ALREADY ADDRESSED by early feasibility/product collaboration; no role model required. |
| 141 | Complex applications require understanding domain work, environment, actors and constraints. | APPLY STRONGLY as complexity-diagnosis discipline. |
| 142 | Parallel/iterative design can expose alternatives and reduce premature commitment. | APPLY principle selectively; no mandatory parallel-design step. |
| 143 | Award-winning enterprise intranet case material. | BACKGROUND ONLY; weak direct evidence for SquiFlow SMB scope. |
| 144 | Enterprise intranet trends include accessibility/stakeholder/persona themes. | BACKGROUND / current human-operability direction already compatible; no persona import. |
| 145 | Prioritization frameworks are context-dependent. | OPTIONAL heuristics only; scores do not become decision authority. |
| 146 | Problem statements help frame the problem before solutions. | ALREADY STRONGLY ADDRESSED by problem-first discovery. |
| 147 | Pain can live at interaction, journey, or relationship/system level. | APPLY: locate pain level before selecting UI/workflow/integration/domain response. |
| 148 | `How might we` questions can widen solution space. | OPTIONAL wording technique. |
| 149 | Roadmaps should communicate strategic problems/outcomes rather than feature lists. | APPLY principle; do not add roadmap artifact while first customer/outcome remains open. |
| 150 | Roadmapping clusters/prioritizes/revisits future problems. | BACKGROUND; no additional planning process. |
| 151 | Research operations include consent, privacy, storage, retention and PII handling. | APPLY minimally to customer-research evidence handling; reject organizational ResearchOps import. |
| 152 | Complexity may originate in integration, information, intention, environment or institution. | APPLY as diagnostic prompts, not SquiFlow taxonomy. |
| 153 | Curated links about experiments/prioritization/pivots. | BACKGROUND ONLY; secondary curation is insufficient independent requirement evidence. |
| 154 | One experiment gives context-bound evidence; related hypotheses can build knowledge. | ALREADY ADDRESSED / APPLY: reinforce first-customer generalization restraint. |
| 155 | Attribute-Driven Design derives architecture from functional needs, constraints and quality-attribute scenarios. | ALREADY ADDRESSED by `PRODUCT_TO_QUALITY_TRACEABILITY.md`; do not import ADD methodology. |
| 156 | Early architecture analysis evaluates quality requirements and risks. | ALREADY ADDRESSED by traceability, NFR model and verification gates. |

## 3. Accepted changes

### B6-P1 — Complexity and pain-location diagnostic

Create a focused discovery companion that distinguishes:

```text
essential complexity
vs
accidental complexity
```

and asks whether reported friction lives primarily at:

- interaction level;
- journey/handoff level;
- relationship/system level.

Use integration, information, intention, environment and institutional constraints as prompts only. Do not convert them into product modules, domain entities, tenant types, or configuration categories.

`docs/domain/BUSINESS_MODEL.md` is refined so `small-business usability` means removing accidental complexity without hiding required states such as payment uncertainty, local/server authority, pending work, or correction history.

### B6-P2 — Disposable exploration and prototype restraint

Operationalize three artifact purposes:

```text
disposable exploration
= think / expose alternatives; expected to be thrown away

evidence prototype
= answer a named uncertainty; fidelity proportional to the question

delivery specification
= communicate an accepted behavior/constraint where durable specification adds value
```

Polish, implementation effort, AI generation, or a working UI does not increase evidence status or create product commitment.

### B6-P3 — Customer-research evidence handling

Before collecting real customer evidence, use the smallest safe approach appropriate to the question:

- appropriate permission/consent for interview/observation and explicit permission before recording;
- data minimization/redaction;
- sensitive-data identification;
- access limited to the research need;
- minimal justified retention;
- sanitized evidence/decision records in the repository.

Because the SquiFlow GitLab repository is public, raw customer PII, credentials/secrets, payment/account data, private customer/supplier records, and confidential customer artifacts must not be committed to it.

This is research-process safety, not a new SquiFlow runtime privacy subsystem.

## 4. Architecture consistency result

Sources 155–156 independently support the existing chain:

```text
business/product outcome
→ consequence if quality is poor
→ quality characteristic / scenario
→ relative trade-off
→ architecture consequence
→ verification evidence
```

The current owners already express this:

- `docs/product/PRODUCT_FOUNDATION.md` — product consequence and evidence;
- `docs/requirements/PRODUCT_TO_QUALITY_TRACEABILITY.md` — lightweight bridge;
- `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md` — quality/failure/degraded semantics;
- `docs/testing/VERIFICATION_STRATEGY.md` — proof obligations.

They were not edited merely to duplicate SEI terminology.

## 5. Constructive adversarial review

### Risk: `essential complexity` becomes a justification for poor UX

Mitigation: complexity is essential only when removing/hiding it would lose a real business responsibility, invariant, authority distinction, or consequence. Presentation and workflow can still be improved aggressively.

### Risk: complexity prompts become another taxonomy/framework

Mitigation: they are diagnostic prompts only. No schema/module/tenant/configuration structure follows from the labels.

### Risk: artifact classes create another mandatory process

Mitigation: the classes explain intent; they do not require every idea to pass through all three. Implementation itself may be the cheapest experiment.

### Risk: research-safety rules become heavy ResearchOps bureaucracy

Mitigation: use the smallest safeguards proportionate to collected evidence. No research team/tooling/process is mandated.

### Risk: privacy language is mistaken for product/runtime privacy requirements

Mitigation: focused runtime/product privacy/security owners remain authoritative. This patch only governs research evidence used by the product study.

## 6. Question-register impact

No PFQ-009 is added.

- complexity/pain diagnosis belongs under PFQ-003;
- first-customer evidence handling applies across the existing PFQs;
- prioritization remains part of PFQ-005;
- quality scenarios remain linked to PFQ-002/PFQ-008 plus Product-to-Quality Traceability.

## 7. Visuals inspected

Meaningful visuals inspected during the review included:

- source 131: multidimensional design-system maturity radar/profile;
- source 142: iterative design/evaluation flow across fidelity stages;
- source 152: nested integration/information/intention/environment/institution complexity model.

These visuals informed interpretation but were not treated as independent SquiFlow requirements.

## 8. Source URLs

- 131 `https://www.nngroup.com/articles/design-system-maturity/`
- 132 `https://www.nngroup.com/articles/after-design-critique/`
- 133 `https://www.nngroup.com/articles/design-disposables/`
- 134 `https://www.nngroup.com/articles/information-pipeline/`
- 135 `https://www.nngroup.com/articles/design-process-isnt-dead/`
- 136 `https://www.nngroup.com/articles/ux-postmortems/`
- 137 `https://www.nngroup.com/articles/design-system-enforcer/`
- 138 `https://www.nngroup.com/articles/vague-prototyping/`
- 139 `https://www.nngroup.com/articles/ai-prototyping/`
- 140 `https://www.nngroup.com/articles/developer-designer-relationship/`
- 141 `https://www.nngroup.com/articles/strategies-complex-application-design/`
- 142 `https://www.nngroup.com/articles/parallel-and-iterative-design/`
- 143 `https://www.nngroup.com/articles/intranet-design/2022/`
- 144 `https://www.nngroup.com/articles/intranet-trends/2022-intranet-trends/`
- 145 `https://www.nngroup.com/articles/prioritization-methods/`
- 146 `https://www.nngroup.com/articles/problem-statements/`
- 147 `https://www.nngroup.com/articles/pain-points/`
- 148 `https://www.nngroup.com/articles/how-might-we-questions/`
- 149 `https://www.nngroup.com/articles/roadmap-types/`
- 150 `https://www.nngroup.com/articles/roadmapping-steps/`
- 151 `https://www.nngroup.com/articles/research-ops-101/`
- 152 `https://www.nngroup.com/articles/complex-application-design-framework/`
- 153 `https://www.producttalk.org/weekend-reading-ab-testing-is-hard-to-pivot-or-not-and-more/`
- 154 `https://www.producttalk.org/build-knowledge-by-expanding-on-previous-hypotheses/`
- 155 `https://www.sei.cmu.edu/library/attribute-driven-design-method-collection/`
- 156 `https://www.sei.cmu.edu/library/early-analysis-of-software-architecture/`
