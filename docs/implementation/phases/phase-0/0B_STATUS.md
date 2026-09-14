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
| `0B-FOUNDATION-KERNEL` | `NOT_INTRODUCED` | No `SquiFlow.ApplicationKernel`, module descriptor graph, shared primitives project, feature/settings/permission kernel, host registry, or execution-mode enum exists. | Repository/project inventory; 0B owner explicitly requires current pressure before extraction. |
| `0B-PARTY-IDENTITY` | `NOT_INTRODUCED` | No GUID/ULID/string identity encoding/generator/API/persistence contract is selected. | Absence is intentional because current owners specify stable internal identity semantics but do not select an encoding. |
| `0B-PARTY-LIFECYCLE` | `NOT_INTRODUCED` | No Party aggregate lifecycle, mutation, contact/profile model, merge workflow, Customer/Account relationship, or persistence is claimed. | `BUSINESS_TERMS.md` and `CROSS_CUTTING_BUSINESS_PRIMITIVES.md` remain the source of current semantics/non-semantics. |
| `0B-RUNTIME` | `NOT_INTRODUCED` | No Web, Workstation, API, Sync, Worker, Admin, database/provider, or durable runtime is added. | Project inventory and dependency-boundary test. |

## Why the introduced claims are currently `BLOCKED`

The source and test intent are reviewable, but this environment does not contain the .NET SDK, so the required executable evidence has not been run.

The introduced code therefore cannot be promoted to `PRODUCTION_HONEST` merely because it looks correct in review.

Required evidence:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

Until those commands pass on the branch (or equivalent repository-owned CI evidence exists), the two introduced implementation claims remain `BLOCKED` and 0B cannot pass.

## Test/runtime tooling introduced with the real code

The first test project uses xUnit v3 with Microsoft Testing Platform under the .NET 10 SDK:

- package: `xunit.v3.mtp-v2` `4.0.0`;
- central package version: `Directory.Packages.props`;
- `.NET 10` runner selection: `global.json` → `Microsoft.Testing.Platform`.

This tooling exists because a real capability boundary now exists. It is not a revived 0A architecture-test project.

## Permanent regression direction

Once executable evidence passes:

- the Party-kind contract test remains a normal per-change regression guard for the accepted semantic distinction;
- the Parties dependency-boundary test remains a normal guard while the capability has no earned outward dependency;
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
