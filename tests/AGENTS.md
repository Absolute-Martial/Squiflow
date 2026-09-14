# Test and Validation Instructions

These rules apply below `tests/` in addition to the root instructions.

## Current test model

The principles-first reset currently has **no executable test/spec projects**. The `tests/` directory is an ownership/instruction location only until implementation work reintroduces verification projects.

Do not restore the removed Phase-0 spec projects by memory merely because historical docs or Git history mention them. The first new implementation slice should introduce the narrowest useful verification needed for that real responsibility.

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
- dependency-direction/forbidden package rule → architecture test/spec;
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
- test old-reader/new-writer and new-reader/old-writer directions where that contract family requires them;
- test destructive contraction only after supported old readers/writers/pending work are demonstrably drained.

## Test data

- Use explicit synthetic data with clear tenant/identity boundaries.
- Do not commit production/customer secrets or data.
- Avoid uncontrolled timestamps/randomness/global environment dependence.
- If randomness is valuable, make failures reproducible via a recorded seed/input.

## Architecture verification

Architecture tests/specs should become executable architecture documentation as real code boundaries return. When introducing a new forbidden dependency or structural invariant, add mechanical verification where it provides durable value without creating a fragile custom framework.

## DO NOT

- Do not create test folders/projects solely to mirror production folders.
- Do not restore old tests without checking whether their architecture still matches current decisions.
- Do not assert implementation details that make harmless refactors impossible when the real contract is behavioral.
- Do not make one test depend on another test's execution order/output.
- Do not swallow exceptions and call the test successful because a log was written.
- Do not use sleeps as synchronization when a deterministic signal/timeout can prove the condition.
- Do not claim mocked provider tests prove real provider behavior.
- Do not weaken/delete a failing invariant test merely to get green output; reconcile architecture/implementation first.
