# Phase 0E Status — Second Capability / Payments Slice

**Status:** IN PROGRESS  
**Baseline:** `main` after merged and qualified Phase 0B MR !67  
**Gate owner:** `0E_ACTIVE_CAPABILITY_AND_TRACK_DEVELOPMENT.md`

## Why 0E is the next earned work area

Phase 0C is intentionally not activated yet. The repository still has no real executable responsibility, so creating Workstation/CoreApi/Web/Guard merely because 0C is numerically next would violate the earned-boundary rules in the root `AGENTS.md` and the 0C owner.

Phase 0E explicitly allows real capability work to advance before host/process work when current semantics justify it. The accepted payment-status vocabulary in `docs/domain/BUSINESS_MODEL.md` provides such a slice without hardening the discovery-sensitive Order/Job/Sale or Customer/Account distinctions.

## Production intent for the active slice

A second capability can be introduced from already-accepted business meaning without changing the dependency direction established by 0B, without coupling capabilities together, and without extracting shared Foundation merely because two modules now exist.

## Current scope states

| Claim / responsibility | State | Exact current scope | Evidence / guard |
|---|---|---|---|
| `0E-PAYMENT-STATUS` | `BLOCKED` pending executable evidence | `SquiFlow.Payments.Domain.PaymentStatus` contains exactly the documented statuses `NotStarted`, `Pending`, `Succeeded`, `Failed`, `OutcomeUnknown`, `PartiallyRefunded`, `Refunded`, and `Reversed`. No transition graph or monetary/settlement semantics is claimed. | `PaymentStatusTests.Accepted_statuses_match_the_documented_payment_semantics`; source review against `BUSINESS_MODEL.md`. |
| `0E-PAYMENTS-DEPENDENCY-BOUNDARY` | `BLOCKED` pending executable evidence | The Payments capability is host/provider neutral and currently has no outward project or package dependency. | `PaymentsDependencyBoundaryTests.Capability_has_no_outward_project_or_package_dependencies`. |
| `0E-CROSS-CAPABILITY-DIRECTION` | `BLOCKED` pending executable evidence | Parties and Payments remain independently owned capability projects; neither references the other and neither depends on Foundation. | Existing Parties dependency guard plus Payments dependency guard; solution/project inventory. |
| `0E-SHARED-FOUNDATION` | `NOT_INTRODUCED` | No shared product-wide primitive has yet appeared merely because a second capability exists. | Current capability semantics are distinct; extraction remains trigger-driven. |
| `0C-HOST-COMPOSITION` | `NOT_INTRODUCED` | No executable host has a real current application responsibility yet. | 0C owner + repository inventory. |
| `0E-PAYMENT-IDENTITY-AMOUNT-TRANSITIONS` | `NOT_INTRODUCED` | No payment ID encoding, amount/currency/rounding, transition rules, retries/idempotency, settlement, persistence, API, authorization, or reconciliation behavior is introduced. | Focused domain/open-decision owners remain authoritative when a real payment operation requires them. |

## Required executable evidence

The same repository-owned contract remains authoritative:

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

Because this slice changes C#, projects, tests, and the solution, the successful 0B GitHub run does not qualify these new claims. A new successful execution is required before the introduced 0E claims can become `PRODUCTION_HONEST`.

## Permanent-regression direction

If the slice qualifies:

- the Payment-status semantic test remains a normal regression guard;
- the Payments no-outward-dependency test remains until a real dependency is explicitly earned and requalified;
- the existing Parties guards remain unchanged;
- introducing a shared primitive requires naming the real duplicated product-wide semantic rather than extracting because there are now two modules;
- adding an executable remains a separate 0C responsibility with its own lifecycle/failure evidence.

## Gate state

Phase 0E is **not complete**. `BLOCKED` is non-empty until executable evidence passes, and the wider 0E exit gate still requires real multi-capability development evidence rather than a phase-only checklist.
