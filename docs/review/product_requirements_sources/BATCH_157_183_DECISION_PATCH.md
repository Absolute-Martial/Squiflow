# Product & Requirements Foundation — Batch 7 Decision Patch

**Coverage:** sources `157–183`  
**Source families:** Product Talk, Carnegie Mellon SEI, Martin Fowler, Open Practice Library  
**Repository baseline reviewed:** `main @ 868727104eba55dd1811129159aa12a0f2827fdc`  
**Status:** Final batch decision/review record. Current semantics remain owned by the focused owner documents changed or referenced by this patch.

## 1. Batch conclusion

Batch 7 completes the 183-source Product & Requirements Foundation review.

The durable findings are:

1. generative discovery evidence and evaluative solution evidence must not be silently substituted for each other;
2. material product responses should be decomposed into consequential assumptions rather than treated as one indivisible hypothesis;
3. deliberate investigations should state in advance what decision they can change and how supporting/refuting/inconclusive evidence will be interpreted;
4. the existing Product Foundation risk check remains valid, with a concrete additional harm/responsibility dimension where a response can burden, mislead, exclude, surveil, financially expose, or otherwise harm an affected actor;
5. material decision changes need explicit supersession/history so current semantics can evolve without rewriting past rationale;
6. the same business word may legitimately have different canonical meanings in different contexts when real identity/lifecycle/authority/invariants differ, without implying a service, database, deployment, or `TenantType`;
7. Event Storming/DDD techniques may support collaborative discovery but do not become automatic architecture boundaries;
8. statistical guidance that interprets p-values/confidence as the probability a hypothesis/result is true is rejected.

No new runtime architecture, product capability, experiment platform, statistical framework, domain-service decomposition, or top-level PFQ is justified by this batch.

## 2. Source-to-decision traceability

