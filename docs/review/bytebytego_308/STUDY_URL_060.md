# URL 060 — OOP Design Patterns and Anti-Patterns: What Works and What Fails

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: comparisons, best-practice lists, pattern catalogs, protocol matrices, popularity claims, maturity ladders, and source diagrams do not select SquiFlow architecture by themselves. The review first asks what SquiFlow is actually doing at the corresponding boundary, what concrete requirement/invariant it solves, why the current mechanism has the needed property, what authority it owns, what it costs, where another mechanism could fit better, and what evidence would justify or falsify a change.

## A. Identification

- **URL occurrence:** `060`
- **PDF page:** `304`
- **Source URL:** `https://blog.bytebytego.com/p/oop-design-patterns-and-anti-patterns`
- **Source access:** paid article with a public preview; no subscription controls bypassed.
- **Related supplied visual:** archive page 139, nine OOP design patterns visual.
- **Visual inspected:** PDF page `304` at full size.

## B. Core concept

### SOURCE

The preview describes design patterns as reusable templates/guidelines for recurring software-design problems and says they can improve modularity, reuse, extensibility and shared engineering vocabulary. It also stresses that patterns can be misapplied and that anti-patterns are recurring approaches that lead to unnecessary complexity, poor maintainability, or inefficient structure. The public preview does not expose the later anti-pattern list.

### INFERENCE

SquiFlow should use pattern names to communicate a design that already has a concrete problem/constraint. A pattern is not evidence that the problem exists, and an anti-pattern label is not enough to reject a design without showing the actual cost.

### EXTERNAL KNOWLEDGE / CAVEAT

The related visual shows Factory, Singleton, Builder, Adapter, Decorator, Proxy, Strategy, Observer and Command. These names describe structural/behavioral shapes, not durability, distribution, authority, or correctness guarantees. In-process Observer does not equal durable pub/sub; Command objects do not require a command bus; Singleton can be harmless for immutable stateless services but risky for mutable global state; Adapter is valuable only around a real incompatible/replacement boundary.

## C. Important concepts

- patterns as vocabulary/templates, not implementations;
- contextual anti-patterns;
- Adapter around provider seams;
- Strategy only for real policy/implementation variation;
- Command semantics versus command-bus infrastructure;
- Observer versus durable messaging;
- Singleton/global mutable-state risk;
- Factory/Builder only when construction complexity exists;
- pattern costs and removal/falsification;

## D. Diagram / visual explanation

The visual groups nine creational, structural and behavioral patterns. SquiFlow already has some shapes that resemble patterns for concrete reasons—for example `IObjectStore` acts as an Adapter-style boundary and explicit sync states resemble a State-style model even though that pattern is not in this visual. The visual does not justify adding nine classes/frameworks to every module.

## E. How it works — step by step

1. Name the recurring design problem first.
2. Explain the simplest direct implementation.
3. Choose a named pattern only if it materially clarifies variation, lifecycle, composition or dependency direction.
4. State the failure/maintenance cost the pattern prevents.
5. Verify it does not hide authority, transactions, retries or distributed failure behind abstractions.
6. Keep the implementation smaller than the problem it solves.
7. Remove or simplify the pattern if the expected variation never appears or indirection becomes the main maintenance cost.

## F. Why it matters

SquiFlow has real provider seams, business commands, explicit state machines and future workflow/rule variation where pattern vocabulary can improve code review. But the small-team constraint makes cargo-cult abstractions particularly expensive.

## G. Trade-offs / limitations

Patterns can standardize communication and reduce recurring design mistakes, but they add indirection, more types, lifecycle complexity and sometimes hidden control flow. Direct code is often better for one simple implementation. Anti-patterns are context-sensitive: the same shape can be sensible at one scale and harmful at another.

## H. Alternatives / comparisons — fit, not winner/loser

```text
direct concrete code
    -> simplest default

Adapter
    -> incompatible/provider replacement seam

Strategy/polymorphism
    -> real selectable behavior

Command object
    -> explicit business intent/value when useful

Observer/event callback
    -> in-process notification

durable outbox/pub-sub
    -> after-commit cross-process delivery

Factory/Builder
    -> non-trivial construction/configuration lifecycle
```

Pattern names never substitute for business semantics.

## I. Real implementation considerations

The implementation decision is not complete until the exact boundary, authority, failure modes, recovery, security/tenant behavior, compatibility, observability, resource cost, small-team operating burden, and adoption/falsification evidence are explicit. The current repository remains documentation/planning-only at the root rather than an application source tree, so architecture statements below are requirements and future proof gates, not claims that code already implements them.

### Implications for the Current Implementation

- **KEEP:** pattern vocabulary only where it explains a real SquiFlow design problem.
- **KEEP:** Adapter-like provider seams for object/backup providers and explicit business-command/state semantics where useful.
- **AVOID:** one pattern framework per diagram, global mutable Singleton state, generic command bus/event bus introduced without a workload, or Observer used as if it were durable messaging.
- **IMPROVE NOW:** when source code exists, code review should ask which concrete problem each indirection/pattern solves and whether a simpler design is now sufficient.
- **LATER / SCALE TRIGGER:** Factory/Builder/Strategy/decorator/proxy layers only when real construction, variation or cross-cutting boundary behavior warrants them.

**What are we actually doing and why?** SquiFlow uses pattern-like structures when they protect a real provider, state, or business-intent boundary. We do not make a pattern decision from a visual comparison; the problem and expected change/failure must be named first.

**What would falsify/change this?** If a pattern adds more types and hidden control flow than the variation it handles, or if the predicted second implementation never appears, it should be simplified. If repeated provider/domain leakage or duplicated policy shows a stable recurring problem, a pattern may become justified.

**Implementation-evidence status:** documented/accepted architecture is not the same as verified implementation. The relevant future slice must prove the behavior in source, tests, deployment, and recovery evidence.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. What is a design pattern according to the source?
2. Why are patterns templates rather than copy-paste implementations?
3. What makes an anti-pattern contextual rather than universally forbidden?

**Critical reasoning**

1. Which existing SquiFlow boundaries legitimately resemble Adapter, Strategy, Command, or Observer?
2. Why does an Observer callback not provide durable delivery?
3. When does a Command object clarify domain intent and when does it become command-bus ceremony?
4. Why can Singleton be dangerous for mutable state but harmless for immutable/stateless services?
5. What concrete problem would justify a Factory or Builder in SquiFlow?

**Trade-off**

1. How much indirection is acceptable for a small team?
2. When does common vocabulary outweigh the cost of another abstraction layer?
3. When should a pattern be removed after requirements change?

**Failure / edge**

1. A process crashes after an in-process Observer callback was supposed to trigger email. What was wrongly assumed?
2. A Singleton stores tenant-specific mutable data. What cross-tenant/concurrency risk appears?
3. A Strategy interface has one implementation for three years. What should review ask?

**Implementation**

1. How can contract tests validate an Adapter-style provider boundary?
2. How should DI registration avoid becoming a service-locator pattern?
3. Which explicit states deserve a state machine versus ordinary conditional logic?
4. How can code review record the reason a pattern exists?

**System design interview**

1. Apply patterns selectively to SquiFlow storage, commands, and notifications without introducing a pattern framework.
2. Explain the difference between in-process Observer and durable event delivery.

**Challenge**

1. An architect proposes Factory + Builder + Strategy + Observer + Command Bus for a feature with one provider and one synchronous operation. Reduce it to the smallest design and state what future evidence would justify adding each pattern back.
