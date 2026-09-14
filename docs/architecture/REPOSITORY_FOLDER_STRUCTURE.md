# Repository Folder-Only Structure

**Version:** v0.0.20  
**Purpose:** directory-only view of SquiFlow repository structure.  
**Important:** directory presence is not proof that a runtime/project is implemented. The principles-first reset intentionally has no production/test `*.csproj` implementation yet.

This document complements `REPOSITORY_STRUCTURE.md`. It intentionally shows **folders only**. Files, project files, source files, configuration files, and documentation filenames are omitted from every tree.

## 1. Current physical folder tree

The current repository contains these tracked directory paths:

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
├── services/
└── tests/
```

Some source-area directories currently exist only because repository guidance such as scoped agent instructions is tracked there. They must not be interpreted as evidence that the previous implementation survived the reset.

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

The `<capability>` child folders above are responsibility labels, not mandatory physical splits. A capability should stay compact until compiler/provider/platform/packaging/lifecycle pressure earns separation.

## 3. Compact capability folder shape

When a real capability begins, prefer the smallest folder shape that completely expresses its current responsibility:

```text
modules/
└── <capability>/
    └── core/
        ├── domain/
        ├── application/
        │   ├── commands/
        │   ├── queries/
        │   └── admission/
        ├── decisions/
        ├── rules/
        ├── contracts/
        └── events/
```

Do not create every child automatically. If the capability has no real `events/`, `admission/`, or separate `decisions/` responsibility yet, those folders should not exist merely to make the tree symmetrical.

## 4. Earned provider/host expansion

A capability may later grow to:

```text
modules/
└── <capability>/
    ├── core/
    ├── postgres/
    └── workstation/
```

and, only when the compile-time/application split is genuinely useful:

```text
modules/
└── <capability>/
    ├── core/
    ├── server/
    ├── workstation/
    └── postgres/
```

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

The current reset has no test projects. As implementation is reintroduced, `tests/` may grow by verification responsibility rather than by mirroring every production folder:

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

This is a classification map, not a requirement to create all categories. Use only the layers needed to prove real claims.

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

The folder map therefore distinguishes three concepts:

```text
folder exists
    ≠ project exists
    ≠ runtime exists
    ≠ responsibility is production-qualified
```
