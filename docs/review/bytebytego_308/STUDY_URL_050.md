# URL 050 — Clean Architecture 101: Building Software That Lasts

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: a comparison, checklist, pattern catalog, popularity claim, maturity ladder, or source diagram never selects SquiFlow architecture by itself. The review must first identify what SquiFlow is actually doing at the corresponding boundary, why that mechanism exists, what authority it owns, what it costs, and what evidence would justify changing it.

## A. Identification

- **URL occurrence:** `050`
- **PDF page:** `294`
- **Source URL:** `https://blog.bytebytego.com/p/clean-architecture-101-building-software`
- **Source access:** paid article with public preview; no bypass.
- **Related supplied visual:** archive page 264, classic Clean Architecture rings/dependency rule.
- **Visual inspected:** PDF page `294` at full size.

## B. Core concept

### SOURCE

The public preview describes Clean Architecture as keeping core business rules independent of frameworks, databases and user-interface details. It presents layered responsibilities with dependencies flowing inward toward business logic, aiming at maintainability, testability, modularity and technology replacement. It credits the approach to Robert C. Martin and earlier Hexagonal/Onion ideas.

### INFERENCE

The useful SquiFlow principle is dependency direction around stable business invariants and real external seams. The concentric-ring diagram is not a requirement to create one project/interface for every ring or to route every request through multiple generic forwarding layers.

### EXTERNAL KNOWLEDGE / CAVEAT

Clean Architecture can add indirection, mapping and interface ceremony if applied mechanically. It does not automatically improve runtime scalability; its primary benefits are change/test/dependency isolation. Framework/database independence is a spectrum: provider-specific code can be contained in infrastructure without inventing generic repositories, and domain code can use pragmatic language/runtime types. A real process/security/provider replacement boundary deserves stronger abstraction than speculative future variability.

## C. Important concepts

- dependency rule/inward dependencies;
- domain/application business rules;
- interface adapters/infrastructure;
- composition roots;
- narrow ports at real replacement boundaries;
- provider type containment;
- module ownership;
- testability versus mockability;
- framework isolation without ceremony;
- modular monolith versus service boundaries;
- architecture dependency tests;

## D. Diagram / visual explanation

The classic visual has Entities at the center, Use Cases around them, Interface Adapters outside, and Frameworks/Drivers at the edge, plus controller/input port/interactor/output port/presenter flow. SquiFlow should interpret this as dependency direction, not a mandatory runtime call chain or project count. A simple handler can invoke domain behavior and concrete infrastructure composition while dependencies still point appropriately.

## E. How it works — step by step

1. Identify the stable business invariant/use case.
2. Keep domain/application semantics independent of ASP.NET/Blazor/Avalonia/provider SDK details where that independence has real value.
3. Compose infrastructure/framework integrations at the runtime edge/composition root.
4. Introduce narrow interfaces only for genuine replacement/process/security/test contract seams such as `IObjectStore` and `IBackupTarget`.
5. Keep provider-specific SQL/ORM/SDK code contained rather than forcing generic repository abstractions.
6. Use architecture dependency tests to prevent UI/provider/framework types leaking into core contracts.
7. Add another assembly/layer only when dependency growth or a real boundary earns it.

## F. Why it matters

SquiFlow already has several real external boundaries—ZITADEL, OpenFGA, object/backup providers, Workstation Guard process, Core/Admin hosts—while still favoring a modular monolith for business code. Dependency discipline protects those seams without paying distributed-system or abstraction costs everywhere.

## G. Trade-offs / limitations

Strong separation improves replaceability/testing and can prevent framework leakage, but too many interfaces/projects/mappers slow development and obscure business intent. Direct concrete code is simpler when there is no real replacement boundary. Provider-specific integration tests are still needed; mocking an abstraction is not proof of provider behavior.

## H. Alternatives / comparisons — fit, not winner/loser

```text
cohesive concrete module code
    -> default when no real replacement/boundary exists

narrow port/interface
    -> real provider/process/replacement seam

contained provider-specific infrastructure code
    -> avoids leaking SDK/SQL while keeping implementation pragmatic

separate executable/service
    -> only for independent deployment/security/fault/availability boundary

full multi-layer project hierarchy
    -> only if codebase scale/dependency ownership earns it, not because the diagram has rings
```

## I. Real implementation considerations

Every adoption/change is required to state its owner/authority, failure behavior, recovery path, implementation evidence and operating burden. A source list is not implementation evidence.

### Implications for the Current Implementation

- **KEEP:** modular-monolith business core, in-process module calls, Core/Admin/Worker composition hosts, and provider SDK containment.
- **KEEP:** `IObjectStore`/`IBackupTarget` because provider replacement is already committed; Guard/Admin API because process/security/availability boundaries are real.
- **KEEP:** reject generic `IRepository<T>`, `IUnitOfWork`, one-interface-per-class and placeholder projects merely for architectural purity.
- **IMPROVE NOW (implementation gate):** when source exists, architecture dependency tests must prove domain/application code does not leak ASP.NET/provider SDK/UI concerns and that provider contracts are tested against real adapters.
- **LATER / SCALE TRIGGER:** split assemblies/services only when dependency ownership, independent deployment, security/fault isolation or team/runtime needs become real.
- **AVOID:** translating the ring diagram into mandatory controller -> port -> interactor -> port -> presenter layers for every trivial use case or claiming Clean Architecture itself improves runtime scale.

**What are we actually doing and why?** We are using dependency direction and narrow abstractions at real provider/process/security boundaries because those properties protect business rules from known external change and failure. We are not creating a full layer/interface hierarchy for every class because speculative indirection is a cost; we would add stronger boundaries only when implementation dependency growth or real replacement/deployment evidence earns them.

**Implementation-evidence status:** the repository is still documentation/planning only at the root (no application source tree committed). These are accepted design requirements and future verification gates, not claims that the controls/behavior already exist in running code.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. What is the Clean Architecture dependency rule?
2. What belongs toward the core versus at the infrastructure edge?
3. Why is a port/interface not automatically required for every class?

**Critical reasoning**

1. Which SquiFlow interfaces/executables have already earned their boundary and why?
2. Why is a generic repository not necessary for provider portability?
3. How can provider-specific SQL remain without contaminating domain code?
4. What architecture dependency rules should CI prove once source exists?
5. When should a module become a separate assembly or service?

**Trade-off**

1. What testability benefit comes from an interface and when does it merely enable over-mocking?
2. When is direct concrete code safer/simpler than another abstraction?
3. How much mapping between DTO/domain/persistence is useful before it becomes ceremony?

**Failure / edge**

1. A provider SDK type leaks into a durable business record. What future migration problem appears?
2. A domain rule depends on ASP.NET `HttpContext`. What test/deployment coupling follows?
3. A service is extracted only to satisfy Clean Architecture layering. What distributed failures are introduced?
4. A mocked storage adapter passes tests but real provider semantics differ. What test layer is missing?

**Implementation**

1. Which namespace/project dependency tests should be automated?
2. Where do composition roots instantiate ZITADEL/OpenFGA/storage implementations?
3. How are adapter contract tests structured?
4. What criteria justify a new project/executable?

**System design interview**

1. Sketch a lean Clean-Architecture-compatible SquiFlow quote approval flow without unnecessary layers.
2. Explain how a modular monolith can follow dependency inversion without being microservices.

**Challenge**

1. A proposal adds 12 projects, 30 interfaces, generic repositories and a mediator pipeline before the first business slice. Evaluate which boundaries are real, which are speculative, and what minimum structure preserves the useful Clean Architecture property.
