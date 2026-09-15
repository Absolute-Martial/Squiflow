# Phase 0 — Architectural Development Foundation

**Status:** active after principles-first implementation reset  
**Current baseline:** v0.0.20  
**Completed/qualified subphases:** 0A and 0B under the production-honest model  
**Next work:** derived from the next real responsibility; 0C is not automatically active  
**0B qualification record:** `0B_STATUS.md`  
**High-level owner:** `docs/implementation/PHASES_AND_GATES.md`  
**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

Phase 0 is developed as a sequence of **smallest production-honest scopes**, not as a checklist whose prewritten boxes authorize implementation.

## Current repository state

0A's reset snapshot contained no production/test projects. Qualified 0B introduced the first earned implementation boundary:

```text
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
```

The current production-code scope is only the accepted Party structural distinction `Person | Organization`. No Foundation/ApplicationKernel project, application/service executable, persistence/provider adapter, sync/runtime security implementation, or complete Party/Customer model is claimed.

0B is complete with `BLOCKED = none`. Its introduced semantic/dependency claims are executable-evidence-backed, while shared Foundation remains deliberately `NOT_INTRODUCED` because the real capability slice did not earn a product-wide abstraction. See `0B_STATUS.md`.

Read first:

- `0A_ARCHITECTURE_BASELINE_RECONCILIATION.md`;
- `0A_BASELINE_STATUS.md`;
- `0B_APPLICATION_KERNEL_AND_MODULE_FOUNDATION.md`;
- `0B_STATUS.md`;
- `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`;
- `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`;
- `docs/implementation/PHASES_AND_GATES.md`;
- `docs/domain/BUSINESS_TERMS.md`;
- `docs/architecture/ENGINEERING_PRINCIPLES.md`;
- `docs/architecture/EXPLICIT_BOUNDARIES_AND_SOLID.md`;
- `docs/architecture/REPOSITORY_STRUCTURE.md`;
- `docs/architecture/REPOSITORY_FOLDER_STRUCTURE.md`.

## Development rule

For every active slice:

```text
real responsibility / workload
        ↓
production intent
        ↓
exact declared scope
        ↓
NOT_INTRODUCED / PRODUCTION_HONEST / BLOCKED
        ↓
implementation
        ↓
falsifiable evidence at the owning layer
        ↓
permanent / recurring regression protection
        ↓
qualification with BLOCKED = none
```

A phase/subphase name is planning vocabulary. It does not authorize a project, provider, interface, process, technology, test matrix, or runtime behavior by itself.

## Rebuild rule

Do not rebuild by reproducing the previous folder/project list. Reintroduce a boundary only when a real declared scope needs it and the pressure/ownership is current.

A component may be narrow, but anything introduced on a claimed path must be production-honest for that exact claim. A known incomplete/unproven responsibility is `BLOCKED`; it cannot be labeled future hardening and carried forward.

Qualified 0B demonstrated this direction:

```text
accepted Party semantic
        ↓
capability-owned code first
        ↓
observe real reuse/change pressure
        ↓
no product-wide primitive earned yet
        ↓
Foundation remains NOT_INTRODUCED
```

Do not create ApplicationKernel/module-composition machinery merely because the subphase title contains “Application Kernel and Module Foundation.”

## KISS / YAGNI

KISS means minimum accidental complexity and the **smallest production-honest scope**. It never means happy-path only, fewest files at any cost, skipped recovery/security/compatibility, fake durability/authority, or hidden edge cases.

YAGNI removes speculative breadth. It does not weaken the depth required by a responsibility already introduced.

## Scope states

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

Only `NOT_INTRODUCED` can be deferred. `BLOCKED` must be finished to `PRODUCTION_HONEST` or explicitly un-introduced before the owning gate can pass.

## Phase-0 work areas

```text
0A  Architecture/repository baseline reconciliation                         QUALIFIED
0B  Capability-first discovery of any genuinely shared kernel/foundation     QUALIFIED
0C  Host/process composition when a real executable earns the boundary       NOT ACTIVE YET
0D  Engineering safety/observability/reproducibility as real code needs it
0E  Active capability/extensibility work through real product slices
0F  Integrated Phase-0 qualification when enough real Phase-0 scope exists
```

These labels describe useful responsibility areas. They are not permission to prebuild all listed concerns, and their internal scope may be reshaped as real work provides better facts.

## 0A permanence

0A's enduring guarantees remain active after later code appears:

- current authority must remain distinguishable from history;
- folder/diagram presence must not be mistaken for implementation;
- project/process/provider boundaries remain earned;
- the production-honest state model governs introduced scope;
- evidence must remain traceable and protected from silent regression;
- future governance specificity remains earned.

The zero-project inventory itself was only the qualification snapshot of the reset. The first Parties/test projects are an expected earned transition, not a regression of 0A.

## 0B permanence

0B's enduring guarantees are now:

- capability semantics remain owned by their capability;
- a real compile-time dependency boundary is mechanically protected;
- shared Foundation is extracted only from actual product-wide pressure rather than phase symmetry;
- absence of Foundation is a valid qualified result when the real slice does not earn it;
- no executable/process topology is encoded into capability metadata;
- the Party semantic/dependency regression tests and repository verification contract remain active until deliberately superseded.

## Future-phase relationship

Phase 2–10 are direction-only `NOT_INTRODUCED` governance. Their retired detailed plans remain history; useful anticipation belongs in `FUTURE_PHASE_CARRY_FORWARD.md` and cannot become success criteria merely because it was written earlier.

If a current Phase-0 slice genuinely needs a later responsibility, pull it forward from current facts and qualify it now. Do not create a temporary unsafe substitute or resurrect a retired future subphase file.

## Verification

0A used repository/document evidence because no executable implementation was part of its declared scope.

0B introduced real executable projects. Its repository-owned verification contract is:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

The current tests cover the Party-kind semantic and the initial Parties dependency boundary. They do not imply runtime/provider/persistence/security behavior exists.

The GitHub workflow executed the verification successfully in maintainer-supplied run `34916005915`, job `104213630182`:

`https://github.com/Absolute-Martial/Squiflow/actions/runs/34916005915/job/104213630182`

GitLab uses the same commands from root `.gitlab-ci.yml`. Its current pipelines are quota-blocked before runner start; that does not invalidate the successful GitHub execution and no GitLab runner-success claim is made.

Once a material claim qualifies, its regression guard remains active until the claim is retired or superseded deliberately.

## CI/CD

0B earned dual-host CI because executable verification is real and GitLab hosted capacity is currently unavailable:

```text
GitLab: .gitlab-ci.yml
GitHub: .github/workflows/verify-dotnet.yml
             ↓
       same repository-owned
       restore/build/test commands
```

This is one verification contract with two execution hosts. Do not create separate GitLab/GitHub build semantics, host-specific business tests, or custom tooling trees merely to support mirroring.

Both paths use change filters so a branch/review whose diff contains no executable/build input can skip this verification job. In an already code-changing MR/PR, later documentation-only commits may still retrigger because providers can evaluate the review against its target branch. `global.json` remains the SDK authority.

## Handoff after 0B

Do not start 0C merely because 0B is complete. A host/process/composition boundary is introduced only when a real executable responsibility requires it.

The next implementation work should again start from current product/capability pressure and activate whichever Phase-0 responsibility area is genuinely needed.