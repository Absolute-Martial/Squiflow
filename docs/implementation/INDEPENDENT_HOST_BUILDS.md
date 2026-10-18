# Independent host builds and meaningful quality gates

## Declared scope

Each current executable is its own restore/build/test/publish root while the normal
`Application.slnx` gate remains the repository-wide compatibility check. The
repository also owns recurring material coverage, scoped mutation, architecture,
secret and NuGet dependency review. Product version remains v0.0.1.

The public entry hosts are included as `web` and `workstation` in the manifest and
independent CI matrix. Their bounded responsibility and supported-platform limits
are owned by [public application entry](PUBLIC_APPLICATION_ENTRY.md). The shared
HTTP client's test root is included in both host graphs.

The bounded non-Workstation completion uses `eng/verify-host-ci.sh` for Web,
CoreApi, AdminApi, AdminBootstrap and DbMigrator, and adds actual Web browser CI.
The existing Workstation entry and its verification behavior are preserved; this
completion does not qualify Workstation or introduce another host.

Current executable inventory remains owned by `README.IMPLEMENTATION.md`.
`eng/hosts.tsv` is the machine-readable mapping from each introduced executable
to its project and the verification projects that own that host's current runtime
or migration graph. `Application.Architecture.Tests` rejects drift between the
manifest, current executables and the GitHub host matrix.

Use:

```bash
./eng/build-host.sh core-api
./eng/build-host.sh core-api --publish
./eng/verify-host.sh core-api
./eng/verify-host-ci.sh core-api
./eng/verify-host-ci.sh web
```

The existing build/verify aliases are the entries in `eng/hosts.tsv`.
`verify-host-ci.sh` accepts exactly one of `core-api`, `admin-api`,
`admin-bootstrap`, `db-migrator` or `web`. Unsupported/extra arguments fail before
dotnet invocation. SyncApi and Worker entries remain absent.

## Host verification ownership

`verify-host.sh` restores/builds the selected host, verifies its formatting, then
runs every verification project declared for that host in `eng/hosts.tsv`.

- CoreApi -> `Application.CoreApi.Tests`.
- AdminApi -> `Application.AdminApi.Tests`.
- AdminBootstrap -> `Application.AdminBootstrap.Tests`.
- Web -> `Application.Web.Tests` and the shared `Application.ApplicationEntry.Tests`.
- DbMigrator -> IdentityAccess, Tenancy, Customers, PlatformAdministration through
  AdminBootstrap, Orders, Fulfillment, Purchasing and Invoices
  PostgreSQL/migration regressions, as declared in the manifest.

The DbMigrator breadth is deliberate. `MigrationModules.All` is the current
registered schema authority; generic migration-registry/lifecycle tests cover
that complete registry, while the declared native owner suites cover their
selected persistence invariants. Adding Fulfillment also adds its native suite
to this host guard. Testing only IdentityAccess would omit those owner checks. Host verification does not replace the full repository gate and does not
imply deployment qualification.

`build-host.sh --publish` removes the previous host output, publishes from the
host's own project graph, and fails if the expected host assembly is absent.
GitHub preserves each independently published host as its own artifact with
`if-no-files-found: error`.

For the five non-Workstation hosts, `verify-host-ci.sh` invokes the existing host
verification and publication scripts, retains SDK information and their combined
log, and requires nonempty host `.dll`, `.deps.json` and `.runtimeconfig.json`
outputs. It removes the previous host publication before verification and writes
`SHA256SUMS` only after these checks pass. The same existing host matrix invokes
this entry point in a fresh checkout per host, with `fail-fast: false`; no solution
restore/build substitutes for the selected graph. Local runs may reuse ordinary
NuGet and incremental-build caches. Checksums identify the publication; they do
not establish signing, provenance attestation or deployment compatibility.

## Actual Web browser CI

`.github/workflows/web-browser.yml` builds only the actual Web graph and invokes
`eng/verify-web-browser.sh`. It provisions the already-used external Playwright
1.64.0 (Apache-2.0) and axe-core 4.11.0 (MPL-2.0) under the runner temporary
directory, using exact package versions, disabled npm lifecycle scripts, and the
Chromium revision selected by that Playwright version. It adds no runtime or
repository npm project. The runner's Node.js must be at least version 20; actual
Node/Playwright/axe and browser versions are recorded. The Linux runner is a test
environment, not a supported deployment declaration.

The wrapper requires built Release Web runtime files and the existing external
tools. It runs `eng/test-signed-in-entry-browser.cjs`, which launches the actual
Web process and Chromium against its controlled local HTTPS issuer/API fixture.
It covers the existing login/PKCE, tenant selection/switch/denial, empty/disabled
account, CSRF/session custody, logout/restart, readiness/failure and responsive
accessibility journeys. The [signed-in entry owner](SIGNED_IN_APPLICATION_ENTRY.md)
defines their exact meaning and limits. No live ZITADEL, OpenFGA, PostgreSQL or
complete authenticated business workflow is qualified by the browser fixture.

