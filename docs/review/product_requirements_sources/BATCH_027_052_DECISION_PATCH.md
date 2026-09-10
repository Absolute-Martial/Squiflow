# Product & Requirements Foundation — Batch 2 Decision Patch

**Source IDs:** 027–052  
**Source family:** SVPG  
**Repository baseline reviewed:** `main` at `4e8ad580c8d0bb7e026c080935c679aca63054d9` after Batch 1 MR !30 merged  
**Status:** Decision/application record for the approved Batch 2 documentation patch. This review record is evidence and rationale; it does not override owner documents.

## 1. Batch 2 result

Batch 2 was dominated by product-operating-model, transformation, team, process, and organizational material. The review did **not** find evidence that SquiFlow should adopt that organizational model or add a new product capability/runtime boundary.

The applicable findings were narrower product-shaping rules:

1. tenant-facing configuration should be evaluated for the behavior and burden it creates, not only for expressive power;
2. the first real customer should be treated as a bounded learning context rather than a template for universal product taxonomy;
3. material product responses should be challenged across value, usability, feasibility, and business viability;
4. implementation/release must be followed by evidence of actual customer/business outcome where the product promise is material;
5. problem-first discovery should still bring technical feasibility into solution exploration early, without letting the existing architecture define the problem.

## 2. Applied owner-document changes

### B2-P1 — configuration as behavioral design

**Owner changed:** `docs/product/PRODUCT_FOUNDATION.md`  
**Disposition:** APPLY after approval.

The product foundation now states that `configurable` is not equivalent to `good variation design`.

Tenant-facing workflow, rules, forms, permissions, pricing/policy, feature visibility, and similar mechanisms must be evaluated for:

- the behavior their UI encourages;
- strength of defaults;
- Owner setup and cognitive burden;
- complexity transferred from Owner to Staff;
- explainability before publication;
- support/correction/migration burden;
- whether repeated configuration demand signals a missing reusable capability;
- whether configurability is being used to avoid resolving a semantic/product distinction.

The target remains strong common semantics + strong defaults + protected invariants + the smallest evidence-supported bounded variation.

### B2-P2 — first-customer learning boundary

**Owner changed:** `docs/product/PRODUCT_FOUNDATION.md`  
**Disposition:** APPLY after approval.

The first real customer may provide deep evidence, but that context is not automatically generalized into SquiFlow's common model.

The owner now records a bounded learning structure covering product promise, exclusions, learning questions, protected invariants, evidence, customer-specific assumptions, generalization risks, and conditions for wider adoption.

It explicitly prevents the following shortcuts:

```text
first print-oriented customer ≠ TenantType.PrintShop
one customer's requested setting ≠ universal configurability requirement
```

### B2-P3 — product-response risk and outcome ownership

**Owner changed:** `docs/product/PRODUCT_FOUNDATION.md`  
**Disposition:** APPLY after approval.

Material product responses are now challenged across:

- value;
- usability;
- feasibility;
- business viability.

These are responsibilities/questions, not mandatory team roles or an imported Product Operating Model.

The owner also extends the product lifecycle beyond implementation:

```text
implemented capability
→ actual use
→ observed customer/business outcome
→ compare with product promise
→ investigate mismatch
→ continue / improve / narrow / change / defer / stop
```

Technical verification remains necessary but is not treated as proof of customer value.

## 3. Added unresolved product questions

The active high-impact question list is extended only with two questions:

### PFQ-006 — What behavior should SquiFlow's configuration encourage?

This asks what truly needs variation, who bears configuration burden, where strong defaults are enough, and when repeated variation signals a reusable capability rather than another setting.

### PFQ-007 — What makes the first customer suitable as a product-learning customer?

This asks whether SquiFlow can observe real work, connect it to a product promise, constrain risk, obtain outcome evidence, isolate customer-specific exceptions, and define the evidence needed for broader generalization.

## 4. Source findings that materially support the patch

The following supplied URLs were especially relevant. They are practitioner/methodology evidence, not SquiFlow customer validation.

| ID | Source | Applied implication |
|---|---|---|
| 027 | https://www.svpg.com/tools-and-processes/ | Tools/configuration make some behaviors easy and others hard; evaluate encouraged behavior and burden. |
| 030 | https://www.svpg.com/technology-first-vs-needs-first/ | Keep the problem-first pass independent, but bring technical feasibility into discovery early enough to distinguish real options. |
| 034 | https://www.svpg.com/the-product-model-and-agile/ | Working software is delivery evidence, not proof that the intended business outcome occurred. |
| 035 | https://www.svpg.com/the-politics-of-pilot-teams/ | Use bounded first-customer learning with explicit outcomes/risks; do not assume the pilot represents the whole market. |
| 040 | https://www.svpg.com/product-model-concepts/ | Challenge material responses across value, usability, feasibility, and viability. |
| 041 | https://www.svpg.com/from-projects-to-products/ | Responsibility continues after release through outcome evidence and learning. |
| 042 | https://www.svpg.com/who-is-product-operating-model-for/ | Study physical/manual/digital work end to end rather than treating the software boundary as the journey. |
| 044 | https://www.svpg.com/the-product-operating-model/ | Terms can be ambiguous; define SquiFlow meanings rather than importing labels. |
| 046 | https://www.svpg.com/changing-how-you-solve-problems/ | Treat requested features as candidate solutions to underlying problems. |
| 047 | https://www.svpg.com/changing-how-you-decide-which-problems-to-solve/ | A real problem still requires prioritization; not every discovered need belongs in current scope. |
| 049 | https://www.svpg.com/changing-how-you-build/ | Protect customers through reliable change; configuration/product change can impose adoption burden. |
| 050 | https://www.svpg.com/transformation-in-action/ | Connectivity and solution shape are context-dependent; do not generalize one case into universal offline requirements. |
| 052 | https://www.svpg.com/product-vs-feature-teams/ | Preserve responsibility for problem/outcome rather than accepting a feature request as product value. |

## 5. Explicitly not adopted from Batch 2

This patch does **not** add or require:

- a Product Operating Model framework for SquiFlow;
- mandatory Product Manager / Product Designer / Tech Lead staffing;
- Product Ops;
- Product Council or PMO machinery;
- transformation coaches or enterprise change-management structures;
- formal pilot-team organization structure;
- OKRs;
- quarterly product-strategy cycles;
- a fixed two-week release cadence;
- A/B experimentation infrastructure;
- a new tenant type, role taxonomy, service, database, API, or runtime component;
- Web offline behavior based on an unrelated case study.

Repeated recommendations across this SVPG cluster are not treated as independent customer evidence.

## 6. Verification

Checked after application:

- `docs/product/PRODUCT_FOUNDATION.md` remains the only authoritative document changed by Batch 2;
- no domain, security, workflow, rules, NFR, architecture, persistence, sync, storage, or implementation-phase owner is changed;
- no code or tests are changed;
- Owner/Staff remain authorization/default-operating templates rather than personas;
- the first-customer rule explicitly prevents automatic generalization into `TenantType` or universal configuration;
- the risk check is expressed as product questions/responsibilities rather than mandated job roles;
- post-release outcome evidence is kept distinct from technical verification;
- all previously unresolved PFQ-001–005 remain unresolved, with PFQ-006 and PFQ-007 added because they can materially alter product shaping.

Future source batches may challenge these conclusions. Material product/domain/architecture changes remain separately approval-gated and must update the appropriate owner document.
