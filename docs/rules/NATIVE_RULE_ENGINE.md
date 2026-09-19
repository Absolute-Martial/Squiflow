# SquiFlow Native Rule Engine

**Version:** v0.1.0

## 1. Ownership

SquiFlow owns the tenant-safe rule architecture:
- rule representation;
- typed fact schema;
- validation;
- complexity limits;
- scope/inheritance;
- versioning/publication;
- immutable snapshots;
- deterministic evaluation contract;
- decision trace;
- simulation/tests;
- Workstation/server compatibility.

External evaluators can be bounded adapters for a specialized rule family, but they do not define tenant rule syntax/security/authority.

## 2. No arbitrary tenant code

Do not accept uploaded tenant C#, scripts or unrestricted expressions.

Use bounded structured rule definitions/decision tables over an allow-listed fact schema.

Example:

```text
WHEN Customer.Type = Organization
AND Order.Total > configured threshold
AND Payment.Term = Credit
THEN RequireApproval = Manager
```

## 3. Scope

Effective rules can be resolved through an allowed hierarchy such as:

```text
Platform
→ Tenant
→ Organization
→ Branch
→ Program
→ Workstation
```

More specific configuration can override only values/actions that are explicitly overridable.

Security/tenant/domain invariants are not overridden through tenant rules.

## 4. Publication lifecycle

```text
Draft
→ Validate
→ Simulate/Test
→ Review/Approval if required
→ Publish immutable version
→ distribute effective snapshot
→ Retire/Roll back by activating another version
```

If validation/compilation fails, the previous active version remains active.

## 5. Runtime

Rule evaluation for normal business operations is in-memory over an immutable RuleSet snapshot.

Do not query the DB for rule definitions on every transaction.

The evaluator performs no external I/O or business mutation. It returns a typed result/action plan. Application/domain code performs allowed effects.

## 6. Web-only authoring

Tenant rule creation/editing/testing/publication is performed through Web administration.

The Workstation only receives compatible effective snapshots relevant to its tenant/context and may evaluate permitted rules locally for offline UX.

## 7. Decision trace

For material decisions record enough bounded evidence to explain:
- DecisionId;
- RuleSet version/hash;
- context/fact categories used;
- matched rules;
- result/action plan;
- timing;
- correlation/operation ID.

Do not log sensitive customer content by default.

## 8. Failure semantics

- invalid definition → publication rejected;
- unsupported schema/operator → explicit incompatibility;
- complexity budget exceeded → rule evaluation failure, no partial side effects;
- missing fact → defined Unknown/validation behavior, not accidental null coercion;
- conflicting outputs → explicit conflict/hit-policy result;
- engine upgrade parity mismatch → deployment/activation gate fails.

## 9. Why not a separate Rule service

Rule evaluation is an application capability, not automatically a network service.

Run it in-process in API/Worker/Workstation where appropriate. Extract a separate process/service only if measured CPU, fault isolation, security or independent deployment requirements justify it.

## 10. Fact authority and local/offline evaluation

A published RuleSet being available on the Workstation does **not** mean every rule can be decided authoritatively offline.

Every fact family used by a rule must declare its authority/freshness class for the decision being made, conceptually:

```text
LocalSafe
LocalProvisional
ServerRequired
```

Examples:

- locally entered order fields can be `LocalSafe` for draft validation;
- a cached organization credit exposure can be `LocalProvisional` for offline guidance;
- current shared inventory/credit/provider/security state can be `ServerRequired` for an authoritative effect.

A RuleSet snapshot declares the fact schema/version and the application layer knows which result can be applied locally versus only shown provisionally.

The Workstation must not turn stale centrally owned facts into authority merely because the rule evaluator returned a deterministic result.

Server-authoritative commands re-evaluate or otherwise validate the applicable current rule/fact state before committing shared financial/stock/security effects.

If a required fact is unavailable offline, return an explicit result such as `RequiresServerFact`/defined Unknown category rather than coercing a stale/missing value into a match.

This rule-fact authority classification must align with the Workstation command authority classes in `docs/workstation/LOCAL_FIRST_DESKTOP.md` and with workflow transition eligibility.