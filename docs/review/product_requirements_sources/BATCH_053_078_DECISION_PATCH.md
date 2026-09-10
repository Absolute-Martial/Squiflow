# Product & Requirements Foundation — Batch 3 Decision Patch

**Source IDs:** 053–078  
**Source family:** Thoughtworks  
**Repository baseline reviewed:** `main` at `05b9bcc1b9f0ecb1364dd4d278bd407813b2463d` after Batch 2 MR !31 merged  
**Status:** Decision/application record for the approved Batch 3 documentation patch. This review record is evidence and rationale; it does not override owner documents.

## 1. Batch 3 result

Batch 3 was dominated by architecture, distributed-systems, data, local/on-site computing, authentication infrastructure, team-design and engineering-practice material.

The review did **not** justify adopting the mechanisms described by those articles merely because they were well explained. In particular, it did not justify micro-frontends, service mesh, authentication sidecars, Kubernetes, generic change-data capture, event sourcing/event-driven core architecture, a metrics/data platform, local microservice servers, or generic ports-and-adapters interfaces.

The applicable findings were narrower:

1. quality characteristics such as accuracy, freshness, latency, availability, offline continuity and consistency must be justified by the business outcome and the cost of being wrong;
2. the accepted local-first Workstation architecture still needs product-level evidence for which business operations should be local-capable, provisional or server-required;
3. product-outcome measures need explicit semantics before they influence product decisions;
4. accepted material changes must propagate across every dependent owner whose contract actually changes, rather than leaving repository-wide inconsistency;
5. architecture patterns remain responses to concrete boundaries/problems, not goals by themselves.

## 2. Applied owner changes

### B3-P0 — cross-owner propagation rule

**Owner changed:** `docs/product/PRODUCT_FOUNDATION.md`  
**Disposition:** APPLY after approval.

A material accepted decision now requires a dependency check across product, domain, permissions/security, workflow/rules, NFR, local-first/sync, architecture, implementation, verification and operations as applicable.

The rule is deliberately conditional: update every dependent owner whose meaning changed; do not edit unrelated documents merely for symmetry.

### B3-P1 — business-to-quality traceability

**Owner changed:** `docs/product/PRODUCT_FOUNDATION.md`  
**Bridge created:** `docs/requirements/PRODUCT_TO_QUALITY_TRACEABILITY.md`  
**Disposition:** APPLY after approval.

The product foundation now requires consequential quality characteristics to trace from:

```text
business/product outcome
→ failure consequence
→ quality characteristic
→ priority/trade-off
→ measurable evidence
→ detailed requirement owner
→ architecture consequence if any
→ verification evidence
```

The new bridge does not replace `NON_FUNCTIONAL_REQUIREMENTS.md`; it supplies the upstream business reason and trade-off context that the existing NFR model intentionally does not invent.

### B3-P2 — offline/local-first product justification

**Owner changed:** `docs/product/PRODUCT_FOUNDATION.md`  
**Bridge:** `docs/requirements/PRODUCT_TO_QUALITY_TRACEABILITY.md`  
**Disposition:** APPLY after approval.

Added `PFQ-008`:

> Which work must remain usable when connectivity or central dependencies are unavailable?

The question explicitly asks for actor, business consequence, tolerated interruption, required accuracy/freshness/current authority and the resulting Local-capable / Local-provisional / Server-required classification.

The accepted Workstation architecture is preserved. This change prevents the desktop's technical capability from silently deciding which business operations belong offline.

### B3-P3 — product-outcome evidence semantics

**Owner changed:** `docs/product/PRODUCT_FOUNDATION.md`  
**Bridge:** `docs/requirements/PRODUCT_TO_QUALITY_TRACEABILITY.md`  
**Disposition:** APPLY after approval.

When a measure materially influences product scope or strategy, record enough meaning to prevent conflicting calculations/interpretations under one label: scope, source, calculation/interpretation, time window, freshness, uncertainty and decision use.

The patch explicitly keeps separate:

```text
product-outcome evidence
≠ operational telemetry
≠ authoritative resource-consumption accounting
```

No metrics warehouse or analytics platform is introduced.

## 3. Cross-owner consistency result

The user explicitly raised the risk that changing only one document could create friction or inconsistency with architecture, domain, permissions and other owners.

The following dependent contracts were therefore inspected before applying Batch 3:

