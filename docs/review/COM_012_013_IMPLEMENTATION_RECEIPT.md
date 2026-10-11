# COM-012 / COM-013 implementation receiving receipt

**State:** locally `PRODUCTION_HONEST`, `BLOCKED = none` for the accepted bounded COM-012/minimal COM-013 scope. The unchanged normal repository gate and all three affected-host build/publish checks passed on identical frozen source. The owner accepted [the contract](COM_012_013_DECISION_PROPOSAL.md) on 2026-10-09. [Order approval and admission](../implementation/ORDER_APPROVAL_AND_ADMISSION.md) owns the bounded runtime meaning.

## Source and scope

The receiving baseline is `f32576f7d86b2166e381a10b80df61865addf240`, branch `t3code/continue-commercial-work`, worktree `/home/lets-smile/.t3/worktrees/Squiflow/t3code-66ed0681`. The incoming worktree was clean before this continuation. Product remains `v0.0.1`; the existing 25 production projects, 23 test projects and four hosts remain unchanged.

The slice introduces typed v2 policy/profile facts, four independent approval permissions, immutable exact-subject approval requests and history, bounded protected continuation/recovery APIs, and the minimal COM-013 consumer that denies commitment without the required exact approval and freezes its evidence atomically. It preserves historical v1 policy and receipt interpretation, independent quotation customer acceptance, accepted financial facts and the original quotation conversion snapshot. Existing authorization/profile prerequisites retain their separate qualification scope.

Submission, decision, recovery and commitment coordinate through the existing Order row lock. Approval does not edit or commit an Order. The requester and exact revision author cannot normally approve; current Owner recovery only reassigns or cancels. Requester cancellation uses current submit authority even after view rights are revoked. Discovery never approves or silently supersedes work.

## Receiving corrections and permanent guards

- Actual HTTP/PostgreSQL/OpenFGA submission exposed timestamp and decimal-representation differences between relational facts and JSON receipts. New adapter timestamps use database precision; exact subject comparison uses recursive JSON value equality. Native precision/restart and unit decimal/nested-candidate regressions guard these invariants.
- The OpenAPI exporter rejected provider/domain subject types with Guid defaults. Explicit protected wire DTOs reuse the existing Order response contract, and the OpenAPI regression requires a successful document with security and declared failure responses.
- Native receiving corrected a PL/pgSQL conditional syntax error and the typed non-mutating rejection for publication from a legacy policy. Versioned malformed-fact and legacy no-rewrite fixtures guard profile compatibility.
- Request-body bounds now produce the same safe oversized-body result for declared and chunked input. History limits are enforced in both HTTP and the owning store.
- Native guards cover tenant filtering and cross-scope denial, the exact runtime role privilege boundary, concurrent approval/Order revision, request-decision races, receipt/evidence failure rollback, restart/replay, and distinct caller-cancellation versus named database-backend-loss recovery. The loss tests observe the actual PostgreSQL lock; they do not infer entry from a sleep.
- The native HTTP journey checks central membership, independent pinned-model OpenFGA permissions, current initial Owner recovery with recently signed test JWT claims, self-assignment denial, approval admission guidance, immutable price commitment, and decision replay denial after approver suspension.

The following suites are the recurring guards: `Application.Orders.Tests`, `Application.Profiles.Tests`, `Application.Tenancy.Tests`, `Application.Orders.Postgres.Tests`, `Application.Profiles.Postgres.Tests`, `Application.Quotations.Postgres.Tests`, `Application.CoreApi.Tests`, and the unchanged repository gate. Database privilege tests apply the checked-in deployment script to an actual nonowner role. Identity signing and account binding in the HTTP fixture are synthetic; central membership, PostgreSQL persistence and OpenFGA checks are native.

## Verification

Commands use .NET SDK `10.0.401`, `DOTNET_CLI_HOME=/tmp/application-commercial-dotnet` and `NUGET_HTTP_CACHE_PATH=/tmp/application-commercial-nuget-http-cache`. No checks run concurrently.

Inspected focused receiving checks:

