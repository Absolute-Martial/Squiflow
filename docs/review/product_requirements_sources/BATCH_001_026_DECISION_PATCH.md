# Product & Requirements Foundation — Batch 1 Decision Patch

**Source IDs:** 001–026  
**Source family:** SVPG  
**Repository baseline reviewed:** `main` at `b35c582662917c9498c5fff8326ae586566fb024`  
**Status:** Decision/application record for the approved Batch 1 documentation patch. This review record is evidence and rationale; it does not override owner documents.

## 1. Why this patch exists

Batch 1 did not justify a new SquiFlow feature or architecture subsystem. Its strongest applicable findings concerned the upstream product foundation and the risk of mistaking mature repository descriptions for validated customer evidence.

The applied documentation changes therefore remain deliberately narrow:

1. clarify that current domain diagrams/examples are conceptual models rather than mandatory universal process sequences;
2. establish a focused owner for target-customer/problem/outcome/product-promise evidence;
3. distinguish repository acceptance, customer/domain evidence, hypotheses, implementation, and verified outcomes;
4. make explicit that technical implementation phases do not automatically constitute customer-facing product releases.

## 2. Applied owner-document changes

### BP1 — conceptual model is not prescribed process

**Owner changed:** `docs/domain/BUSINESS_MODEL.md`  
**Disposition:** APPLY after approval.

The `Party → Commercial Relationship / Account → Business Context → Transaction → Workflow → Settlement` chain is now explicitly identified as a conceptual relationship model rather than a mandatory journey sequence.

The illustrative walk-in small-shop flow is likewise identified as an example used for scope reasoning, not evidence that every target business follows the sequence or that every step belongs inside SquiFlow.

### BP2 — upstream product foundation owner

**Owner created:** `docs/product/PRODUCT_FOUNDATION.md`  
**Disposition:** APPLY after approval.

The new owner is intentionally upstream of the detailed domain/requirements/architecture documents. It owns the current statement of target-customer evidence, product outcome/promise, high-impact product questions, and the distinction between current-model and problem-first discovery.

It does not silently reopen or override accepted material product/domain/architecture decisions.

### BP3 — product evidence/status semantics

**Owner:** `docs/product/PRODUCT_FOUNDATION.md`  
**Disposition:** APPLY after approval.

The document separates evidence provenance from product-understanding status and prevents the following collapses:

```text
repository statement ≠ observed customer behavior
source recommendation ≠ SquiFlow requirement
hypothesized need ≠ supported need
accepted product responsibility ≠ implemented capability
implemented capability ≠ verified customer outcome
```

### BP4 — technical phase versus product promise

**Owner:** `docs/product/PRODUCT_FOUNDATION.md`  
**Related implementation owners:** `MASTER_IMPLEMENTATION_PLAN.md`, `docs/implementation/PHASES_AND_GATES.md`  
**Disposition:** APPLY after approval.

The implementation owners continue to define technical order and gates. The product foundation now explicitly states that completion of a technical phase does not, by itself, mean SquiFlow has a coherent sellable/customer-complete release.

No technical phase ordering was changed by this patch.

## 3. Source findings that materially support the patch

The following supplied sources were especially relevant. They are methodology/practitioner evidence, not customer validation for SquiFlow.

| ID | Source | Applied implication |
|---|---|---|
| 001 | https://www.svpg.com/business-strategy-vs-product-strategy/ | Keep business objective/product problem distinct from implementation tactics. |
| 002 | https://www.svpg.com/your-business-plan-is-wrong/ | Preserve uncertainty in early product assumptions rather than treating plans as facts. |
| 004 | https://www.svpg.com/seven-deadly-sins-of-product-planning/ | Permit narrowing/stopping weak bets and consider opportunity cost. |
| 007 | https://www.svpg.com/prototype-testing/ | Investigate how people currently solve the problem before showing a proposed solution. |
| 011 | https://www.svpg.com/product-strategy-actions/ | Start with problems/outcomes rather than predetermined features. |
| 012 | https://www.svpg.com/product-strategy-insights/ | Allow generative discovery to expose needs outside the current component list. |
| 013 | https://www.svpg.com/product-strategy-overview/ | Do not confuse a large initiative/capability inventory with product strategy. |
| 014 | https://www.svpg.com/product-strategy-focus/ | Keep the active question/problem set small and consequential. |
| 015 | https://www.svpg.com/discovery-vs-design/ | Maintain a problem-first discovery pass before mapping findings to the existing design. |
| 016 | https://www.svpg.com/design-in-enterprise-software-companies/ | Distinguish buyer, user, administrator, and other affected interests where evidence warrants it. |
| 020 | https://www.svpg.com/personas-for-product-management/ | Do not turn Owner/Staff authorization templates into invented behavioral personas. |
| 021 | https://www.svpg.com/great-products-by-design/ | Judge customer-facing completeness by a coherent successful outcome, not feature count. |
| 025 | https://www.svpg.com/process-vs-model/ | Do not interpret conceptual models as prescribed detailed process. |
| 026 | https://www.svpg.com/failing-fast-vs-learning-fast/ | Separate reversible learning experiments from preventable harm to protected responsibilities. |

## 4. Explicitly not adopted from Batch 1

This patch does **not** add:

- a Product Council or PMO;
- a mandatory stage-gate product process;
- a fixed design-before-development phase;
- a final-PRD handoff model;
- an OKR framework;
- invented personas or organization roles;
- arbitrary prototype sample-size/value-score thresholds;
- any new SquiFlow runtime component, service, database, API, workflow engine, or architecture boundary.

Older source material in the batch sometimes uses final-spec, handoff, or stage-gate language. The durable learning/risk-reduction principle was retained where useful; those older process structures were not imported as requirements.

## 5. Current unresolved product questions

The patch records only the highest-impact questions in `docs/product/PRODUCT_FOUNDATION.md`:

- exact initial customer ecosystem;
- first end-to-end product outcome/promise;
- evidence of how selected work is actually performed today;
- adoption-to-first-useful-result path;
- evidence threshold for continuing, narrowing, changing, deferring, or stopping a product responsibility.

These are intentionally unresolved. The Batch 1 sources help establish why the questions matter; they do not answer them for SquiFlow.

## 6. Verification

Checked after application:

- existing practical domain decisions remain present in `docs/domain/BUSINESS_MODEL.md`;
- no architecture/runtime decision was changed;
- no technical phase ordering was changed;
- the new product owner explicitly defers detailed domain/security/workflow/NFR/architecture/implementation authority to the existing owner documents;
- Owner/Staff remain current authorization/default-operating templates, not newly defined personas;
- unknown product evidence is kept unknown rather than filled with an invented first-customer story.

Future source batches may challenge these conclusions. If later evidence changes a material accepted product/domain/architecture decision, that change still requires explicit approval and an update to the appropriate owner document.
