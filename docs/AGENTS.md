# Documentation and Architecture Decision Instructions

These rules apply below `docs/` in addition to the root instructions.

## Documentation is part of the architecture contract

SquiFlow uses focused owner documents to prevent architecture from living only in chat or code folklore. Keep one clear owner per responsibility and link to it rather than cloning large decision blocks into many files.

## Source-of-truth discipline

- A focused current owner should state the accepted behavior/invariant for its responsibility.
- `CURRENT_DECISIONS.md` summarizes accepted decisions; it should not quietly conflict with focused owners.
- `OPEN_DECISIONS.md` records choices intentionally not yet closed.
- Review/source-study documents are evidence/context, not automatic architecture authority. The application baseline source review is mandatory routing input before custom infrastructure, but each dependency/adaptation still needs an active source-admission decision.
- Implementation-phase documents describe sequencing/maturity; they do not override a focused architecture/security/data owner without an explicit decision change.

When changing a material decision, update the focused owner and the appropriate decision record in the same change when practical.

## Writing conventions

- Use precise scoped claims: `Workstation local DB is SQLite/WAL` is better than `we use SQLite everywhere`.
- Distinguish accepted architecture, implemented code, selected-but-not-yet-implemented mechanisms, provisional assumptions, and open decisions.
- Do not write that a feature/control is implemented merely because its architecture has been accepted.
- Prefer diagrams/tables when they clarify ownership/flow, but accompany them with invariant prose so the meaning survives formatting changes.
- Mark examples as illustrative when exact names/ports/thresholds are not architecture.
- Keep dates/versions/status accurate when used.

## Architecture change checklist

Before accepting a new mechanism/technology in docs, answer as applicable:

1. What exact workload/problem/invariant requires it?
2. What is the current simpler mechanism?
3. Why is the new complexity earned now?
4. What authority/durability/security boundary changes?
5. What new failure/recovery/operational burden appears?
6. What compatibility/migration/exit path is required?
7. What evidence/POC/measurement validates the choice?
8. What future evidence would make us revisit/remove it?

## DO NOT

- Do not turn comparison/review articles into automatic technology selections.
- Do not copy generic clean-code/SOLID/cloud-native rules verbatim when they conflict with SquiFlow decisions.
- Do not describe modules as microservices just because they are bounded contexts.
- Do not claim `stateless` means HA, `event-driven` means broker, `internal` means gRPC, `cloud-native` means Kubernetes, or `versioned` means runtime schema registry.
- Do not use simplistic CAP-theorem slogans as a design decision without identifying the actual distributed data model and failure requirement.
- Do not state that all app state must be externalized to Redis; SquiFlow intentionally has local Workstation state and durable provider-owned stores.
- Do not duplicate long canonical decisions into AGENTS/phase/review docs; link to the owner and capture only the local instruction/implication.
- Do not silently rewrite historical decision/review documents to make history appear cleaner.

## Validation

For documentation changes that affect architecture/project paths/commands, run the root build/spec commands when a current executable contract exists and inspect referenced paths. The post-purge tree currently has no such contract. A documentation-only MR can still create harmful architecture drift, so verify that examples and current-state claims match the repository.
