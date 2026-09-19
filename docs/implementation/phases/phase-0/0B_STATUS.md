# Phase 0B Status — Capability-First Foundation Discovery

**Status:** RETIRED / HISTORICAL EVIDENCE

**Baseline:** former post-0A implementation, deleted on 2026-09-17

**Gate owner:** `0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md`

> The code, projects, solution, build/package files, tests, and CI wrappers described below no longer exist in the current tree. All claims and execution results in this file are qualification-time history. They are not current implementation evidence.

## Production intent achieved

A developer can express and extend real host/provider-neutral capability meaning without conflating it with stronger unresolved domain roles and without introducing shared Foundation/kernel abstractions that no current consumer needs.

The qualified real scenario is the accepted structural Party distinction:

```text
PartyKind
= Person | Organization
```

This is deliberately **not** a claim that the complete Party capability, a customer/account model, persistence, API/runtime behavior, or shared Foundation exists.

## Why this was the first code

`docs/domain/BUSINESS_TERMS.md` gives one stable Party semantic that does not depend on unresolved customer-specific vocabulary:

```text
Party
= person or organization participating in a business relationship or transaction context
≠ synonym for Customer
```

The following remain discovery-sensitive and were not hardened by 0B:

```text
Customer / Party / Account / Commercial Relationship
Order / Request / Job / Work / Transaction / Sale
Quotation / Estimate / Tender / Offer
Supplier / outsourced producer / vendor
```

`docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md` also leaves internal-ID representation, rounding, unit conversion, business-timezone behavior and document numbering unresolved for later real journeys. 0B did not invent those decisions.

## Qualified scope record

| Claim / responsibility | State | Exact qualified guarantee | Evidence / permanent guard |
|---|---|---|---|
| `0B-PARTY-KIND` | `PRODUCTION_HONEST` | `SquiFlow.Parties.Domain.PartyKind` contains exactly `Person` and `Organization`, matching the accepted structural Party meaning. It does not encode Customer/Supplier/Account roles. | `PartyKindTests.Accepted_kinds_match_the_documented_party_semantics`; successful GitHub Actions verification run `34916005915`, job `104213630182`; source owner `BUSINESS_TERMS.md`. |
| `0B-PARTIES-DEPENDENCY-BOUNDARY` | `PRODUCTION_HONEST` | The Parties capability is host/provider-neutral and currently has no outward project/package dependency. | `PartiesDependencyBoundaryTests.Capability_has_no_outward_project_or_package_dependencies`; successful GitHub Actions verification run `34916005915`, job `104213630182`; future dependency changes requalify this claim. |
| `0B-GITHUB-EXECUTION` | `PRODUCTION_HONEST` | The repository-owned verification contract can execute successfully on a real CI host using the committed GitHub workflow and root `global.json`. | Successful GitHub Actions run: `https://github.com/Absolute-Martial/Squiflow/actions/runs/34916005915/job/104213630182`; `.github/workflows/verify-dotnet.yml`; recurring execution on executable/build-input changes. |
| `0B-GITLAB-CI-WRAPPER` | `PRODUCTION_HONEST` for its narrow current claim | GitLab accepts `.gitlab-ci.yml` and creates the same `verify-dotnet` job using the repository-owned restore/build/test commands. No claim is made that a GitLab runner successfully executed them while hosted quota is exhausted. | GitLab clean-branch pipeline `#187` and MR pipelines `#188/#189` created the job but failed before start with `ci_quota_exceeded`, `runner = null`; `.gitlab-ci.yml` remains a thin wrapper. |
| `0B-FOUNDATION-KERNEL` | `NOT_INTRODUCED` | No `SquiFlow.ApplicationKernel`, module descriptor graph, shared primitives project, feature/settings/permission kernel, host registry, or execution-mode enum exists because the real Parties slice exposed no current product-wide reuse/change pressure requiring one. | Repository/project inventory plus the capability-first gate rule. This absence is the qualified 0B discovery result, not unfinished work. |
| `0B-PARTY-IDENTITY` | `NOT_INTRODUCED` | No GUID/ULID/string identity encoding/generator/API/persistence contract is selected. | Absence is intentional because current owners do not yet select an encoding. |
| `0B-PARTY-LIFECYCLE` | `NOT_INTRODUCED` | No Party aggregate lifecycle, mutation, contact/profile model, merge workflow, Customer/Account relationship, or persistence is claimed. | `BUSINESS_TERMS.md` and `CROSS_CUTTING_BUSINESS_PRIMITIVES.md`. |
| `0B-RUNTIME` | `NOT_INTRODUCED` | No Web, Workstation, API, Sync, Worker, Admin, database/provider, or durable runtime is added. | Project inventory and current capability dependency boundary. |

## BLOCKED

`BLOCKED = none` for the qualified 0B scope.