- `docs/security/TENANT_PERMISSIONS.md`;
- `docs/workstation/LOCAL_FIRST_DESKTOP.md`;
- `docs/sync/SYNC_AND_AUTHORITY.md`;
- `docs/requirements/NON_FUNCTIONAL_REQUIREMENTS.md`;
- `docs/testing/VERIFICATION_STRATEGY.md`;
- existing architecture/decision-audit rules.

No semantic edits were necessary in the first five because their existing contracts already match the Batch 3 conclusions:

- permissions snapshots are already non-authoritative and current OpenFGA/server authority is rechecked;
- Workstation operations are already explicitly classified local-capable/provisional/server-required;
- sync already revalidates current identity, TenantContext, permission, domain/rule and concurrency state;
- NFRs already distinguish hard invariants, operational targets, degraded modes and per-invariant freshness/consistency;
- verification already requires observable expected outcome, failure input, authority, recovery and evidence layer.

Duplicating the new product-level rationale into each of those documents would increase drift without changing their contract. `PRODUCT_TO_QUALITY_TRACEABILITY.md` therefore serves as the explicit bridge.

If a future decision actually changes those downstream semantics, this patch's cross-owner propagation rule requires updating the affected owners directly.

## 4. Source findings that materially support the patch

The following supplied sources were especially relevant. They are architecture/practitioner evidence, not SquiFlow customer validation.

| ID | Source | Applied implication |
|---|---|---|
| 057 | https://www.thoughtworks.com/en-br/insights/blog/architecture/how-can-on-site-servers-enable-richer-retail-experiences-part-one | Local execution is justified by business continuity needs, not by architecture preference. |
| 058 | https://www.thoughtworks.com/en-br/insights/blog/architecture/how-can-on-site-servers-enable-richer-retail-experiences-part-two | Local execution creates sync/deploy/update/resource obligations that must be earned by the product need. |
| 059 | https://www.thoughtworks.com/en-br/insights/blog/architecture/tackling-the-challenges-of-using-event-driven-architecture-in-a-billing-system | Arrival order, business time, duplicates and reconciliation are separate concerns; asynchronous events do not remove domain authority obligations. |
| 062 | https://www.thoughtworks.com/en-br/insights/blog/data-engineering/metric-driven-data-architectures | Consequential metrics need agreed semantics/source rather than multiple silent definitions. |
| 064 | https://www.thoughtworks.com/en-br/insights/blog/architecture/the-best-architecture-vs-the-useful-one | Quality priorities must follow real business value/trade-offs; near-real-time freshness may be less important than trustworthy accuracy. |
| 066 | https://www.thoughtworks.com/en-br/insights/blog/architecture/architectural-risk-management | Business strategy/quality needs should trace to architecture risk rather than architecture being selected independently. |
| 073 | https://www.thoughtworks.com/en-br/insights/blog/agile-engineering-practices/from-features-to-outcomes-how-product-teams-can-deliver-real-business-value | Outcome measures should inform real decisions while retaining uncertainty honestly. |
| 075 | https://www.thoughtworks.com/en-br/insights/blog/experience-design/design--together | People/usability, business viability and technology feasibility must interact during solution discovery. |
| 078 | https://www.thoughtworks.com/en-br/insights/blog/digital-innovation/client-assessments-discoveries-part-1-people | Discovery should use real artifacts/participants and distinguish observations from assumed facts. |

## 5. Explicitly not adopted from Batch 3

This patch does **not** add or require:

- micro-frontends or a micro-frontend communication layer;
- service mesh;
- authentication sidecars;
- Kubernetes;
- generic CDC infrastructure;
- event sourcing or an event-driven core architecture;
- a metrics/data platform;
- local per-store microservice servers;
- generic ports-and-adapters/repository abstractions;
- strict TDD/pairing/Agile Fluency/AARM process requirements;
- a new architect role or team-organization model;
- Web offline behavior.

The sources explain contexts where those mechanisms can help. SquiFlow adopts a mechanism only when its own responsibility/failure/topology evidence earns it.

## 6. Verification

Checked after application:

- the local-first Workstation decision is preserved;
- server/current authority for permissions, tenant isolation, stock/credit/payment and other protected facts is preserved;
- no architecture/runtime decision is reopened merely because a source describes an alternative pattern;
- product-outcome measures remain distinct from operational telemetry and durable consumption accounting;
- the traceability bridge does not override the focused NFR/security/sync/verification owners;
- `PFQ-008` remains unresolved until product/customer evidence establishes which first-product work actually needs offline continuity;
- future material changes must update every dependent owner whose contract changes.

Future source batches may challenge these conclusions. Material product/domain/architecture changes remain separately approval-gated and must update the appropriate owner documents and materially dependent owners.