| Check | Inspected result |
|---|---|
| Release solution build | Zero warnings and errors. |
| Orders unit suite | 132 passed; zero failed/skipped. |
| Profiles unit suite | 28 passed; zero failed/skipped. |
| Tenancy unit suite | 37 passed; zero failed/skipped. |
| CoreApi approval filter | 24 passed; zero failed/skipped before the added native Owner-recovery assertions. |
| Updated native CoreApi approval journey | One passed; zero failed/skipped, including Owner recovery. |
| Legacy v1 profile compatibility regression | One passed after correcting typed failure handling; no stored legacy fact/receipt rewrite. |
| Approval cancellation/backend-loss regressions | Two passed, with distinct exception assertions and successful same-key retry. |

Earlier failed receiving runs remain in `/tmp/application-com012-continuation-20261009` alongside corrected runs; a failed run is not qualification. The quotation receiving method selected two actual cases (approval off/on); both passed and preserved accepted prices/origin and the original conversion snapshot. Earlier file-name filters selected no tests and are explicitly not evidence. The final confidentiality regression checks absence of the full `Subject` property without rejecting the legitimate `SubjectStale` summary flag. The full-suite result below comes from one exact normal run, never a sum of partial runs.

## Exact final qualification

| Exact command | Inspected result |
|---|---|
| `./eng/verify.sh` | Exit 0; locked restore, format verification, Release build; **1,282/1,282 across all 23 projects**, zero failures/skips, zero warnings/errors. Script-reported duration 2,215 seconds. |
| `./eng/build-host.sh core-api --publish` | Exit 0, zero warnings/errors; expected host assembly published. |
| `./eng/build-host.sh admin-api --publish` | Exit 0, zero warnings/errors; expected host assembly published. |
| `./eng/build-host.sh db-migrator --publish` | Exit 0, zero warnings/errors; expected host assembly published. |

The exact gate includes CoreApi 527, Orders.Postgres 111, Profiles.Postgres 25 and Quotations.Postgres 37 passing tests. All 1,453 source files were unchanged throughout gate and publication. The runtime source manifest SHA-256 is `79a4f38aedb0a1414849bcbaa83d29eee80a1f906cb5a916511427d9cea98934`. Qualification-only documentation updates follow this run; no runtime/build/test/deployment source changes afterward are permitted by this receipt.

| Retained log | SHA-256 |
|---|---|
| `normal-gate.log` | `7e3f5193be4336adbaa0ff995d90285b017537db5fb75bddc8e330e565fbc314` |
| `publish-core-api.log` | `7df23cd62b3e70ff2271215beebcae0f3a84197e27043701e2909b7865c17311` |
| `publish-admin-api.log` | `c7f2c8d4b7e2fe71b2406349a65c795b992ed29ac91b0505328ea6d5375fc4d4` |
| `publish-db-migrator.log` | `556113db87d2b2621a23780dcbdaa7e78be4ba2ac7b89611d4935dc28b480e4d` |

The durable local handoff is `artifacts/com012-013-20261009/`: a source-only ZIP, complete patch including new files, changed-file/hash manifests, frozen gate manifests and receiving logs. Git metadata is outside the writable worktree boundary, so this receiving source is delivered uncommitted; no push or PR was performed. The source-only artifact and baseline patch must remain together with this receipt.

After qualification-only documentation updates, the architecture suite passed 16/16 again. The six owned task-catalog hashes are current. The full catalog validator still reports 15 metadata errors, including eight inherited hash mismatches; its output is byte-identical to the validator run on an isolated archive of baseline `f32576f7d86b2166e381a10b80df61865addf240`. These pre-existing catalog issues were preserved, and catalog-wide reconciliation is not claimed by this slice.

## Deployment, requalification and non-claims

Deploy ordered migrations, apply the checked-in CoreApi runtime grants, publish/pin the updated immutable OpenFGA model, explicitly delegate approval rights, and publish/activate the required profile for new work. There is no automatic Owner/Staff grant expansion or approval. Supported old profiles preserve their pinned meaning; unknown/contradictory authority fails closed. Destructive removal of retained schema/evidence is unsupported.

Requalify on policy/profile/receipt version meaning, exact subject/provenance comparison, eligibility and self-approval, permission/model or Owner recovery changes, transaction/lock ownership, migrations/grants, resource/disclosure bounds, and quotation/commercial compatibility.

Remote CI, deployment, live ZITADEL/storage provider qualification and full-product qualification remain separate. Fulfillment, stock, purchasing, invoices/payments, notifications, dynamic forms, runtime implementation switching and general OPS-013 audit are not introduced by this slice.
