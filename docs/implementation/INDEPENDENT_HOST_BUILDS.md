# Independent host builds in the monorepo

## Declared scope

Each executable is its own build and publication root. Keeping all projects in
`Application.slnx` provides repository-wide compatibility checks; it does not make
that solution a prerequisite for building or publishing CoreApi. Product version
remains v0.0.1. Current executable inventory belongs to `README.IMPLEMENTATION.md`.

Use the project graph of the selected host:

```bash
./eng/build-host.sh core-api
./eng/build-host.sh core-api --publish
./eng/verify-host.sh core-api
```

Other existing targets are `db-migrator`, `admin-bootstrap` and `admin-api`.
`CONFIGURATION` defaults to Release. Restore is locked, and publication goes to
ignored `artifacts/publish/<host>/`. Unsupported or extra arguments fail before
invoking dotnet. Workstation, Web UI, SyncApi and Worker targets will be added only
when those executables exist; this change introduces none of them.

`verify-host.sh` builds the chosen host, checks formatting and runs its own host
regression project. The DbMigrator target uses the existing migration
registry/lifecycle regression project. These checks cover their actual referenced
project graphs; they are not replacements for all module/provider tests or the
normal full `./eng/verify.sh` compatibility gate.

## Dependency and failure boundaries

CoreApi must not reference Workstation, AdminApi, Web UI or another executable.
The same restriction applies in reverse. A host-owned UI/provider library also
cannot be used as a hidden dependency of another host or a host-neutral module.
Repository-wide source/build-identity policy runs in architecture tests, not in
CoreApi host tests; CoreApi still checks its own assembly and runtime branding.
Application contracts and genuinely host-neutral capability libraries may be
shared. Their compile errors affect the hosts that actually reference them; this
is required dependency checking, not an unrelated-host failure.

Workstation will communicate with the server through an owned external API/sync
contract. A network connection does not require a project reference to the server
executable. Runtime protocol compatibility, versioning, cancellation and failure
handling still need qualification when the transport is introduced. Separate
builds alone prove none of those runtime guarantees.

The GitHub workflow has independently running host jobs, `fail-fast: false`,
separate publish artifacts and no dependency on another host job. A failing
Workstation job, when introduced, must not cancel or become a prerequisite of a
CoreApi build/publish job. The separate repository-wide compatibility job remains
an overall integration signal; an error there cannot erase a successful host
artifact. Deployment pipelines and required branch checks are not introduced here.

## Evidence and regression guards

`CurrentProjectsRespectDependencyAndProviderBoundaries` checks the actual project
references. `ApiCannotDependOnWorkstationOrItsHostComponents` proves both direct
executable and hidden UI-component references are rejected; a WinExe desktop
project is a valid independent host. Project inventory scans include active
projects under apps/foundation as well as modules/services/tests.

Requalify on new hosts, project references, shared MSBuild imports, SDK/package
policy changes, build/verify entry points or workflow dependencies. A broken
unreferenced desktop source must not enter the CoreApi compilation graph. Real
host build isolation evidence is recorded after inspecting the corresponding run;
remote workflow execution and production deployment are separate claims.

The local 2026-10-03 isolation experiment compiled and published CoreApi in an
isolated source copy while an unreferenced desktop probe in that same copy
failed with the intentional C# compiler error CS1029. CoreApi had zero build
warnings/errors. This is build isolation evidence, not a Workstation runtime:
there is no introduced Workstation executable. Entry-point argument checks also
rejected missing, unimplemented and unsupported targets before running dotnet.

## Local qualification

The final normal parallel `./eng/verify.sh` run on 2026-10-03 passed locked
restore, formatting verification, Release build and all 568 tests across
15 test projects, with zero warnings, errors, failures or skips. The nine
architecture tests include the executable/UI-component separation and relocated
repository-wide identity guard; all 309 CoreApi host tests passed after that move.
The earlier prequalification run stopped on CA1861 in the new test; a shared
readonly project-area list fixed it before this successful normal run.

The independent build/verification scope is `PRODUCTION_HONEST` locally, with
`BLOCKED = none` for this scope. Remote GitHub job execution, deployment branch
policy and a Workstation runtime are not claimed. Logs are retained locally under
ignored `artifacts/verification/2026-10-03-independent-hosts/`.
