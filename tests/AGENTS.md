# Test and Validation Instructions

These rules apply below `tests/` in addition to the root instructions.

## Current test model

Two unit-test projects now exist because two real capability boundaries exist:

```text
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
tests/unit/SquiFlow.Payments.Tests/SquiFlow.Payments.Tests.csproj
```

The qualified Parties tests prove only:

- accepted Party structural kinds are exactly `Person` and `Organization`;
- the Parties capability currently has no outward project/package dependency.

The qualified Payments tests prove only:

- the documented Payment statuses are exactly `NotStarted`, `Pending`, `Succeeded`, `Failed`, `OutcomeUnknown`, `PartiallyRefunded`, `Refunded`, and `Reversed`;
- the Payments capability currently has no outward project/package dependency.

Those Payments claims are `PRODUCTION_HONEST`, with successful executable evidence recorded in `docs/implementation/phases/phase-0/0E_STATUS.md`.

Neither test project proves persistence, API, runtime, authorization, sync, money/rounding, transition rules, settlement, Customer/Account semantics, or shared Foundation behavior.

Do not restore removed Phase-0 spec projects by memory. Add another test project only when a distinct real verification responsibility earns it.

## Test qualities

Tests/specs should be:

- deterministic and repeatable;
- independent/order-insensitive;
- self-validating with a clear failure;
- fast when the behavior is pure/in-memory;
- realistic when behavior depends on framework/provider/process semantics;
- named after observable behavior/invariant rather than implementation trivia.

FIRST is guidance, not a reason to fake integration behavior as unit tests. “Fast” never overrides the need to use a real DB/framework/process when that is what the claim depends on.

## Layer selection

Use the narrowest layer that can actually prove the claim:

- pure value/invariant → pure test/spec;
- application orchestration → application test with controlled ports where appropriate;
- dependency-direction/forbidden package rule → architecture test/spec, which may live in an existing test project when the rule does not justify another project;
- ASP.NET middleware/session/routing → real test host/pipeline when introduced;
- SQLite WAL/locking/encryption/migration → real SQLite implementation;
- PostgreSQL transaction/RLS/pool behavior → real PostgreSQL;
- process supervision/restart → real process boundary;
- restore/recovery → actual restore drill;
- external/provider contract → contract/integration test appropriate to the provider.

Do not mock away the property being tested.

## Required negative coverage

For protected/durable/versioned behavior, include negative cases such as:

- cross-tenant access;
- missing/revoked authorization;
- duplicate semantic operation;
- same idempotency key with changed intent;
- expected-version conflict;
- response loss after commit;
- process termination mid-operation;
- dependency outage/timeout;
- unsupported contract/schema version;
- disk-full/low-space where relevant;
- malformed/oversized input;
- secret/sensitive-data leakage where observable.

Only add cases relevant to behavior that actually exists. None of those runtime/durable cases is implied by the current Party-kind or Payment-status slices.

## Compatibility fixtures

Once a serialized/durable contract or schema version is released/supported:

- preserve representative old fixtures;
- do not regenerate all fixtures from the latest model;
- test old-reader/new-writer and new-reader/old-writer directions where that contract family requires them;
- test destructive contraction only after supported old readers/writers/pending work are demonstrably drained.

`PartyKind` and `PaymentStatus` are currently internal capability semantics. No serialized numeric compatibility contract is claimed; do not infer a wire/storage contract from enum ordinal values.

## Test data

- Use explicit synthetic data with clear tenant/identity boundaries.
- Do not commit production/customer secrets or data.
- Avoid uncontrolled timestamps/randomness/global environment dependence.
- If randomness is valuable, make failures reproducible via a recorded seed/input.

## Architecture verification

Architecture tests/specs are executable architecture documentation for boundaries that actually exist.

The current Parties and Payments dependency tests deliberately reject all outward `ProjectReference` and `PackageReference` entries in their production capability projects. If a future real dependency is earned, do not merely weaken a test: update the active scope/owner and replace the old guard with one that protects the newly accepted dependency direction.

Do not create a shared architecture-test framework merely because two small tests currently look similar. Extract shared test infrastructure only when maintenance/change pressure makes the shared ownership clearer than the duplication.

## Current executable commands

```text
dotnet restore SquiFlow.sln
dotnet build SquiFlow.sln -c Release --no-restore
dotnet test SquiFlow.sln -c Release --no-build
```

Qualified 0E has successful GitHub execution evidence for the current four-project solution: run `34922277237`, job `104232827231`.

GitLab pipeline `#196` for the first 0E code commit failed before start with `ci_quota_exceeded`, `runner = null`; no `dotnet` command executed. This remains historical infrastructure-capacity context, not a code/test failure and not a claim that current executable evidence is missing.

Do not generalize the successful run beyond the exact qualified solution/claims. A later change to executable/build inputs requires fresh evidence.

## DO NOT

- Do not create test folders/projects solely to mirror production folders.
- Do not restore old tests without checking whether their architecture still matches current decisions.
- Do not assert implementation details that make harmless refactors impossible when the real contract is behavioral.
- Do not make one test depend on another test's execution order/output.
- Do not swallow exceptions and call the test successful because a log was written.
- Do not use sleeps as synchronization when a deterministic signal/timeout can prove the condition.
- Do not claim mocked provider tests prove real provider behavior.
- Do not weaken/delete a failing invariant test merely to get green output; reconcile architecture/implementation first.
