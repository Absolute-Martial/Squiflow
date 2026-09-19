# Business Terms and Semantic Glossary

**Version:** v0.1.0

**Status:** Focused owner for consequential SquiFlow business/domain terminology.  
**Authority boundary:** This document owns the meaning, distinction, ambiguity, evidence status, and relationship of consequential business terms. It does **not** duplicate detailed workflow, pricing, payment, inventory, authorization, sync, or other behavioral rules owned by their focused documents.

## 1. Why this document exists

The same word can mean different things to an Owner, Staff member, customer, supplier, accountant, designer, developer, or external system. Different words can also describe the same underlying business concept.

Semantic ambiguity becomes expensive when it hardens into:

- persistence/schema names;
- public or sync APIs;
- permission identifiers;
- workflow states;
- reporting dimensions;
- legal/human document labels;
- migration rules;
- customer-facing language;
- long-lived identifiers or historical interpretation.

SquiFlow therefore resolves consequential language deliberately rather than treating repository wording, one customer's vocabulary, or an external framework taxonomy as automatically universal.

## 2. What belongs here

Create or refine a glossary entry when ambiguity could materially change product scope, domain identity, lifecycle, authority, workflow, permissions, reporting, persistence, APIs, migration, historical meaning, or user understanding.

Do **not** add every noun used in the repository. Ordinary implementation terms whose meaning is already local and unambiguous do not need glossary ceremony.

A useful entry contains only what is needed:

```text
Term
SquiFlow meaning
Context / actor
Examples
Counterexamples
Identity
Lifecycle / important states
Historical meaning
Related terms
Customer aliases
Disputed interpretations
Evidence provenance
Status
Focused owner / reference
```

The focused owner remains authoritative for detailed behavior. For example, this glossary may distinguish `Payment` from `Settlement`, while payment state/retry/reconciliation behavior belongs to the payment/API/domain owner documents.

## 3. Evidence and status rule

A term appearing in an accepted repository document establishes **documented current SquiFlow meaning**, not customer validation that every target business uses the same word or conceptual boundary.

For semantic evidence distinguish where material:

- **REPOSITORY** — current documented SquiFlow usage;
- **REPORTED** — vocabulary/meaning described by a participant;
- **OBSERVED** — language or distinction visible in real work/artifacts;
- **INFERENCE** — a proposed normalization or conceptual relationship;
- **UNKNOWN / DISPUTED** — evidence is insufficient or materially conflicting.

Do not generalize one customer's terminology into a universal SquiFlow concept merely because it is convenient for schema or UI naming.

## 4. Initial accepted/documented terms

### Party

**Current SquiFlow meaning:** a person or organization participating in a business relationship or transaction context. Current documented examples include an individual customer, organization, supplier, representative, or another actor.

**Counterexample:** a fixed synonym for `Customer`. A supplier or representative can also be a Party.

**Evidence/status:** REPOSITORY / documented current model. Customer-language validation remains context dependent.

**Focused owner:** `docs/domain/BUSINESS_MODEL.md`.

### Business Context

**Current SquiFlow meaning:** contextual scope relevant to business work, which may include tenant, branch/location, program/project, and Workstation where applicable.

**Counterexample:** a mandatory hierarchy through which every transaction must pass.

**Evidence/status:** REPOSITORY / documented current model.

**Focused owner:** `docs/domain/BUSINESS_MODEL.md`.

### Owner and Staff

**Current SquiFlow meaning:** default small-team authorization/operating templates.

**Counterexample:** validated behavioral personas, fixed job titles, or proof that all businesses divide work into only two human roles.

**Evidence/status:** REPOSITORY / accepted authorization direction.

**Focused owners:** `docs/security/TENANT_PERMISSIONS.md` and `docs/product/PRODUCT_FOUNDATION.md`.

### Internal ID

**Current SquiFlow meaning:** stable application identity for a business record.

**Counterexample:** a human/legal invoice, receipt, quotation, or other document number.

**Evidence/status:** REPOSITORY / accepted cross-cutting semantic rule.

**Focused owner:** `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md`.

### Issued / Posted business fact

**Current SquiFlow meaning:** a fact that has acquired financial, stock, legal, or historical business meaning such that later correction/reversal/revision is used instead of silent destructive overwrite.

