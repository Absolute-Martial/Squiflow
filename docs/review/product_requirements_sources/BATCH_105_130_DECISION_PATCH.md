# Product & Requirements Foundation — Batch 5 Decision Patch

**Coverage:** sources `105–130`  
**Source families:** Thoughtworks `105–109`; Nielsen Norman Group `110–130`  
**Repository baseline reviewed:** `main @ ef32a66d5e2a8cc4df72628125f7b27ccf776beb`  
**Status:** Decision/review record. Current semantics remain owned by the focused owner documents changed by this patch.

## 1. Batch conclusion

Batch 5 does not justify adopting a UX department/process, Scrum ceremony model, separate UX backlog, story-point standard, Agile quality maturity model, A/B testing platform, design system, or UX-debt registry.

The useful conclusions are narrower:

1. consequential business language needs an explicit semantic owner before ambiguous terms harden into persistence, APIs, workflow, permissions, reporting, or historical meaning;
2. stakeholder/Owner reports and direct evidence from the actor performing the work must remain distinguishable;
3. adoption/setup/learning/recovery can be part of reaching the first useful routine outcome, without implying a generic onboarding platform;
4. prototypes are evidence instruments whose fidelity should be proportional to the uncertainty being tested;
5. a formal cross-platform design system is deferred until repetition/inconsistency creates enough cost to justify it.

## 2. Source-to-decision traceability

| ID | Source implication | SquiFlow disposition |
|---|---|---|
| 105 | Deployment alone does not ensure people can adopt/use the product effectively. | APPLY principle to adoption-to-routine-use discovery; reject enterprise change-management machinery. |
| 106 | Forecasts depend on simplifying assumptions and should expose uncertainty. | APPLY existing uncertainty discipline; no forecasting framework. |
| 107 | Similar business words can represent materially different concepts. | APPLY: create focused `docs/domain/BUSINESS_TERMS.md`. |
| 108 | A/B results require statistical/uncertainty interpretation. | DEFER experimentation infrastructure; do not adopt a universal p-value rule. |
| 109 | Agile Quality Management maturity/scoring model. | REJECT framework; requirement-specific NFR/verification gates remain stronger. |
| 110 | Design specifications can communicate implementation-significant UX behavior. | APPLY selectively; no final-design handoff model. |
| 111 | Research does not naturally fit feature-sized sprint items. | APPLY responsibility/evidence visibility; no UX-process bureaucracy. |
| 112 | Too little documentation causes relearning; too much causes maintenance burden. | ALREADY ADDRESSED by documentation truth hierarchy. |
| 113 | Enterprise intranet winners emphasize user/stakeholder involvement, governance, IA, launch/support and ROI. | BACKGROUND ONLY / limited-access source; no SMB product requirement derived. |
| 114 | Questions and assumptions should remain visible until research/evidence resolves them. | ALREADY ADDRESSED by evidence/status taxonomy; retain context-bound evidence semantics. |
| 115 | UX involvement in delivery ceremonies can preserve context. | BACKGROUND; responsibility retained, Scrum mapping not adopted. |
| 116 | No single UX backlog structure fits every team. | APPLY principle; no separate UX backlog by default. |
| 117 | Repeated experience compromises can accumulate cost. | APPLY only by recording consequential accepted compromises with revisit triggers. |
| 118 | Collaborative sketching can cheaply expose alternatives. | OPTIONAL discovery technique. |
| 119 | Overcommitting UX capacity degrades work. | LOW applicability; existing WIP/capacity honesty sufficient. |
| 120 | UX/Agile integration depends on collaboration/process flexibility. | BACKGROUND; no organization prescription. |
| 121 | UX work can be made visible through stories/subtasks/criteria. | APPLY visibility principle; reject mandatory story format. |
| 122 | Successful UX/Agile integration varies widely. | BACKGROUND support for avoiding one fixed process. |
| 123 | Prototype is a hypothesis; fidelity follows learning goal. | APPLY proportional-prototyping rule. |
| 124 | Style guides/design systems can improve consistency/reuse when repeated patterns exist. | DEFER WITH TRIGGER; require semantic consistency now, shared design-system infrastructure later only if earned. |
| 125 | Practitioner reports emphasize research, collaboration, iteration and adaptation. | APPLY selectively; note sampling bias. |
| 126 | Stakeholder/business input does not substitute for evidence from actual users doing the work. | APPLY evidence-source/actor distinction. |
| 127 | Small qualitative study shows cadence can improve transparency but also squeeze research. | BACKGROUND / caveat; no process import. |
| 128 | Design charrettes can generate alternatives cheaply. | OPTIONAL technique. |
| 129 | Older guidance recommends design-ahead and stronger UI coordination. | APPLY coherence goal; reject fixed sprint-zero/design-ahead process. |
| 130 | Requirements can reflect representatives' assumptions rather than users' real work. | APPLY strongly to actual-work observation and evidence provenance. |

## 3. Accepted changes

