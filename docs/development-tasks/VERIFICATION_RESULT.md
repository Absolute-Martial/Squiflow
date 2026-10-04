# Catalog verification result — 2026-10-03

## Assignment-package checks

The final package runs `python3 docs/development-tasks/validate_catalog.py --refresh` and validation for **119 tasks**. Checks cover mandatory metadata/sections, model/status values, contiguous unique IDs, explicit scope, prompts, local owner/task links, required and conditional dependency DAG, all required tasks reaching their release gates, every task routed in the responsibility matrix, generated metadata consistency and per-file SHA-256 membership/content.

The package builder validates the catalog before writing a deterministic ZIP and checks archive member names, CRC and every member SHA-256 against the manifest. The final ZIP's digest is in its `.zip.sha256` sidecar. Package checks prove planning-file integrity, not completion of the assigned development.

## Earlier repository command — superseded by the current run below

Executed the normal `./eng/verify.sh` under `script --quiet --return` on the current dirty checkout at HEAD `5a94ad107084dbe872e8556d9400414735974d43`.

- SDK observed: .NET 10.0.401; Docker server observed: 29.8.1.
- Locked restore and formatting verification completed; Release build reported **0 warnings and 0 errors**.
- The complete command returned **exit 0**, recorded by the transcript's COMMAND_EXIT_CODE.
- The captured test output exposes only the Tenancy.Postgres summary: **19 passed, 0 failed, 0 skipped**. It does not expose a trustworthy whole-solution aggregate, so no aggregate test count or new runtime qualification is asserted here.
- No coverage, remote CI, live managed IdP, browser, deployment, performance or recovery qualification is inferred.

Durable local transcript: `artifacts/verification/development-task-catalog-2026-10-03.log` (outside the catalog archive).

Transcript SHA-256: `b1afed73d6e4cf4e80456714d658d5976029b4cd8598c67fd158055f81ed8797`.

`git diff --check` also returned exit 0. Existing uncommitted source/decision/inventory changes were preserved. Their production state remains under their focused owners and the receiving GATE-001 acceptance, rather than being rewritten by this planning task.

## Fresh complete receiving-environment run

Executed `./eng/verify.sh` with ordinary redirected output and inspected completion: **exit 1**. Locked restore, format verification and Release build passed with zero warnings/errors. Complete output recorded **657 passed, 11 failed, 0 skipped, 668 total across 16 suites**.

All 11 failures belong to IdentityAccess.Postgres and occur during Testcontainers ResourceReaper initialization, caused by `RegexMatchTimeoutException` in DockerImage/MatchImage. They do not qualify those database invariants, nor prove a business or migration defect. The unaffected suites include AdminApi **56/56**, host-neutral Invoices **15/15**, CoreApi **310/310**, and architecture **9/9**.

The normal baseline is **not qualified**. [OPS-022](phase-03-runtime-operations/OPS-022-qualify-parallel-testcontainers-startup.md) is an explicit required correction/evidence task before GATE-001. No rerun, serialization, fake provider or disabled cleanup was used to hide this failure. Feature code and existing owners remain untouched by catalog integration.

Full evidence is [the current log](verification-summary.json); [verification-summary.json](verification-summary.json) records every suite and aggregate. Log SHA-256: `b95ee30711c2b5781f52b3452c5ed296521fe686737031ac5048b54e27003204`.

These results are source-specific local evidence. They do not qualify live ZITADEL, browser/device transport, production deployment, financial issuance/settlement or recovery. The catalog remains usable for assignment, with the observed blocker visible; planning-file validation is distinct from the failed runtime gate.

## Subsequent focused review — full-gate qualification still pending

The original normal-run failure above remains retained. Subsequent [independent ADM-002 review](../production-completion-review/evidence/ADM-002-ADM-003.md) inspected 39/39 focused cases with coverage, three fresh processes with 11/11 hostile cases each, and 23/23 routing/budget cases, all with zero failures/skips. Budget coverage reached 50/50 executable lines and 4/4 emitted branches; the collector emits no exception-filter condition counters, and the review separately states unqualified provider/race/deployment paths. These overlapping focused runs are not summed into a new whole-solution total.

