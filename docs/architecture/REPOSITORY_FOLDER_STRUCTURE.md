# Repository Folder-Only Structure

**Version:** v0.1.0
**Purpose:** directory-only view of the tracked SquiFlow repository structure.

**Current inventory:** the solution contains ten production projects and ten test projects, including Orders and its PostgreSQL adapter. See `README.IMPLEMENTATION.md` for the current responsibility-level implementation truth.

**Important:** directory presence is not proof that a runtime/project/responsibility is production-qualified.

This document complements `REPOSITORY_STRUCTURE.md`. It intentionally shows **folders only**. Files, project files, source files, configuration files, and documentation filenames are omitted from every tree.

For physical project placement, `REPOSITORY_STRUCTURE.md` is the more specific project-structure owner. A logical `core` responsibility does not require an extra `core/` folder when a compact capability is represented by one `Application.<Capability>/` project.

## 1. Current physical folder tree

This tree lists directories represented by tracked repository paths. Project and test project directories are shown as folders; no filenames are included. Some folders also reserve ownership without containing a current runtime project.

```text
SquiFlow/
├── .github/
│   └── workflows/
├── apps/
│   ├── desktop/
│   │   ├── guard/
│   │   └── workstation/
│   └── web/
├── deploy/
│   └── database/
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
│   │       ├── phase-10/
│   │       ├── phase-2/
│   │       ├── phase-3/
│   │       ├── phase-4/
│   │       ├── phase-5/
│   │       ├── phase-6/
│   │       ├── phase-7/
│   │       ├── phase-8/
│   │       └── phase-9/
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
├── infrastructure/
│   └── authorization/
│       └── openfga/
├── modules/
│   ├── application-profiles/
│   │   └── Application.Profiles/
│   ├── branding/
│   │   └── Application.Branding/
│   ├── identity-access/
│   │   ├── Application.IdentityAccess/
│   │   └── Application.IdentityAccess.Postgres/
│   │       ├── Migrations/
│   │       └── Properties/
│   ├── orders/
│   │   ├── Application.Orders/
│   │   └── Application.Orders.Postgres/
│   │       ├── Migrations/
│   │       ├── Properties/
│   │       └── Sql/
│   └── tenancy/
│       ├── Application.Tenancy/
│       └── Application.Tenancy.Postgres/
│           ├── Migrations/
│           └── Properties/
├── reference-sources/
├── services/
│   ├── core-api/
│   │   └── Application.CoreApi/
│   │       ├── Authentication/
│   │       ├── Authorization/
│   │       ├── Composition/
│   │       ├── Health/
│   │       └── Properties/
│   └── db-migrator/
│       └── Application.DatabaseMigrator/
│           └── Properties/
├── eng/
└── tests/
    ├── architecture/
    │   └── Application.Architecture.Tests/
    ├── integration/
    │   ├── Application.CoreApi.Tests/
    │   ├── Application.IdentityAccess.Postgres.Tests/
    │   ├── Application.Orders.Postgres.Tests/
    │   └── Application.Tenancy.Postgres.Tests/
    └── unit/
        ├── Application.Branding.Tests/
        ├── Application.IdentityAccess.Tests/
        ├── Application.Orders.Tests/
        ├── Application.Profiles.Tests/
        └── Application.Tenancy.Tests/
```

Tracked folders show physical layout only; they do not establish a runtime or production claim. The ignored `reference-sources/snapshots/` research payload is not part of this tracked-folder tree and is not a product project or dependency.

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
│   ├── db-migrator/
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

The `<capability>` child names above describe logical responsibility categories and possible future physical splits. They are **not mandatory physical folders**. A compact capability can instead be one project directory directly under the capability until a real compiler/provider/platform/packaging/lifecycle boundary earns `core/`, `server/`, `workstation/`, or `postgres/` separation.

## 3. Compact capability folder shape

The current preferred compact physical shape is:

```text
modules/
└── <capability>/
    └── Application.<Capability>/
        ├── Domain/
        ├── Application/
        ├── Decisions/
        ├── Rules/
        ├── Contracts/
        └── Events/
```

Create only the child folders that current code actually needs.

Conceptually this compact project owns the capability's host-neutral/core responsibility. It does not need a second physical `core/` layer merely to name that responsibility.

## 4. Earned provider/host expansion

When real boundaries appear, a capability may later gain responsibility-specific project/folder locations such as:

```text
modules/
└── <capability>/
    ├── Application.<Capability>/
    ├── Application.<Capability>.Postgres/
    └── Application.<Capability>.Workstation/
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
├── db-migrator/
├── sync-api/
├── admin-api/
└── worker/
```

These are workload/process boundaries, not separate business implementations. CoreApi owns the interactive Web/API role unless it is later renamed/replaced; do not add a forwarding-only WebApi beside it. The remaining folders are created only when their workload or trust boundary is implemented. All hosts converge on capability-owned application behavior.

## 7. Testing folder growth

The current architecture, unit and integration test projects cover the implemented slices listed in `README.IMPLEMENTATION.md`. As implementation grows, `tests/` may gain additional verification responsibilities:

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

This is a classification map, not a requirement to create all categories. `tests/architecture/` is now earned by the current project-graph and inventory drift guard; other categories still require an actual verification responsibility.

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