**Counterexample:** every draft form value.

**Evidence/status:** REPOSITORY / accepted cross-cutting invariant.

**Focused owners:** `docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md` and the applicable aggregate/domain owner.

## 5. Terms that require discovery before stronger normalization

The following distinctions are consequential enough that SquiFlow should not silently collapse or separate them without evidence from actual work and the relevant domain owner:

```text
Order
Request
Job
Work
Transaction
Sale

Customer
Party
Account
Commercial Relationship

Fulfilled
Completed
Ready
Delivered / Picked up
Settled

Quotation
Estimate
Tender
Offer

Supplier
Outsourced producer / printer
Vendor
```

These are **discovery prompts**, not evidence that every term must become a separate entity/state/type.

For each disputed group ask:

1. Does the business treat the concepts differently in real cases?
2. Does the distinction change identity, authority, lifecycle, pricing, accounting, workflow, reporting, correction, or historical meaning?
3. Is the difference only customer wording that can map to one stable concept?
4. Would collapsing them lose information or make a real invariant impossible to express?
5. Would separating them create artificial taxonomy/configuration burden?

## 6. UI wording versus domain identity

A customer-facing label may vary without changing the underlying domain concept when that variation is safe and evidence-backed.

Conversely, two identical-looking labels do not imply the underlying concepts are the same.

Use this rule:

```text
vocabulary difference
≠ automatically a domain difference

domain difference
≠ automatically a TenantType
```

A stronger concept boundary is justified only when real identity, lifecycle, authority, invariant, historical meaning, reporting, or operational responsibility differs.

## 7. Context-specific canonical meaning

SquiFlow does not require every consequential word to have one global meaning across every business context.

Where evidence shows that the same term legitimately refers to different concepts in different contexts, each context may own a precise local meaning. The boundary must be justified by real semantic differences such as identity, lifecycle, authority, invariants, historical meaning, reporting, or operational responsibility—not merely by organization charts, screen grouping, or architectural fashion.

When context-specific meanings are accepted, record enough mapping to prevent accidental collapse:

```text
term
→ context A meaning / owner
→ context B meaning / owner
→ shared identity or translation, if any
→ information that may cross the boundary
→ information that must not be assumed equivalent
```

Use this rule:

```text
same word
≠ necessarily one global meaning

legitimate semantic context
≠ automatically a separate service
≠ automatically a separate process
≠ automatically a separate database
≠ automatically a separate deployment
≠ automatically a TenantType
```

A semantic boundary can exist inside the current modular monolith. Runtime extraction requires its own independently justified deployment, failure, scaling, security, ownership, or operational reason.

Likewise, different customer-facing words may still map safely to one canonical concept when their underlying identity and rules are the same.

## 8. Historical semantics

Renaming a term in the UI or documentation must not silently reinterpret historical data.

When a concept's meaning changes materially after data has been created, decide explicitly whether the change is:

- wording only;
- a compatible clarification;
- a new version/meaning requiring migration;
- a genuinely separate concept.

Long-lived persisted/API/workflow meanings require more evidence before change than reversible screen wording.

## 9. Relationship to discovery

`docs/product/PRODUCT_FOUNDATION.md` owns the product-evidence process. `docs/product/DISCOVERY_COMPLEXITY_AND_RESEARCH_EVIDENCE.md` owns detailed discovery/investigation discipline. This glossary records semantic conclusions and unresolved ambiguity that emerge from that process.

When observing real work, capture the participant's own language before normalizing it into SquiFlow terms. Record aliases and counterexamples where they expose meaning, but do not preserve every customer-specific phrase as a first-class product concept.

Collaborative modeling techniques such as Event Storming may help surface language, events, boundaries, and disagreements. Their output remains a discovery artifact/hypothesis until checked against the relevant evidence and owner decisions; an aggregate or workshop grouping does not automatically become a process/service/database boundary.

## 10. Anti-taxonomy rule

This document must not grow into a generic ERP ontology or an external framework vocabulary dump.

A term is added because SquiFlow has a consequential semantic decision to make, not because another product, article, accounting package, or design pattern uses the word.