| ID | Source implication | SquiFlow disposition |
|---|---|---|
| 157 | Earlier/rougher customer collaboration can expose needs and alternatives before solution commitment. | APPLY / ALREADY ADDRESSED through disposable exploration and first-customer discovery; customer request still does not equal requirement. |
| 158 | Business goals can drive quality attributes/architecture, while some goals require non-architectural responses; stakeholder and beneficiary may differ. | ALREADY STRONGLY ADDRESSED by Product Foundation, actor/burden analysis, and Product-to-Quality Traceability; do not import PALM. |
| 159 | Concrete quality scenarios help elicit/clarify quality-attribute requirements. | ALREADY ADDRESSED by NFR and product-to-quality scenario discipline; no QAW ceremony. |
| 160 | Material decisions benefit from preserving context, rationale, alternatives, consequences, and supersession rather than silently rewriting history. | APPLY through `docs/decisions/MATERIAL_DECISION_HISTORY.md`; no ADR-per-change bureaucracy. |
| 161 | Candidate responses rely on value/desirability, viability, feasibility, usability, and ethical assumptions. | APPLY SELECTIVELY: retain SquiFlow's existing risk vocabulary and add harm/responsibility as a concrete diagnostic dimension. |
| 162 | Test underlying assumptions rather than treating an entire solution as one indivisible hypothesis. | APPLY to material responses; investigate consequential assumptions only. |
| 163 | Good experiment design states learning goal, method, participants/context, measures/thresholds, and interpretation before results. | APPLY STRONGLY as decision-changing precommitment. |
| 164 | Observation and explanation/theory are distinct; learning moves between generating and testing explanations. | ALREADY ADDRESSED by OBSERVATION versus INFERENCE; reinforced by generative/evaluative distinction. |
| 165 | Not every possible assumption/test deserves investigation; judgment and learning value matter. | ALREADY ADDRESSED by question budget/evidence cost; no mandatory testing matrix. |
| 166 | Hypothesis testing requires care in defining what is actually being learned. | BACKGROUND / ALREADY ADDRESSED. |
| 167 | Enterprise/on-site contexts can use interviews, prototypes, usability tests, and other experiments when A/B testing is impractical. | APPLY: `experiment` is broader than production A/B testing. |
| 168 | Testing costly assumptions before implementation can avoid unnecessary build/maintenance work. | APPLY WITH CAVEAT: no absolute `before code` rule; an executable spike may be the cheapest credible evidence. |
| 169 | Confidence does not replace surfaced assumptions and disconfirming evidence. | ALREADY ADDRESSED by evidence taxonomy and adversarial review. |
| 170 | Expected impact should connect proposed change to baseline/comparable evidence, cost, maintenance, and opportunity cost. | ALREADY ADDRESSED / SUPPORT current product-response and opportunity-cost discipline. |
| 171 | Low traffic does not mean no learning; qualitative/contextual methods answer different questions from powered statistical experiments. | APPLY STRONGLY for first-customer discovery; preserve generalization limits. |
| 172 | The source explicitly says its older five-component hypothesis format has been revised by later guidance. | SUPERSEDED; preserve historical source status but do not import obsolete format. |
| 173 | Diagnose whether a difficulty is in the problem/value, solution, detailed design, or feasibility before acting at the wrong level. | ALREADY ADDRESSED by WHY→WHAT→HOW, problem-first discovery, and pain-location diagnosis. |
| 174 | A test should state what it wants to learn and what action follows from supported/refuted/inconclusive evidence. | APPLY STRONGLY to investigation precommitment. |
| 175 | Research/evidence method must fit the question; qualitative and quantitative methods have different inference/generalization limits. | APPLY SELECTIVELY without rigid research-process rules. |
| 176 | Older article presents simplified/misleading statistical interpretations and a mechanical confidence threshold. | REJECT statistical guidance; no universal `p < .05`/95% decision rule. |
| 177 | Product evidence must be tied to the relevant customer/context rather than a universal audience. | ALREADY ADDRESSED by PFQ-001/002 and first-customer learning boundary; no TenantType inference. |
| 178 | Data/observation and interpretation can diverge; derived conclusions need explicit semantics and challenge. | ALREADY ADDRESSED by evidence taxonomy, measurement semantics, and adversarial review. |
| 179 | Testing everything wastes effort; choose consequential assumptions and appropriate methods before testing. | APPLY STRONGLY through evidence-cost and decision-changing investigation rules. |
| 180 | The same term can have distinct canonical meanings in different bounded semantic contexts with explicit mappings. | APPLY semantic lesson; explicitly reject automatic service/database/process/deployment/TenantType inference. |
| 181 | Legacy displacement can use stable capability seams while warning against exhaustive upfront decomposition. | MOSTLY NOT APPLICABLE because SquiFlow is not a legacy-displacement program; no new service/capability boundaries inferred. |
| 182 | Discovery has generative work about customer needs/opportunities and evaluative work testing candidate responses. | APPLY STRONGLY; made explicit in the discovery/evidence companion. |
| 183 | Event Storming can expose events, language, handoffs, and model disagreements collaboratively. | OPTIONAL TECHNIQUE / PARTIAL REJECT: workshop output remains hypothesis; aggregates/groupings do not automatically become microservices. |

## 3. Accepted changes

### B7-P1 — Generative versus evaluative evidence

`docs/product/DISCOVERY_COMPLEXITY_AND_RESEARCH_EVIDENCE.md` now distinguishes:

```text
generative evidence
= understand actual work, outcomes, context, language, pain, workarounds, authority

evaluative evidence
= test a candidate response or a material assumption about that response
```

A successful prototype does not establish that the underlying problem is important enough to prioritize. Observing a real problem does not establish that the first proposed response is correct.

### B7-P2 — Assumption decomposition plus harm/responsibility

The existing Product Foundation risk check already says to examine **at least** value, usability, feasibility, and business viability. Therefore no duplicate Product Foundation section was added merely for symmetry.

The focused discovery companion operationalizes an additional harm/responsibility diagnostic where relevant:

```text
Who benefits?
Who bears operational/cognitive/financial/privacy/security cost?
Could the response mislead, exclude, overburden, surveil, or financially expose someone?
What data is collected/retained/exposed and why?
Could a safer/less burdensome response achieve the same result?
```

This does not create an ethics committee/process.

### B7-P3 — Decision-changing investigation precommitment

Before a non-trivial test, experiment, prototype evaluation, pilot, or POC, record enough to know why the investigation is worth doing:

```text
decision affected
→ assumption / uncertainty
→ customer / actor / context
→ method and method-fit
→ evidence to collect
→ interpretation limits
→ action if supported
→ action if refuted
→ action if inconclusive
```

