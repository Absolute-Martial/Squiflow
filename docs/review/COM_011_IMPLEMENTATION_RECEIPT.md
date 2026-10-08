# COM-011 and ADM-018–021 implementation and local qualification receipt

Qualification: 2026-10-08, completed at 15:58:23 UTC. Product: **v0.0.1**. Baseline: `a6a4c042f8b3da63c3a569761ee7a19049db9cb9` on `t3code/continue-commercial-work`. Focused owner: [Order program reference policy](../implementation/ORDER_PROGRAM_REFERENCE_POLICY.md). Owner acceptance: [COM-011/profile contract](COM_011_PROFILE_PREREQUISITE_DECISION_PROPOSAL.md).

COM-011 and the bounded ADM-018–021 prerequisites are locally `PRODUCTION_HONEST`, with `BLOCKED = none` for their declared scope. Existing drafts retain their previous policy. Unprofiled legacy drafts fail closed until an independently authorized, explicit optional-baseline assignment; the assignment keeps their business revision and old receipts intact. A required tenant may activate its required policy directly, while selecting the separate legacy baseline preserves the active profile for new work.

## Qualified responsibilities and permanent guards

| Claim / owner | Falsifiable evidence and recurring guard |
|---|---|
| Fixed catalog and permission metadata / Profiles and Tenancy | Canonical four-feature IDs, dependencies, nonselectability, fingerprint and permission descriptors; Profiles unit suite **27/27** and native OpenFGA model checks. This qualifies the admitted metadata, not a full selectable feature catalog. |
| Typed tenant policy draft/publication / Profiles, CoreApi | Native policy head CAS, immutable revisions/receipts, fresh request authority, scoped PostgreSQL roles and RLS; Profiles PostgreSQL **21/21**, CoreApi **503/503**, including real central membership suspension and replay denial. |
| Separate private profile publication/activation / Profiles, AdminApi | Actual PostgreSQL/OpenFGA Admin-entry intersection and independent publish/activate rights, actor/device receipts, stale authority revisions and revoked replay; AdminApi **102/102** plus Profiles provider tests. |
| Retained profile routing and explicit legacy baseline / Profiles and Orders | Borrowed caller-owned transaction fencing, concurrent routing/creation, restart/connection-loss/cancellation rollback, retained compatibility, irreversible migration guard and explicit Order-revision CAS assignment. Profiles PostgreSQL **21/21**, Orders PostgreSQL **95/95**, and the registered dependency-order migration guard. |
| Reference editing and commitment / Orders, CoreApi | Unicode scalar bounds, malformed/control rejection, canonical stored facts, metadata-only edit authority, current draft CAS, same-key races, receipt-insert rollback, missing/tampered pin evidence and mandatory-reference admission. Orders unit **117/117**, native Orders **95/95** and protected CoreApi contracts. |
| Quoted Order preservation / Quotations and Orders | Actual issue → operator acceptance → conversion → reference edit → commitment, retaining accepted price/lines/origin and original conversion snapshot through replay. Quotations PostgreSQL **36/36** and the Order quoted-fact guards. |
| Receiving authorization prerequisites / Tenancy and CoreApi | Fresh active tenant/account/initial-Owner checks, bounded native serialization-conflict retries, concurrent Owner handoff, restart proposal recovery and uncertain provider observation. Tenancy PostgreSQL **32/32** and CoreApi provider/pipeline tests. This does not replace separate upstream gate-owner acceptance. |

The fixed setting is `RequireReferenceForProgramOrders`; program-attributed Orders require the 128-scalar customer-supplied reference only when their retained policy requires it. Direct non-program Orders remain optional. New direct drafts pin at creation and quoted drafts pin at conversion. Later activation affects new work. Missing or contradictory authority never supplies an implicit optional policy. Reference editing needs `orders.edit` independently of price authority; commitment still needs `orders.commit`.

## Exact verification and source permanence

The final **normal `./eng/verify.sh` exited 0 in 2,810 seconds**, with locked restore, unchanged-format verification, Release build at **zero warnings/errors**, and **1,220 passed / 0 failed / 0 skipped across 23 projects**. The 12 unit and 11 container project groups used the repository's unchanged scheduling; test cases were not forced into serialized execution. Every file in the captured pre-gate manifest remained unchanged throughout the run.

Independent `./eng/build-host.sh core-api --publish`, `admin-api --publish`, and `db-migrator --publish` each exited 0 with zero warnings/errors. All commands used `DOTNET_CLI_HOME=/tmp/application-commercial-dotnet` and `NUGET_HTTP_CACHE_PATH=/tmp/application-commercial-nuget-http-cache`. The one provider project is earned by central PostgreSQL authority; it reuses the admitted Npgsql/EF Core and host mechanisms rather than adding a vendor, process or database.

Evidence is retained at `/home/lets-smile/.t3/artifacts/commercial-continuation-20261007T092009Z`: `com011-repository-gate.log`, `com011-gate-summary.json`, `com011-gate-source.json`, three publish logs/results, source-only packages/manifests, integration review and original failed checks. Final gate SHA-256: `764e897f4fd3b96c3d9093145b7af27ce3734a15a460312c1893f8ae9416d9c6`. Post-qualification documentation changes are checked separately by the architecture suite and source hash comparison; they do not alter the gated runtime or tests.

## Receiving corrections and review

GPT-6 Luna at high effort handled bounded contract/dependency review and delegated work. The root integrated and performed the final source/security review, receiving fixes and verification. No independent whole-product gate acceptance is claimed by this receipt.

Three failed normal gates remain retained: the first exposed malformed-surrogate test-attribute serialization and an outdated stored-error expectation; the second exposed missing protected OpenAPI failure/capacity/deadline declarations and a test that confused raw OpenFGA membership with the host's verified contextual membership; the third exposed a stale expected migration-context list. Each was corrected and focused checks passed before the final normal gate. Earlier native Order checks also exposed historical literal/versioned receipt compatibility and the old quoted-header guard rejecting metadata-only revision advances. Those fixes preserve historical reads and every accepted financial header/line fact. Invalid pinned creation evidence and missing baseline actor/device evidence fail closed. Failed runs are supporting history, never passing qualification.

The inherited task-catalog validator still fails unrelated COM-004/OPS scheduling metadata, gate dependencies and stale hashes. Only the five owned task rows and their scoped hashes were synchronized. This receipt does not claim catalog validation passed.

## Requalification and non-claims

Requalify on catalog IDs/dependencies/fingerprints, permission/model changes, policy meaning, profile or receipt versions, routing/transaction ownership, baseline eligibility/evidence, reference normalization, quoted-fact guards, migrations/grants, host composition or failure/resource bounds. The normal gate and the named permanent tests are the recurring guards; affected hosts also require independent build/publish checks.

COM-012 approvals/inbox/reassignment and COM-013 broader approval admission remain `NOT_INTRODUCED`. No Web UI, dynamic forms, runtime implementation switching, legal approval, stock/fulfillment, invoice persistence, payment/credit, timer, notification or general OPS-013 audit ledger is introduced. Live Hugging Face storage/provider and ZITADEL-instance qualification, other gate-owner claims, remote CI, coverage, deployment and complete-product qualification remain separate. Product version remains `v0.0.1`.
