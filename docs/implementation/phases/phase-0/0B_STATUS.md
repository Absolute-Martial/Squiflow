# Phase 0B Status — Capability-First Foundation Discovery

**Status:** IN PROGRESS  
**Baseline dependency:** stacked on `phase0/0a-production-honest-requalification` / MR !64 until that baseline is merged  
**Gate owner:** `0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md`

## Production intent for the active slice

A developer can express the already-accepted structural Party distinction in host/provider-neutral capability code without conflating Party with Customer and without introducing shared Foundation/kernel abstractions that no current consumer needs.

This slice deliberately does **not** claim that the complete Party capability, a customer/account model, or Phase 0B itself is finished.

## Why this is the first code

`docs/domain/BUSINESS_TERMS.md` currently gives one stable Party semantic that does not depend on unresolved customer-specific vocabulary:

```text
Party
= person or organization participating in a business relationship or transaction context
≠ synonym for Customer
```

The following remain discovery-sensitive and are therefore not hardened by this slice:

```text
Customer / Party / Account / Commercial Relationship
Order / Request / Job / Work / Transaction / Sale
Quotation / Estimate / Tender / Offer
Supplier / outsourced producer / vendor
```

`docs/domain/CROSS_CUTTING_BUSINESS_PRIMITIVES.md` also leaves internal-ID representation, rounding, unit conversion, business-timezone behavior and document numbering intentionally unresolved for later real journeys. This slice does not invent those decisions.

## Current scope states

| Claim / responsibility | State | Exact current scope | Evidence / guard |
|---|---|---|---|
| `0B-PARTY-KIND` | `BLOCKED` pending executable evidence | `SquiFlow.Parties.Domain.PartyKind` contains only `Person` and `Organization`, matching the accepted Party structural meaning. It does not encode Customer/Supplier/Account roles. | `PartyKindTests.Accepted_kinds_match_the_documented_party_semantics`; source review against `BUSINESS_TERMS.md`. |
| `0B-PARTIES-DEPENDENCY-BOUNDARY` | `BLOCKED` pending executable evidence | The first Parties capability project is host/provider neutral and currently has no outward project or package dependency. | `PartiesDependencyBoundaryTests.Capability_has_no_outward_project_or_package_dependencies`; the test becomes a requalification point if a dependency is later earned. |
| `0B-VERIFICATION-CI` | `BLOCKED` pending a successful real CI execution | GitLab and GitHub each have a thin CI wrapper over the same repository-owned restore/build/test commands. GitLab is currently quota-blocked; the GitHub workflow exists in-repository but has not yet been executed on a mirrored GitHub branch. | `.gitlab-ci.yml`; `.github/workflows/verify-dotnet.yml`; GitLab pipeline `#174` failed before start with `ci_quota_exceeded`; GitHub execution evidence is still absent. |
| `0B-FOUNDATION-KERNEL` | `NOT_INTRODUCED` | No `SquiFlow.ApplicationKernel`, module descriptor graph, shared primitives project, feature/settings/permission kernel, host registry, or execution-mode enum exists. | Repository/project inventory; 0B owner explicitly requires current pressure before extraction. |
| `0B-PARTY-IDENTITY` | `NOT_INTRODUCED` | No GUID/ULID/string identity encoding/generator/API/persistence contract is selected. | Absence is intentional because current owners specify stable internal identity semantics but do not select an encoding. |
| `0B-PARTY-LIFECYCLE` | `NOT_INTRODUCED` | No Party aggregate lifecycle, mutation, contact/profile model, merge workflow, Customer/Account relationship, or persistence is claimed. | `BUSINESS_TERMS.md` and `CROSS_CUTTING_BUSINESS_PRIMITIVES.md` remain the source of current semantics/non-semantics. |
| `0B-RUNTIME` | `NOT_INTRODUCED` | No Web, Workstation, API, Sync, Worker, Admin, database/provider, or durable runtime is added. | Project inventory and dependency-boundary test. |

## Why the introduced code claims are currently `BLOCKED`

The source and test intent are reviewable, but this assistant workspace has no .NET SDK and cannot resolve external hosts, so local provisioning/execution is unavailable.

Both CI hosts now converge on the same repository-owned commands:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

GitLab pipeline `#174` (`2847992488`) was created for commit `2914b7d8`, proving the GitLab YAML is accepted. Its `verify-dotnet` job did **not** start: GitLab reported `failure_reason = ci_quota_exceeded`, with no runner assigned. Therefore the failed GitLab pipeline is an infrastructure/quota failure, not evidence that restore/build/tests failed.

