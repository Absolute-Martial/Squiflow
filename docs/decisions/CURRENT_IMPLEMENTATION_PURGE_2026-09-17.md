# Current Implementation Purge — 2026-09-17

**Status:** accepted and applied

**Current result:** documentation-only implementation baseline; production/test code is `NOT_INTRODUCED`

## Decision

The `PartyKind` implementation slice and every repository-owned executable/build artifact introduced solely for that slice are purged from the current tree.

The removed implementation was too small to be a useful application baseline and created a misleading sense of progress. Its previous qualification proved only an enum and its lack of dependencies. That evidence did not justify keeping the slice as the starting point for the real application.

## Removed current files

```text
modules/parties/SquiFlow.Parties/Domain/PartyKind.cs
modules/parties/SquiFlow.Parties/SquiFlow.Parties.csproj
tests/unit/SquiFlow.Parties.Tests/Architecture/PartiesDependencyBoundaryTests.cs
tests/unit/SquiFlow.Parties.Tests/Domain/PartyKindTests.cs
tests/unit/SquiFlow.Parties.Tests/SquiFlow.Parties.Tests.csproj
SquiFlow.sln
Directory.Build.props
Directory.Packages.props
global.json
.github/workflows/verify-dotnet.yml
.gitlab-ci.yml
```

Generated `bin/` and `obj/` material under the removed projects was also deleted.

Ignored `bin/` and `obj/` remnants from the superseded pre-reset apps, Foundation, Customers, Payments, CoreApi, and old test projects were also removed so generated artifacts cannot be mistaken for a current implementation.

## Resulting implementation truth

```text
production projects: 0
test projects:       0
executable hosts:    0
solution/build/test contract: absent
active runtime responsibility: none
BLOCKED: none
```

The absence is deliberate. No build/test success is claimed after the purge because there is no current executable contract to run.

## Historical status

Phase 0A remains the current qualified reset baseline. The former 0B Parties qualification is retained in its phase documents and Git history only as historical evidence. Its implementation claims and CI evidence are retired and are not current authority.

This decision does not reject the accepted domain statement that a Party may be a person or an organization. It rejects the deleted enum-only slice as the current application baseline. Any future Party representation must be earned inside a useful real capability journey and may choose a different representation.

## Replacement implementation route

The next implementation starts from a real application responsibility and the source-first review in `docs/review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md`:

1. inspect a proven implementation for the active responsibility;
2. use a focused package, adapt bounded source when its license permits the intended use and distribution, reuse tests/algorithms, or record why the source cannot fit;
3. retain SquiFlow business and authority ownership;
4. introduce the smallest useful production-honest vertical slice;
5. add build/test/CI artifacts only with executable code that needs them.

No previous phase label or deleted project reserves the shape of the replacement implementation.