If all plausible results lead to the same action, skip the investigation unless another explicit safety/qualification purpose exists.

The patch also states that low traffic does not mean no learning and rejects mechanical statistical significance as the sole product/business decision rule.

### B7-P4 — Material decision history and supersession

Created `docs/decisions/MATERIAL_DECISION_HISTORY.md` as the focused owner for historical decision supersession.

Current semantics remain in focused owner documents and `CURRENT_DECISIONS.md`; unresolved choices remain in `OPEN_DECISIONS.md`. Material historical rationale is preserved separately so a later decision can explicitly supersede an earlier one without rewriting the old context.

A separate ADR is not required when an existing audit/review record already preserves the necessary context/rationale. The convention is intentionally limited to decisions whose historical rationale materially matters.

No direct edit to `CURRENT_DECISIONS.md` was needed: it already identifies current accepted direction and points to the decision audit for reasoning. Adding duplicate current-rule prose there would not change its contract.

### B7-P5 — Context-specific semantic meaning

`docs/domain/BUSINESS_TERMS.md` now permits distinct canonical meanings for the same word when evidence establishes a real semantic boundary.

Where this occurs, the relevant meaning/mapping should make explicit:

```text
context A meaning / owner
context B meaning / owner
shared identity / translation if any
information that may cross
information that must not be assumed equivalent
```

The file also now states explicitly:

```text
legitimate semantic context
≠ service boundary
≠ process boundary
≠ database boundary
≠ deployment boundary
≠ TenantType
```

A semantic boundary may live entirely inside the modular monolith. Runtime extraction requires an independently justified deployment/failure/scaling/security/ownership/operational reason.

## 4. Superseded/rejected source handling

Two final-batch sources require explicit non-adoption rather than silent omission.

### Source 172 — superseded by its own author

The older hypothesis-format article explicitly points to later revised guidance. It remains part of the corpus/history but is not used as the current experiment template.

### Source 176 — statistical interpretation rejected

SquiFlow does not adopt the source's simplified probability interpretation of p-values/confidence or a universal 95%/`p < .05` threshold.

Statistical inference, when later justified by sufficient data and an appropriate design, must be interpreted according to the actual method/assumptions and remains one evidence mode among others. Product/business decisions do not become correct merely because one threshold is crossed.

## 5. DDD/Event Storming restraint

Batch 7 does not establish a DDD architecture mandate.

Use collaborative semantic techniques only when they help answer a real discovery/domain question. Their artifacts remain hypotheses/models until supported and accepted through the normal owner/evidence process.

In particular:

```text
bounded semantic context
≠ microservice

Event Storming aggregate
≠ automatic aggregate implementation
≠ automatic service
```

The anti-`TenantType` rule remains unchanged.

## 6. Constructive adversarial review

### Risk: generative/evaluative labels become process bureaucracy

Mitigation: they classify what evidence can support; no separate team, phase, backlog, or artifact is required.

### Risk: harm/responsibility becomes vague moral gatekeeping

Mitigation: use concrete actor/data/burden/financial/privacy/security/trust consequences only when material to the response. It is a diagnostic question, not a veto role.

### Risk: precommitment becomes experiment ceremony for trivial choices

Mitigation: apply it only to non-trivial deliberate investigations where post-hoc reinterpretation or evidence cost is material.

### Risk: decision history duplicates current owner docs

Mitigation: current owner documents retain only current semantics. History records rationale/supersession and does not become alternate authority.

### Risk: semantic contexts become architecture decomposition

Mitigation: runtime boundaries require independent failure/deployment/scaling/security/ownership evidence. Semantics alone are insufficient.

## 7. Question-register impact

No PFQ-009 is added.

- generative discovery belongs under PFQ-003;
- decision-changing evidence belongs under PFQ-005;
- customer/context boundaries remain under PFQ-001/PFQ-007;
- quality scenarios remain under PFQ-002/PFQ-008 plus Product-to-Quality Traceability.

The question budget remains intentionally small.

## 8. Final corpus synthesis

The 183-source study does not justify turning SquiFlow into a catalogue of external frameworks or patterns.

The durable reasoning chain is:

