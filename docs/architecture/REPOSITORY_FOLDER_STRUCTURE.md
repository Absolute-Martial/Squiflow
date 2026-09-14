# Repository Folder-Only Structure

**Version:** v0.0.20  
**Purpose:** directory-only view of SquiFlow repository structure.  
**Important:** directory presence is not proof that a runtime/project/responsibility is production-qualified. The 0A reset snapshot was implementation-empty; the active 0B slice has now earned one compact capability project and one unit-test project.

This document complements `REPOSITORY_STRUCTURE.md`. It intentionally shows **folders only**. Files, project files, source files, configuration files, and documentation filenames are omitted from every tree.

For physical `.csproj` placement, `REPOSITORY_STRUCTURE.md` is the more specific project-structure owner. A logical `core` responsibility does not require an extra `core/` folder when a compact capability is represented by one `SquiFlow.<Capability>/` project.

## 1. Current physical folder tree

The active 0B branch contains these source/test paths in addition to the retained documentation/ownership directories:

```text
SquiFlow/
├── apps/
│   ├── desktop/
│   │   ├── guard/
│   │   └── workstation/
│   └── web/
├── deploy/
├── docs/
│   ├── admin/
│   ├── api/
│   ├── architecture/
│   ├── data/
│   ├── decisions/
│   ├── domain/
│   ├── implementation/
│   │   └── phases/
│   │       ├── phase-0/
│   │       ├── phase-1/
│   │       ├── phase-2/
│   │       ├── phase-3/
│   │       ├── phase-4/
│   │       ├── phase-5/
│   │       ├── phase-6/
│   │       ├── phase-7/
│   │       ├── phase-8/
│   │       ├── phase-9/
│   │       └── phase-10/
│   ├── integrations/
│   ├── observability/
│   ├── operations/
│   ├── product/
│   ├── requirements/
│   ├── review/
│   │   ├── bytebytego_308/
│   │   └── product_requirements_sources/
│   ├── rules/
│   ├── security/
│   ├── server/
│   ├── sync/
│   ├── testing/
│   ├── web/
│   ├── workflow/
│   └── workstation/
├── foundation/
├── modules/
│   └── parties/
│       └── SquiFlow.Parties/
│           └── Domain/
├── services/
└── tests/
    └── unit/
        └── SquiFlow.Parties.Tests/
            ├── Architecture/
            └── Domain/
```

`modules/parties/SquiFlow.Parties/` is the first real capability project location. The empty `foundation/`, application/service ownership folders, and other reserved directories still do not imply those runtimes/projects exist.

## 2. Accepted growth map — folders only

The following is the accepted ownership/growth map. It is **not a scaffolding checklist**. Create a folder only when real implementation work earns it.

```text
SquiFlow/
├── apps/
│   ├── web/
│   └── desktop/
│       ├── workstation/
│       ├── guard/
│       ├── diagnostics/
│       ├── maintenance/
│       ├── sync/
│       └── document/
├── services/
│   ├── core-api/
│   ├── web-api/
│   ├── sync-api/
│   ├── admin-api/
│   └── worker/
├── modules/
│   └── <capability>/
│       ├── core/
│       ├── server/
│       ├── workstation/
│       └── postgres/
├── foundation/
│   ├── application-kernel/
│   ├── observability/
│   └── workstation-runtime/
├── infrastructure/
│   ├── storage/
│   ├── backup/
│   ├── identity/
│   └── authorization/
├── tests/
├── deploy/
└── docs/
```

The `<capability>` child names above describe logical responsibility categories and possible future physical splits. They are **not mandatory physical folders**. A compact capability can instead be one project directory directly under the capability, as shown by `modules/parties/SquiFlow.Parties/`, until a real compiler/provider/platform/packaging/lifecycle boundary earns `core/`, `server/`, `workstation/`, or `postgres/` separation.

## 3. Compact capability folder shape

The current preferred compact physical shape is:

```text
modules/
└── <capability>/
    └── SquiFlow.<Capability>/
        ├── Domain/
        ├── Application/
        ├── Decisions/
        ├── Rules/
        ├── Contracts/
        └── Events/
```

Create only the child folders that current code actually needs. The first Parties slice currently needs only `Domain/`.

Conceptually this compact project owns the capability's host-neutral/core responsibility. It does not need a second physical `core/` layer merely to name that responsibility.

## 4. Earned provider/host expansion

When real boundaries appear, a capability may later gain responsibility-specific project/folder locations such as:

```text
modules/
└── <capability>/
    ├── SquiFlow.<Capability>/
    ├── SquiFlow.<Capability>.Postgres/
    └── SquiFlow.<Capability>.Workstation/
```

and, only when a separate authoritative application/compile-time boundary is genuinely useful, another project may be introduced for that responsibility according to `REPOSITORY_STRUCTURE.md` and the focused capability owner.

This is progressive growth, not a required final shape.

## 5. Desktop process growth

```text
apps/
└── desktop/
    ├── workstation/
    ├── guard/
    ├── diagnostics/
    ├── maintenance/
    ├── sync/
    └── document/
```

Only `workstation/` and `guard/` are reserved primary desktop responsibilities. The other helper-process folders are created only when fault/resource/security/lifecycle isolation earns a separate process.

## 6. Server process growth

```text
services/
├── core-api/
├── web-api/
├── sync-api/
├── admin-api/
└── worker/
```

These are workload/process boundaries, not separate business implementations. They converge on capability-owned application behavior. Do not create a folder simply because it appears in this growth map.

## 7. Testing folder growth

The active 0B slice has earned only the unit-test category:

```text
tests/
└── unit/
    └── SquiFlow.Parties.Tests/
```

As implementation grows, `tests/` may gain other verification responsibilities:

```text
tests/
├── architecture/
├── unit/
├── application/
├── integration/
├── compatibility/
├── process/
├── recovery/
└── performance/
```

This is a classification map, not a requirement to create all categories. A small architecture assertion may live inside an existing test project, as the current Parties dependency-boundary test does; create a separate `tests/architecture/` project/folder only when that distinct verification responsibility is earned.

## 8. Folder creation rule

A new source/runtime folder should answer at least one concrete question:

- what responsibility does this folder own?
- what dependency direction does it protect?
- what provider/platform/process boundary does it isolate?
- what distinct lifecycle/security/fault/resource behavior requires it?
- what verification proves the boundary?

If the only answer is “the architecture diagram shows it,” do not create the folder yet.

## 9. Relationship to scoped agent instructions

Scoped `AGENTS.md` files may cause an ownership directory to exist before runtime implementation exists. That is intentional: the folder can reserve local engineering rules without claiming a project or executable has been built.

The folder map therefore distinguishes:

```text
folder exists
    ≠ project exists
    ≠ runtime exists
    ≠ responsibility is production-qualified
```
