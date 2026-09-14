# Test and Validation Instructions

These rules apply below `tests/` in addition to the root instructions.

## Current test model

The current Phase-0 rewrite uses executable specification projects, not only a conventional test runner:

```bash
dotnet run --project tests/SquiFlow.Phase0.Specs/SquiFlow.Phase0.Specs.csproj -c Release
dotnet run --project tests/SquiFlow.Architecture.Specs/SquiFlow.Architecture.Specs.csproj -c Release
```

Do not replace these commands with `dotnet test` unless the repository is deliberately migrated to a test framework and the implementation docs are updated.

## Test qualities

Tests/specs should be:

- deterministic and repeatable;
- independent/order-insensitive;
- self-validating with a clear failure;
- fast when the behavior is pure/in-memory;
- realistic when the behavior depends on framework/provider semantics;
- named after observable behavior/invariant rather than implementation trivia.

FIRST is guidance, not a reason to fake integration behavior as unit tests. “Fast” never overrides the need to use a real DB/framework/process when that is what the claim depends on.

## Layer selection

Use the narrowest layer that can actually prove the claim:

- pure value/invariant → pure spec;
- application orchestration → application spec with controlled ports where appropriate;
- dependency-direction/forbidden package rule → architecture spec;
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

Only add cases relevant to behavior that actually exists.

## Compatibility fixtures

Once a serialized/durable contract or schema version is released/supported:

- preserve representative old fixtures;
- do not regenerate all fixtures from the latest model;
- test old-reader/new-writer and new-reader/old-writer directions where the contract family requires them;
- test destructive contraction only after supported old readers/writers/pending work are demonstrably drained.

## Test data

- Use explicit synthetic data with clear tenant/identity boundaries.
- Do not commit production/customer secrets or data.
- Avoid timestamps/randomness/global environment dependence unless controlled/frozen.
- If randomness is valuable (property/fuzz test), make failures reproducible via a recorded seed/input.

## Architecture specs

Architecture specs are executable architecture documentation. When introducing a new forbidden dependency or structural invariant, add a spec if the rule can be checked mechanically without building a fragile custom framework.

## DO NOT

- Do not assert implementation details that make harmless refactors impossible when the real contract is behavioral.
- Do not make one test depend on another test's execution order/output.
- Do not swallow exceptions and call the test successful because a log was written.
- Do not use sleeps as synchronization when a deterministic signal/timeout can prove the condition.
- Do not claim mocked provider tests prove real provider behavior.
- Do not weaken or delete a failing invariant test merely to get green output; reconcile the architecture/implementation first.