The wrapper also runs the bounded business, quotation/approval and operations
journeys in separate evidence directories, and validates all four result contracts
before creating the combined checksum receipt. Each journey has a ten-minute process
budget plus a ten-second kill budget. `APPLICATION_ENTRY_BROWSER_EXECUTABLE` may select
an already installed absolute browser executable for local runs; its actual version
is recorded. This avoids a browser download without claiming the local browser is
the CI-pinned Chromium revision.
Old evidence is removed before preflight. Success requires a fresh final journey
JSON with the browser version, screenshots and executed accessibility reports
without reported violations. Incomplete axe rules remain manual-review evidence,
not conformance. Successful evidence receives `SHA256SUMS`. CI retains diagnostics
after failure, including partial screenshots, for 14 days; missing files fail the
upload. It collects no HAR, browser storage, TLS keys or production credentials.
The generated local test private material stays outside the artifact directory.

Existing local installations can run the same guard after building Web:

```bash
./eng/build-host.sh web
APPLICATION_ENTRY_PLAYWRIGHT_PATH=/tmp/application-entry-browser/node_modules/playwright \
APPLICATION_ENTRY_AXE_PATH=/tmp/application-entry-browser/node_modules/axe-core/axe.min.js \
PLAYWRIGHT_BROWSERS_PATH=/tmp/application-entry-browsers \
./eng/verify-web-browser.sh
```

`python3 eng/test-quality-verification.py` uses fixture dotnet/browser commands
to test graph selection, exit propagation, required publication/evidence files,
stale-output rejection and workflow guards without product builds. The existing
architecture CI job runs it. These checks prove orchestration only.

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

The repository job has a 60-minute budget for the complete coverage-enabled
gate; host and mutation jobs retain their 30-minute budgets. On 2026-10-11,
GitHub run `38138736532` reached the former 30-minute limit while the normal
PostgreSQL suites were progressing, after the completed suites had passed.
The expanded commercial test graph needs room to finish coverage collection and
review. This changes only the job deadline: every test, coverage review and
failure condition remains required. Requalify the budget when the test graph or
runner changes.

Coverage and mutation artifacts contain source/testing diagnostics and are kept
for 14 days. They must not contain production credentials or unrestricted
customer payloads. Current tests use synthetic/test data. Published host artifacts
contain repository configuration templates with empty credential/provider fields;
source secret scanning remains a recurring guard. Distribution signing/provenance
and production release promotion are later release responsibilities, not claimed
by OPS-017.

## Security/static review of the workflows

Verification/security jobs use read-only repository permissions; the existing
CodeQL job separately has its declared `security-events: write` result-upload
permission. Checkout, setup-dotnet
and upload-artifact references are commit pinned. Pull-request jobs receive no
production credentials. There is no `pull_request_target`, privileged container,
release deployment, or secret-bearing integration in these workflows. Provider
integration tests use Testcontainers/local test configuration rather than
production providers.

The changed host checkout and browser checkout disable persisted Git credentials.
The browser job receives no production/provider secrets and uploads only its
dedicated synthetic evidence directory. Existing Gitleaks, locked NuGet audit,
CodeQL, Coverlet/ReportGenerator and scoped Stryker gates remain in place. Wiring
or static inspection of these tools is not a passed security/dependency/mutation
result.

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
./eng/verify-host-ci.sh core-api
./eng/verify-host-ci.sh admin-api
./eng/verify-host-ci.sh admin-bootstrap
./eng/verify-host-ci.sh db-migrator
./eng/verify-host-ci.sh web
./eng/verify-web-browser.sh # with the external-tool environment shown above
./eng/mutate-orders.sh
./eng/audit-dependencies.sh
```

The corresponding GitHub `Verify`, `Web browser`, `Security Review` and `CodeQL` jobs must also be
inspected on the same source before remote-CI success is claimed. Local success
never substitutes for a remote-run claim, and remote CI never substitutes for
provider/deployment properties it does not exercise.

In an environment without the locked NuGet graph and Docker/Testcontainers, leave
OPS-017 acceptance `BLOCKED`; do not replace the missing runs with mocks or older
source evidence.

For the bounded 2026-10-15 source-only completion, dynamic qualification is
`BLOCKED`: the integrator has a normal gate running and has not released the
build/Docker hold. No .NET restore/build/test/publish, browser launch, mutation,
dependency/secret/static-security scan or remote CI was run for that patch.
The [completion receipt](../review/OPS_017_NON_WORKSTATION_COMPLETION.md) records
the exact static/selfcheck results and integration decisions.

The Web browser runner now executes both the signed-in entry guard and
`eng/test-business-web.cjs` against the built Release Web host, in separate
`artifacts/web-browser/entry` and `business` directories. Both final result files,
nonempty screenshots and actual axe reports are required before checksums are
written. Entry success cannot mask a failed business guard. Controlled issuer/API
fixtures remain distinct from live providers and native provider evidence.