### B5-P1 — Business/domain language owner

Create `docs/domain/BUSINESS_TERMS.md` as a lightweight semantic owner.

It owns:
- term meaning and scope;
- examples/counterexamples;
- relationships/aliases;
- disputed interpretations;
- evidence/status;
- semantic/historical change implications.

It does **not** duplicate detailed pricing, workflow, payment, inventory, security, or sync behavior.

### B5-P2 — Adoption-to-routine-outcome refinement

Product discovery must consider the steps that may stand between product availability and a useful routine outcome, including where relevant setup, import/data entry, initial configuration, learning/training, first real use, first-error recovery, and repeated routine use.

This does not create an onboarding subsystem by default; assisted/manual setup remains a valid first response.

### B5-P3 — Evidence actor/source distinction

For consequential discovery evidence, record both:
- who supplied the evidence; and
- whose work/outcome the evidence describes.

An Owner reporting Staff friction is useful REPORTED evidence, but it is not the same as Staff reporting it or direct observation/artifacts of Staff encountering it.

### B5-P4 — Proportional prototyping

Treat a prototype as an evidence instrument. Use the lowest fidelity capable of distinguishing the current uncertainty. A working implementation may itself be the cheapest credible experiment when the uncertainty is technical or lower-fidelity artifacts cannot answer it.

## 4. Deferred change

### B5-P5 — Formal design system/style guide

Do not create a cross-platform design system now.

Revisit when evidence shows one or more of:
- repeated component/pattern reimplementation;
- semantic/state divergence between Web and Workstation;
- UI inconsistency causing user error;
- repeated implementation cost;
- accessibility/visual-quality work needing reusable standards;
- multiple contributors making ad-hoc patterns difficult to govern.

Until then, preserve shared business semantics and compatible state/error language while allowing platform-native component implementation.

## 5. Constructive adversarial review

### Risk: glossary becomes duplicate domain documentation

Mitigation: it owns semantic meaning/distinction/evidence only and links to focused behavioral owners.

### Risk: direct observation becomes dogma

Mitigation: evidence cost and consequence still matter. Reported stakeholder evidence remains useful when labelled correctly; direct observation is prioritized when the distinction could materially change the product decision.

### Risk: adoption thinking creates premature onboarding scope

Mitigation: discovery of an adoption blocker does not imply automation. Assisted/manual setup, stronger defaults, examples, or no product change can be valid responses.

### Risk: prototyping becomes a mandatory gate

Mitigation: no prototype is required when uncertainty is already sufficiently closed or implementation is the cheapest reversible experiment.

### Risk: deferring a design system creates inconsistent UX

Mitigation: semantic/state consistency is required now; shared visual/component infrastructure is deferred, not consistency itself.

## 6. Source URLs

- 105 `https://www.thoughtworks.com/en-br/insights/blog/change-management-agile-world-getting-ready-war`
- 106 `https://www.thoughtworks.com/en-br/insights/blog/forecast-charts-when-will-we-be-done-what-can-we-get-done`
- 107 `https://www.thoughtworks.com/en-br/insights/blog/ba-practice-business-terms`
- 108 `https://www.thoughtworks.com/en-br/insights/blog/dont-be-misled-your-ab-testing-part-1`
- 109 `https://www.thoughtworks.com/en-br/insights/blog/agile-quality-management-model`
- 110 `https://www.nngroup.com/articles/creating-design-specs-for-development/`
- 111 `https://www.nngroup.com/articles/user-research-agile/`
- 112 `https://www.nngroup.com/articles/lean-agile-documentation/`
- 113 `https://www.nngroup.com/articles/intranet-design/2021/`
- 114 `https://www.nngroup.com/articles/tracking-questions-assumptions-facts-agile/`
- 115 `https://www.nngroup.com/articles/ux-scrum/`
- 116 `https://www.nngroup.com/articles/ux-agile-backlog/`
- 117 `https://www.nngroup.com/articles/ux-debt/`
- 118 `https://www.nngroup.com/articles/collaborative-agile-activities/`
- 119 `https://www.nngroup.com/articles/tracking-ux-capacity/`
- 120 `https://www.nngroup.com/articles/agile-not-easy-ux/`
- 121 `https://www.nngroup.com/articles/ux-user-stories/`
- 122 `https://www.nngroup.com/articles/state-ux-agile-development/`
- 123 `https://www.nngroup.com/articles/ux-prototype-hi-lo-fidelity/`
- 124 `https://www.nngroup.com/articles/front-end-style-guides/`
- 125 `https://www.nngroup.com/articles/ux-success-agile/`
- 126 `https://www.nngroup.com/articles/ux-without-user-research/`
- 127 `https://www.nngroup.com/articles/doing-ux-agile-world/`
- 128 `https://www.nngroup.com/articles/design-charrettes/`
- 129 `https://www.nngroup.com/articles/agile-user-experience-projects/`
- 130 `https://www.nngroup.com/articles/agile-development-and-usability/`