The reviewer then reproduced a separate public-liveness bearer-authentication gap: **exit 1, 0 passed / 1 failed / 0 skipped**. The real native authentication callback is entered outside the protected deadline; controlled fixture evidence proves pipeline dependence, not live ZITADEL latency. The retained [failing transcript](../production-completion-review/evidence/ADM-002-ADM-003.md) activates [ADM-003.public-auth](../production-completion-review/evidence/ADM-002-ADM-003.md) as a receiving-baseline blocker before GATE-001. The narrow deadline correction remains supported; joint AdminApi and baseline acceptance remain pending this correction, independent protection/public-path review and the exact combined normal gate with coverage. [OPS-022's independent root receipt](../production-completion-review/evidence/OPS-022.md) separately records final 24/24 actual fresh-process cases and abort/SIGTERM cleanup; optimized guard injections are harness-validation evidence only. No new full-gate pass is asserted.

## Current-head continuation — 2026-10-04

The exact source now under review is HEAD `18aae19d7441018bc186b902bd5b61beddd5169e`. The tracked [first-sequence evidence](../production-completion-review/evidence/FIRST-SEQUENCE-CURRENT-HEAD.md) supersedes stale blocker wording while preserving the failed historical gate above.

Focused execution observed on this exact HEAD before the shared workspace tunnel failed:

- `Application.AdminApi.Tests`: **87 passed / 0 failed / 0 skipped**;
- `Application.Tenancy.Postgres.Tests`: **19 passed / 0 failed / 0 skipped**;
- `Application.Invoices.Tests`: **25 passed / 0 failed / 0 skipped**;
- the normal `./eng/verify.sh` completed locked restore and a Release build with **0 warnings / 0 errors**; `Application.Architecture.Tests` completed **14/14** and multiple other suites were observed passing.

The final normal-gate exit and full aggregate were not delivered before the workspace tunnel disconnected. Therefore `ADM-001`, `ADM-002`, `OPS-022`, and the deliberately partial `COM-020` scope have current focused evidence. The ADM-003 public-auth correction is implemented in commit `6735370` and focused tests are green, but its required independent post-fix reviewer receipt is absent, so ADM-003 remains evidence-complete and acceptance-pending. **GATE-001 remains `BLOCKED`** until that independent receipt is retained and one complete uncontended normal repository run is inspected on this exact source. At that stage the local continuation container had neither .NET nor Docker; the later SDK retry below supersedes only the SDK-availability part of that environment record.

## GATE-001 local retry from `Squiflow(6).zip` — 2026-10-04

The supplied archive resolves to the same current HEAD `18aae19d7441018bc186b902bd5b61beddd5169e`. Its SHA-256 is `a82e89695dbc3da1628eec60514daef91e2067208ef6986ba53d5dbfc1fddf12`. The upload was described as carrying a new SDK folder, but exhaustive archive inspection found no SDK folder, `dotnet` executable or SDK-sized payload; `.local/` contains only a zero-byte NuGet migration marker. The execution container also has no Docker/Podman daemon or socket.

Because GATE-001 requires the real normal `./eng/verify.sh` including PostgreSQL/OpenFGA Testcontainers, no qualification is inferred from this environment. The exact retry evidence is tracked in [GATE-001 environment retry](../production-completion-review/evidence/GATE-001-ENVIRONMENT-RETRY-2026-10-04.md). GATE-001 remains `BLOCKED` pending one complete normal run on this exact source with .NET 10.0.401 and Docker/Testcontainers.


## GATE-001 SDK/runtime retry — 2026-10-04

The uploaded `Squiflow-offline-sdk(1).zip` was inspected and its .NET 10.0.401 tarball passed the supplied SHA-512 check. The SDK installed successfully and reports Microsoft.NETCore.App / Microsoft.AspNetCore.App 10.0.12.

A first `./eng/verify.sh` attempt was invalidated before restore by this sandbox's injected `PLATFORM=linux/amd64`, which MSBuild interpreted as a solution platform. Re-running with only that host variable removed reached locked restore. The exact repository gate then could not continue because the sandbox cannot resolve/reach `api.nuget.org`, has an empty NuGet package cache, and has no Docker-compatible daemon/socket. The active lockfiles require **123 package/version pairs**.

Tracked details: [SDK/runtime retry receipt](../production-completion-review/evidence/GATE-001-SDK-RETRY-2026-10-04.md) and [locked NuGet manifest](../production-completion-review/evidence/GATE-001-NUGET-MANIFEST.json). No full gate pass is claimed. GATE-001 remains `BLOCKED` pending offline/reachable locked packages, working Docker/Testcontainers, and one complete normal run on the exact current source.
