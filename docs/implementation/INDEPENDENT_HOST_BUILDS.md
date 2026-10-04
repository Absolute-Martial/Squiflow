# Independent host builds and meaningful quality gates

## Declared scope

Each current executable is its own restore/build/test/publish root while the normal
`Application.slnx` gate remains the repository-wide compatibility check. The
repository also owns recurring material coverage, scoped mutation, architecture,
secret and NuGet dependency review. Product version remains v0.0.1.

Current executable inventory remains owned by `README.IMPLEMENTATION.md`.
`eng/hosts.tsv` is the machine-readable mapping from each introduced service host
to its project and the verification projects that own that host's current runtime
or migration graph. `Application.Architecture.Tests` rejects drift between the
manifest, current service executables and the GitHub host matrix.

Use:

```bash
./eng/build-host.sh core-api
./eng/build-host.sh core-api --publish
./eng/verify-host.sh core-api
```

Current aliases are `core-api`, `admin-api`, `admin-bootstrap` and `db-migrator`.
Unsupported/extra arguments fail before dotnet invocation. Workstation, Web UI,
SyncApi and Worker entries are not predeclared and are added only when those
executables actually exist.

## Host verification ownership

`verify-host.sh` restores/builds the selected host, verifies its formatting, then
runs every verification project declared for that host in `eng/hosts.tsv`.

- CoreApi -> `Application.CoreApi.Tests`.
- AdminApi -> `Application.AdminApi.Tests`.
- AdminBootstrap -> `Application.AdminBootstrap.Tests`.
- DbMigrator -> IdentityAccess, Tenancy, Customers, PlatformAdministration through
  AdminBootstrap, and Orders PostgreSQL/migration regressions.

The DbMigrator breadth is deliberate. `MigrationModules.All` applies five module
schemas, so testing only IdentityAccess was not a complete independent migrator
graph. Host verification does not replace the full repository gate and does not
imply deployment qualification.

`build-host.sh --publish` removes the previous host output, publishes from the
host's own project graph, and fails if the expected host assembly is absent.
GitHub preserves each independently published host as its own artifact with
`if-no-files-found: error`.

## Material coverage review

`COLLECT_COVERAGE=1 ./eng/verify.sh` still collects Cobertura data from the normal
repository test run and generates the HTML/Cobertura/TextSummary report. It then
runs `eng/review-coverage.py` against `eng/quality-critical-paths.json`.

The material review intentionally does **not** impose a repository-wide vanity
percentage. Instead it requires named current money/authority paths to:

1. exist in the produced coverage report;
2. have executable lines exercised;
3. exercise at least one branch when the report identifies branch-bearing lines;
4. retain a review artifact that reports covered/uncovered lines and branch counts.

The initial material paths are current Order commitment/pricing state,
host-neutral invoice issue facts, CoreApi tenant authorization, AdminApi platform
model/authority validation, and AdminApi declared-permission enforcement. Add or
remove entries only when current material responsibilities change.

A path can pass this guard while still having uncovered behavior; reviewers use
the retained uncovered-line list to decide whether the gap is material. The guard
prevents a high aggregate number from hiding a completely unexercised critical
file.

## Scoped mutation gate

`eng/mutate-orders.sh` runs Stryker only against `Application.Orders/OrderDraft.cs`
and `eng/review-mutation.py` then isolates mutants inside
`OrderDraftLifecycle.AssessCommit`.

That method is selected because stale revision acceptance or allowing a committed
or abandoned draft to commit again would violate an active money/authority
invariant. The review fails when a targeted mutant is `Survived`, `NoCoverage`,
`Timeout` or `RuntimeError`, and retains a status summary. Nonviable compiler
mutants are reported rather than misrepresented as business evidence.

This is deliberately scoped mutation testing. It is not a claim that all source
files need mutation testing or that mutation score itself is a product KPI.

## Architecture, secret and dependency gates

`Application.Architecture.Tests` continues to enforce actual project/provider
boundaries and now also prevents current host-manifest/CI-matrix drift and requires
all GitHub action references in verification/security workflows to be immutable
40-character revisions.

`.github/workflows/security.yml` keeps the full-history Gitleaks scan and adds a
locked NuGet audit using `eng/audit-dependencies.sh`. The audit explicitly enables
`NuGetAuditMode=all` and treats NU1901-NU1904 vulnerability findings as errors.
Its output is retained for triage. No production/provider credential is supplied
to the verification, mutation, coverage or dependency jobs; workflow permissions
remain `contents: read`. Gitleaks receives only GitHub's job token for the scanning
action and is configured not to publish a potentially sensitive scan artifact.

A vulnerability finding is not silently suppressed to make CI green. Triage must
identify the affected direct/transitive dependency, applicability, remediation or
explicit time-bounded exception in a focused security record before the gate can
be accepted.

## CI failure and artifact semantics

The normal repository job, every current host job, architecture job and scoped
mutation job are independent GitHub jobs. Host matrix `fail-fast` is disabled.
Failure of one host does not erase another host's independently produced result.
The repository compatibility job remains the integrated signal.

Coverage and mutation artifacts contain source/testing diagnostics and are kept
for 14 days. They must not contain production credentials or unrestricted
customer payloads. Current tests use synthetic/test data. Published host artifacts
contain repository configuration templates with empty credential/provider fields;
source secret scanning remains a recurring guard. Distribution signing/provenance
and production release promotion are later release responsibilities, not claimed
by OPS-017.

## Security/static review of the workflows

Current GitHub jobs use read-only repository permissions. Checkout, setup-dotnet
and upload-artifact references are commit pinned. Pull-request jobs receive no
production credentials. There is no `pull_request_target`, privileged container,
release deployment, or secret-bearing integration in these workflows. Provider
integration tests use Testcontainers/local test configuration rather than
production providers.

Requalify when action revisions, workflow permissions/triggers, SDK/tool manifests,
package audit policy, host inventory, host project references, migration registry,
coverage material paths, Stryker version/target, test parallelism or build/verify
entry points change.

## Qualification state

The source-level OPS-017 contract is implemented. Static validation can prove
manifest/path/workflow consistency, shell/Python syntax and deterministic review
logic. Production-honest acceptance still requires inspection of these commands on
the **same exact source**:

```bash
COLLECT_COVERAGE=1 ./eng/verify.sh
./eng/verify-host.sh core-api
./eng/verify-host.sh admin-api
./eng/verify-host.sh admin-bootstrap
./eng/verify-host.sh db-migrator
./eng/build-host.sh core-api --publish
./eng/build-host.sh admin-api --publish
./eng/build-host.sh admin-bootstrap --publish
./eng/build-host.sh db-migrator --publish
./eng/mutate-orders.sh
./eng/audit-dependencies.sh
```

The corresponding GitHub `Verify` and `Security Review` jobs must also be
inspected on the same source before remote-CI success is claimed. Local success
never substitutes for a remote-run claim, and remote CI never substitutes for
provider/deployment properties it does not exercise.

In an environment without the locked NuGet graph and Docker/Testcontainers, leave
OPS-017 acceptance `BLOCKED`; do not replace the missing runs with mocks or older
source evidence.