GitLab hosted quota remains an external CI-capacity limitation, but it no longer blocks the 0B code claims because the same repository-owned verification contract has now executed successfully on the GitHub CI host. The narrow GitLab-wrapper claim is only that GitLab accepts/schedules the same job; no false GitLab runner-success claim is made.

## Executable evidence

The repository-owned verification contract is:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

The maintainer supplied the successful GitHub Actions evidence for the committed workflow:

```text
run: 34916005915
job: 104213630182
https://github.com/Absolute-Martial/Squiflow/actions/runs/34916005915/job/104213630182
```

That workflow uses `actions/checkout@v7`, `actions/setup-dotnet@v6`, reads SDK selection from root `global.json`, and runs the repository-owned restore/build/test sequence.

The public GitHub job page was not machine-readable through the assistant web fetcher, so this status record does not invent step timing/output beyond the successful run evidence supplied by the maintainer and the commands defined by the committed workflow.

GitLab independently proves its wrapper is accepted/scheduled. On the clean branch, pipeline `#187` (`2848938219`) and MR pipelines `#188/#189` created `verify-dotnet` but failed before runner start with `ci_quota_exceeded`. Those are infrastructure-capacity failures, not build/test failures.

## Static repository evidence

Repository-side review confirms:

- exactly one production capability project and one test project are introduced;
- `SquiFlow.sln` contains exactly those two projects;
- the capability project is at `modules/parties/SquiFlow.Parties/`;
- the test project is at `tests/unit/SquiFlow.Parties.Tests/`;
- no Foundation/ApplicationKernel, application/service executable, provider/persistence project, or second capability exists;
- the production project declares no package or project references;
- `Directory.Packages.props` contains only the currently consumed xUnit test package version;
- `global.json` retains the .NET 10 SDK baseline and selects Microsoft Testing Platform;
- `.gitlab-ci.yml` and `.github/workflows/verify-dotnet.yml` are thin host-specific wrappers over the same repository-owned commands;
- GitHub reads the SDK from `global.json` instead of duplicating the SDK pin;
- the branch is based on current `main` after merged !64 rather than carrying the pre-squash 0A ancestry from superseded !66.

## Test/runtime tooling introduced with the real code

The first test project uses xUnit v3 with Microsoft Testing Platform under the .NET 10 SDK:

- package: `xunit.v3.mtp-v2` `4.0.0`;
- central package version: `Directory.Packages.props`;
- runner selection: `global.json` → `Microsoft.Testing.Platform`;
- GitLab recurring wrapper: root `.gitlab-ci.yml` with the official .NET 10 SDK image;
- GitHub recurring wrapper: `.github/workflows/verify-dotnet.yml` with `actions/setup-dotnet@v6` and root `global.json`.

These do not create two test contracts. They execute the same repository-owned verification commands.

## Permanent regression protection

The qualified claims remain protected by:

- `PartyKindTests.Accepted_kinds_match_the_documented_party_semantics` for the accepted Party semantic;
- `PartiesDependencyBoundaryTests.Capability_has_no_outward_project_or_package_dependencies` while the capability legitimately has no outward dependency;
- GitHub/GitLab CI wrappers over the same restore/build/test contract;
- `global.json` as SDK authority;
- requalification whenever `global.json`, solution/projects, C# source, central build/package properties, or either CI definition materially changes the executable claim;
- explicit scope/evidence update before any future outward dependency or shared Foundation extraction is accepted;
- explicit compatibility decision before any persistence/API contract treats `PartyKind` numeric values as stable serialized meaning.

## Relationship to Foundation

The qualified 0B discovery result is:

```text
accepted Party semantic
        ↓
modules/parties/SquiFlow.Parties
        ↓
real capability pressure observed
        ↓
no product-wide shared primitive currently earned
        ↓
Foundation remains NOT_INTRODUCED
```

`PartyKind` remains Party-owned. No second capability was manufactured merely to create reuse evidence.

If later real work independently exposes a product-wide primitive, Foundation can be introduced then under a new explicit claim/evidence record. 0B completion does not freeze Foundation out forever.

## Exit-gate result

0B is **COMPLETE / QUALIFIED** because:

- a real capability-owned semantic exists;
- its semantic and dependency claims have successful executable evidence;
- dependency direction is mechanically protected for the boundary that exists;
- no topology metadata leaked into the capability;
- no speculative shared kernel/framework was created;
- shared Foundation remains honestly `NOT_INTRODUCED` because current pressure did not earn it;
- future Party/runtime concerns remain honestly `NOT_INTRODUCED`;
- permanent regression/requalification guards are recorded;
- `BLOCKED = none`.

## Handoff

0B completion does not automatically authorize 0C scaffolding. Phase 0C becomes active only when a real executable/host/process responsibility earns a composition boundary.

Until then, continue from real product/capability responsibility and pull the appropriate Phase-0 work area forward only when current facts require it.
