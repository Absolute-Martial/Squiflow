# SquiFlow (internal development codename)

**Current product version:** `v0.1.0` (locked until the complete production-capable product gate)

**Implementation state:** first independently earned backend/API slice after the 2026-09-17 purge. Phase 0A remains qualified; the former 0B Parties implementation is retired history.

The repository codename is not the public product identity. Every deployed presentation obtains its name, legal identity, theme and links from validated branding configuration; the checked-in runtime has no codename branding fallback. Earlier code and retired phase records remain in Git history as evidence and context, not current implementation authority.

## Read in this order

1. `README.IMPLEMENTATION.md` — current implementation truth.
2. `docs/product/PRODUCT_IDENTITY_AND_VERSIONING.md` — codename, public identity and locked-version contract.
3. `docs/decisions/CURRENT_IMPLEMENTATION_PURGE_2026-09-17.md` — purge decision and removed inventory.
4. `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md` — scope and quality contract.
5. `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md` — evidence permanence and requalification contract.
6. `docs/implementation/phases/phase-0/0A_BASELINE_STATUS.md` — qualified reset baseline.
7. `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md` — proven-source routing for future implementation.
8. `docs/domain/BUSINESS_TERMS.md`, `docs/decisions/CURRENT_DECISIONS.md`, and `OPEN_DECISIONS.md` — accepted and unresolved product meaning.
9. The focused owner for the responsibility being changed.

## Source precedence

```text
focused canonical owner
        ↓
accepted/current decision record
        ↓
current implementation/gate record
        ↓
historical review/source-study/branch/MR material
```

Repository state proves what is implemented. Architecture documents can define future ownership and accepted direction without implying that a project or runtime exists.

## Current implementation boundary

```text
production projects: 8
test projects:       7
executable hosts:    2
solution/build/test contract: present
active runtime responsibilities: public application bootstrap; classified OpenAPI v1 description; JWT access-token validation; authenticated account resolution; tenant membership listing/context resolution; account/tenancy persistence; one-shot DB migration
active host-neutral responsibilities: bounded feature graph validation and deterministic effective-selection compilation
BLOCKED: none
```

The deleted `PartyKind` enum and Parties implementation are not current implementation. The current solution/build/test files belong only to the independently earned ApplicationProfiles, Branding, IdentityAccess, Tenancy, CoreApi and DbMigrator slices and do not revive the retired 0B shape.

## Architecture direction

- C# and modern .NET remain the application direction; .NET 10 is the current toolchain baseline when code is reintroduced.
- Avalonia Workstation and Blazor tenant Web remain accepted presentation directions when those surfaces are implemented.
- ASP.NET Core remains the server-host foundation when server hosts are implemented.
- The product starts as a modular monolith; ordinary capability communication is in-process.
- One capability owns one source implementation of its business meaning.
- PostgreSQL remains selected central transactional storage and SQLite/WAL remains selected Workstation local storage when those responsibilities are introduced.
- ZITADEL remains the selected identity platform; CoreApi now enforces its configured OIDC/JWT issuer and audience contract, while live-provider/login/session evidence remains pending. OpenFGA and OpenBao/Vault-style key management remain accepted directions, not current runtimes.
- Guard remains a future supervision/recovery boundary, not business authority.
- Projects, processes, providers, interfaces, and protocols are earned by a real boundary.

## Development rule

> **Scope is a choice; honesty is not.**

```text
real useful responsibility
        ↓
inspect applicable proven implementations
        ↓
declare production intent and exact scope
        ↓
NOT_INTRODUCED / PRODUCTION_HONEST / BLOCKED
        ↓
implement the smallest useful production-honest slice
        ↓
prove claims with falsifiable evidence
        ↓
retain recurring regression guards
        ↓
qualify only with BLOCKED = none
```

Production-honest does not mean writing every mechanism from scratch. Before custom infrastructure, inspect the source review and record whether a focused dependency, bounded source adaptation, reusable tests/algorithms, or reference-only decision fits the active responsibility. SquiFlow keeps ownership of its business meaning, authority, security, compatibility, and evidence.

## Gate and verification status

Phase 0A is the current qualified baseline. The former 0B qualification proved properties of code that has since been deliberately purged, so those claims and CI results are historical and no longer describe the repository.

The repository-owned verification entry point is `./eng/verify.sh`. It restores, verifies formatting, builds in Release configuration and runs the complete test suite, including real PostgreSQL Testcontainers integration tests. `.github/workflows/verify.yml` is the thin GitHub Actions wrapper around the same command. A workflow definition is not evidence that a remote run passed; report remote CI evidence only after inspecting that run.

Phase 0 and the already-concrete Phase 1 trust boundary retain architecture direction. Phase 2–10 remain direction-only `NOT_INTRODUCED` planning until real work earns detail.

## Versioning

The product version is exactly `v0.1.0` and remains locked there until the complete product is explicitly qualified as capable of its accepted real-production responsibilities. Git revisions identify builds and changes without creating alternate product versions. `/api/v1` and similar identifiers version a contract family; they are not the product version. Historical documents may retain the version that identified their original decision or qualification event.