```text
business goal / product promise
→ relevant customer / actor / context
→ observed/reported work and problem evidence
→ semantic/domain understanding
→ candidate product responsibility
→ material assumptions and harms/burdens
→ decision-changing evidence
→ capability / invariant / bounded variation
→ functional + quality requirements
→ architecture only where earned
→ implementation
→ verification
→ observed customer/business outcome
→ supersede/refine decisions when evidence changes
```

Across all batches, recurring false-authority traps include:

```text
repository statement ≠ customer validation
customer request ≠ accepted requirement
stakeholder report ≠ observed operator behavior
prototype success ≠ product demand
metric movement ≠ outcome by definition
provider state ≠ business authority outside its contract
configuration ≠ automatically good flexibility
glossary ≠ universal ontology
semantic context ≠ service boundary
source/framework recommendation ≠ SquiFlow requirement
implemented capability ≠ verified customer outcome
AI-generated analysis ≠ evidence
statistical threshold ≠ automatic business truth
```

The largest remaining uncertainty after the source review remains upstream/customer-facing: exact initial customer ecosystem, first coherent end-to-end product outcome, and observed current work.

Further methodology/source accumulation should therefore have a higher evidence threshold than direct first-customer discovery.

## 9. Source URLs

- 157 `https://www.producttalk.org/co-creating/`
- 158 `https://www.sei.cmu.edu/library/relating-business-goals-to-architecturally-significant-requirements-for-software-systems/`
- 159 `https://www.sei.cmu.edu/library/architecting-in-a-complex-world-eliciting-and-specifying-quality-attribute-requirements/`
- 160 `https://martinfowler.com/bliki/ArchitectureDecisionRecord.html`
- 161 `https://www.producttalk.org/five-types-of-assumptions/`
- 162 `https://www.producttalk.org/assumption-testing/`
- 163 `https://www.producttalk.org/experiment-design/`
- 164 `https://www.producttalk.org/why-you-arent-learning-as-much-as-you-could-from-your-experiments/`
- 165 `https://www.producttalk.org/leanstartupconf2015/`
- 166 `https://www.producttalk.org/the-dos-and-donts-of-hypothesis-testing/`
- 167 `https://www.producttalk.org/enterprise-experiments/`
- 168 `https://www.producttalk.org/run-experiments-before-you-write-code/`
- 169 `https://www.producttalk.org/dont-rely-on-confidence-alone/`
- 170 `https://www.producttalk.org/how-to-estimate-the-expected-impact-of-a-product-change/`
- 171 `https://www.producttalk.org/what-to-do-when-you-dont-have-enough-traffic-to-ab-test/`
- 172 `https://www.producttalk.org/the-5-components-of-a-good-hypothesis/`
- 173 `https://www.producttalk.org/putting-the-4-levels-of-product-analysis-into-practice-a-halloween-themed-example/`
- 174 `https://www.producttalk.org/hypothesis-testing-mistake-1-not-knowing-what-you-want-to-learn/`
- 175 `https://www.producttalk.org/the-14-most-common-hypothesis-testing-mistakes-product-teams-make-and-how-to-avoid-them/`
- 176 `https://www.producttalk.org/just-enough-statistics-to-get-the-product-job-done/`
- 177 `https://www.producttalk.org/know-who-you-are-building-for-and-why/`
- 178 `https://www.producttalk.org/if-you-dont-get-this-one-thing-right-all-your-product-research-and-experiments-wont-matter/`
- 179 `https://www.producttalk.org/why-testing-everything-doesnt-work/`
- 180 `https://www.martinfowler.com/bliki/BoundedContext.html`
- 181 `https://martinfowler.com/articles/patterns-legacy-displacement/create-town-plan.html`
- 182 `https://www.producttalk.org/discovering-solutions/`
- 183 `https://openpracticelibrary.com/practice/event-storming/`

## 10. Collection completion

```text
Batch 1  001–026  complete
Batch 2  027–052  complete
Batch 3  053–078  complete
Batch 4  079–104  complete
Batch 5  105–130  complete
Batch 6  131–156  complete
Batch 7  157–183  complete

TOTAL: 183 / 183 unique sources reviewed
```

This completion means every URL in the curated collection has a disposition. It does **not** mean every source claim is accepted, every product question is closed, or customer validation is complete.