The GitHub Actions workflow is now present at `.github/workflows/verify-dotnet.yml`. It uses `actions/checkout@v7`, `actions/setup-dotnet@v6`, reads the SDK selection from root `global.json`, and invokes the same restore/build/test sequence. Because the user manages GitHub mirroring/remotes independently and the branch has not yet been executed there, source review of the workflow is not treated as passing evidence.

A successful execution of the repository-owned commands on either legitimate CI host is sufficient executable evidence for the Party-kind and dependency-boundary code claims. The CI path that supplies that evidence must itself have actually run successfully; configuration presence alone is not enough.

Until executable evidence actually passes on this branch, the Party-kind and dependency-boundary claims remain `BLOCKED` and 0B cannot pass.

## Static review completed

Repository-side static review currently confirms:

- the branch contains exactly one production capability project and one test project;
- `SquiFlow.sln` contains exactly those two projects;
- the capability project is in the canonical compact path `modules/parties/SquiFlow.Parties/`;
- the test project is in `tests/unit/SquiFlow.Parties.Tests/`;
- no Foundation/ApplicationKernel, application/service executable, provider/persistence project, or second capability was introduced;
- the production project declares no package or project references;
- `Directory.Packages.props` contains only the currently consumed xUnit test package version;
- `global.json` retains the .NET 10 SDK baseline and selects Microsoft Testing Platform for the test runner;
- `.gitlab-ci.yml` and `.github/workflows/verify-dotnet.yml` are thin host-specific orchestration over the same repository-owned commands; neither introduces custom build scripts or an `eng/` tooling tree;
- GitHub's workflow reads the SDK from `global.json` instead of duplicating the pinned SDK version;
- current-state repository/structure documents distinguish the 0A zero-project snapshot from the active 0B two-project state;
- stale-state searches only return self-describing audit/status text or explicitly historical decision material, not an active focused owner claiming deleted runtime exists.

This is static/repository evidence only. It does **not** replace required restore/build/test execution.

## Test/runtime tooling introduced with the real code

The first test project uses xUnit v3 with Microsoft Testing Platform under the .NET 10 SDK:

- package: `xunit.v3.mtp-v2` `4.0.0`;
- central package version: `Directory.Packages.props`;
- `.NET 10` runner selection: `global.json` → `Microsoft.Testing.Platform`;
- GitLab recurring entry point: root `.gitlab-ci.yml`, using the official `mcr.microsoft.com/dotnet/sdk:10.0` SDK image;
- GitHub recurring entry point: `.github/workflows/verify-dotnet.yml`, using `actions/setup-dotnet@v6` with root `global.json`.

These CI files now exist because a real executable verification responsibility exists and GitLab hosted capacity is currently unavailable. They do not create a second test contract: both execute the same repository-owned commands.

## Permanent regression direction

Once executable evidence passes:

- the Party-kind contract test remains a normal per-change regression guard for the accepted semantic distinction;
- the Parties dependency-boundary test remains a normal guard while the capability has no earned outward dependency;
- GitLab and GitHub CI remain thin recurring orchestration over the same repository-owned commands; a host-specific workflow must not become a second source of build/test meaning;
- changes to `global.json`, the solution, projects, C# source, central build/package properties, or either CI definition requalify the executable verification claim;
- a future MR may deliberately change the dependency claim only by naming the real consumer/pressure, updating the scope record, and adding evidence for the new boundary;
- a future persistence/API/serialized contract must not infer stable numeric enum values from this internal type without an explicit compatibility decision.

## Relationship to Foundation

This slice is intentionally capability-first:

```text
accepted Party semantic
        ↓
modules/parties/SquiFlow.Parties
        ↓
real capability pressure accumulates
        ↓
only then ask whether anything is genuinely product-wide Foundation
```

There is currently no evidence that `PartyKind` belongs in Foundation. It remains Party-owned.

No second capability will be manufactured merely to create reuse evidence.

## 0B completion

Phase 0B is **not complete**.

Before the 0B gate itself can pass, real capability/application work must demonstrate whatever shared primitive/composition boundary is actually needed, all introduced responsibilities must be `PRODUCTION_HONEST`, future concerns must remain honestly `NOT_INTRODUCED`, dependency direction must be protected for the boundaries that exist, and `BLOCKED` must be empty.
