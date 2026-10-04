# First-sequence current-head qualification status — 2026-10-04

## Baseline

- Git HEAD: `18aae19d7441018bc186b902bd5b61beddd5169e`.
- Product version remains `v0.0.1`.
- The source ZIP includes Git metadata. Runtime verification artifacts under ignored `artifacts/` paths are not part of the archive; tracked summaries in this directory preserve the inspected outcomes without pretending to be raw logs.

## Unit dispositions

| Unit | Disposition | Current evidence |
|---|---|---|
| BAS-001 | `PRODUCTION_HONEST` for source/evidence inventory | Current HEAD, dirty paths and tracked review hashes are recorded in the production-completion review package. |
| ADM-001 | `PRODUCTION_HONEST` narrow existing scope | Current AdminApi 87/87 and Tenancy.Postgres 19/19; real PostgreSQL/OpenFGA fixtures. |
| ADM-002 | `PRODUCTION_HONEST` narrow deadline scope | Independent focused/routing/cancellation review plus current AdminApi 87/87. |
| ADM-003 activated public-auth slice | `BLOCKED` / acceptance pending | Commit `6735370` implements the correction and focused public/protected bearer tests are green, but the required independent post-fix reviewer receipt is not retained in this repository. |
| OPS-022 | Focused evidence complete; GATE handoff retention pending | Fresh-process/abort/SIGTERM cleanup outcomes are summarized in tracked evidence, but the gate-named independent `artifacts/verification/ops-022/root-final-review.json` receipt is absent from this source archive. |
| COM-020 | `PRODUCTION_HONEST` deliberately partial neutral scope | Current Invoices 25/25 plus Architecture 14/14 observed. Durable invoice runtime remains not introduced. |
| GATE-001 | `BLOCKED` / acceptance pending | The gate-required ADM-001/ADM-002/OPS-022 handoffs are not retained at their historical artifact paths, the independent post-fix ADM-003 receipt is absent, and the complete normal repository gate has not produced an inspectable final exit/aggregate on this exact source. The later local retry has .NET 10.0.401 but lacks the locked NuGet cache and Docker-compatible daemon/socket. |

## Final gate requirement

Do not infer GATE-001 acceptance from the focused results. Before dispatching the final gate, recover/retain the gate-required ADM-001, ADM-002 and OPS-022 handoffs and complete the independent post-fix ADM-003 review in [the tracked review assignment](../assignments/ADM-003-INDEPENDENT-REVIEW.md). Then run one uncontended normal `./eng/verify.sh` on this exact source in an environment with .NET 10 and Docker/Testcontainers, with final exit, every suite summary, failures and skips inspected. Only after all prerequisite receipts are retained and that normal gate passes may GATE-001 move to `PRODUCTION_HONEST`.

A subsequent retry using `Squiflow(6).zip` confirmed the same HEAD, but that archive did not contain the described SDK payload. See [GATE-001 environment retry](GATE-001-ENVIRONMENT-RETRY-2026-10-04.md).

A later `Squiflow-offline-sdk(1).zip` supplied the exact .NET 10.0.401 SDK and its checksum validated. The gate then reached locked restore after removing this sandbox's injected `PLATFORM=linux/amd64`, but the container has no reachable NuGet feed or populated package cache and no Docker-compatible daemon/socket. See [GATE-001 SDK/runtime retry](GATE-001-SDK-RETRY-2026-10-04.md) and the exact [offline NuGet manifest](GATE-001-NUGET-MANIFEST.json). These environment failures remain evidence pending; they are not converted into a source failure or a false gate pass.
