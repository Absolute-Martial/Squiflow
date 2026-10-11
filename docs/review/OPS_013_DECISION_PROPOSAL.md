# OPS-013 decision proposal: reuse the Fulfillment pickup journal

**Status:** Accepted by the owner on 2026-10-10 for the complete proposed bounded contract. Acceptance authorizes implementation; qualification remains pending until its native evidence and normal repository gate pass.

## Proposed initial audit profile

Select one successful operator-recorded pickup (`FulfillmentEventKind.Handoff`) for the already-qualified COM-014/015 case: one committed, program-attributed Order with one catalog-priced in-house non-stock service line. Reuse the existing Fulfillment journal, command receipts, and protected history path. Do not add an audit service, generic event store, new persistence boundary, or second copy of the same transition.

The existing journal carries the proposed minimum evidence:

| OPS-013 fact | Existing retained evidence |
|---|---|
| Tenant and business resource | Tenant-scoped `tenant_id` and `order_id`; the retained source snapshot identifies the committed Order line and source Order revision. |
| Actor | `FulfillmentEvent.ActorAccountId`, with the tenant authorization revision observed for the command. |
| Action and outcome | `Kind = Handoff`, quantity and retained unit facts; a committed event records an accepted pickup effect. Denied or rejected attempts are outside this first profile. |
| Fact revision and time | Event ID, fulfillment revision, server UTC `RecordedAt`, and the frozen source Order revision. |
| Correlation and replay | The caller/operation-scoped command receipt retains the idempotency identity and original result. Its tenant, Order, revision, actor and operation are checked against event/activity evidence by the database guard. Replays return the retained result without creating another event. |

This profile does not claim that a recorded pickup proves physical delivery, customer acceptance, payment, or legal receipt. The current Fulfillment owner expressly limits pickup to operator-recorded evidence.

## Proposed retention, access, and privacy decisions

- Retain the selected journal and its command receipts indefinitely. There is no automatic deletion or runtime maintenance path in this profile. Any future retention change, legal hold, or privileged maintenance process requires a separate owner decision and requalification.
- Preserve corrections as appended linked events. Do not rewrite or delete the original pickup event. This is append-only behavior enforced by runtime privileges and database guards; it is not a tamper-evident or unrestricted-forensics claim.
- Reuse current active membership and the existing `orders.fulfillment.view` permission for tenant-scoped history access. Keep the existing bounded history order/cursor contract and safe error handling. No new read authority is implied.
- Do not add credentials, raw request bodies, or additional user-entered content to the audit profile, logs, or telemetry. The existing Fulfillment event format retains recipient labels and correction/disposition reasons for its own protected business-history contract. OPS-013 must not widen that exposure; if the audit read requires a distinct projection, it should return only the minimum audit fields above. The owner must accept that existing COM-014/015 retention and access boundary as part of selecting this journal.
- The CoreApi runtime role must remain unable to update or delete retained event and receipt rows. Schema migration or database-owner access is not runtime maintenance authority.

## Existing evidence and gaps for OPS-013

The current COM-014/015 owner and receiving receipt already qualify much of this profile. Relevant existing guards include:

- `FulfillmentPostgresTests.PartialCompletionAndPickupPersistOnlyOperationalFactsWithoutPriceDisclosure` and `TwoActorsHaveOneRevisionWinnerAndSemanticReplayRejectsChangedIntent` for pickup facts and concurrency/replay.
- `AuthorityRevocationDeniesRestartedReplayAndReceiptFailureRollsBackAllEvidence` and `DatabaseRejectsUnpairedActivityEventsReceiptsAndWorkAdvancementAndRollsBackFailures` for current authority and PostgreSQL rollback/consistency.
- `RuntimeRoleEnforcesTenantIsolationAcrossAllFourTablesAndRejectsCrossTenantInsert` and `FulfillmentRuntimeGrantTests.ProvisioningGrantsOnlyTheEffectiveFulfillmentTableAndColumnRights` for RLS and effective privilege bounds.
- `HistoryAndInboxBoundsHoldAndMalformedStoredFactsFailClosed`, `FulfillmentEndpointTests.HistoryPageBoundsAndQueryShapeAreEnforcedBeforeStorage`, and `InboxCursorIsBoundToTheRequestedTenant` for bounded history and cursor scope.
- `FulfillmentNativeEndpointTests.NativeHttpJourneyRetainsCommittedFactsAndProtectsFulfillmentHistoryAcrossRestart` for the real PostgreSQL/OpenFGA/CoreApi journey, protected history and restart/replay.

These are existing COM-014/015 evidence, not a new OPS-013 qualification claim. The receipt records the exact prior integrated gate; this proposal did not rerun it.

Before accepting OPS-013 for this profile, retain focused evidence that closes the remaining task-specific claims:

1. **Atomic audit failure:** use the real PostgreSQL path to fail the handoff journal write and the linked command-receipt write independently; prove no successful handoff, projection advance, activity row, or receipt remains. Existing event/receipt failure triggers are close analogues and may be extended or explicitly accepted as sufficient if they exercise the exact proposed pickup path.
2. **Runtime-role denial:** with the actual restricted role, attempt `UPDATE` and `DELETE` against handoff event and receipt rows and assert PostgreSQL denial. Existing privilege inspection and runtime mutation guards are strong evidence; the task receipt should name the direct denial proof.
3. **Replay and read stability:** prove a same-key retry yields the original pickup result and no additional audit fact, then read consecutive bounded history pages in stable order under current membership and viewer permission. Existing replay and pagination tests should be reused where they prove this exact selected profile.
4. **Telemetry independence:** disable OTLP export in a real CoreApi/PostgreSQL test, commit a pickup, restart the host, and read the retained audit history successfully. This establishes durable evidence independently of telemetry delivery.

Run the applicable focused suites and exact normal `./eng/verify.sh` on the integrated source; inspect the full results. Do not claim OPS-013 qualified based only on this proposal or historical COM-014/015 results.

## Owner decision accepted on 2026-10-10

The owner accepted all six choices as one decision:

1. The initial OPS-013 transition is an accepted Fulfillment pickup event for the declared COM-014/015 case.
2. The existing event journal and command receipt are sufficient authoritative audit evidence, with no second ledger.
3. The journal/receipts have indefinite retention, no automatic cleanup, and no runtime maintenance path; corrections append.
4. Current membership plus `orders.fulfillment.view` is the access boundary, with tenant-scoped bounded history reads.
5. Existing recipient/reason retention remains governed by the Fulfillment owner and is not exposed more broadly by OPS-013; any separate OPS-013 read projection is data-minimized.
6. The evidence listed above is required before OPS-013 can be marked `PRODUCTION_HONEST`; no tamper-evident or legal-forensics claim is made.

## Current gate context

The repository owner accepted GATE-001 for the declared backend-entry baseline, as recorded in `docs/production-completion-review/evidence/GATE-001-OWNER-ACCEPTANCE.md` and summarized in `README.IMPLEMENTATION.md` lines 91 and 106. ADM-006 remains a separate current receiving blocker under README lines 92–93. The older paragraph around README line 172 labels the following blocker text historical; it is not reopened by this proposal. OPS-013 still requires its own exact evidence and normal repository gate on the final integrated source.

## Canonical owners

- OPS-013 task: `docs/development-tasks/phase-03-runtime-operations/OPS-013-retain-and-read-capability-owned-business-audit.md`
- Fulfillment owner: `docs/implementation/ORDER_SERVICE_FULFILLMENT.md`
- COM-014/015 receiving evidence: `docs/review/COM_014_015_IMPLEMENTATION_RECEIPT.md`
- Gate-owner acceptance: `docs/production-completion-review/evidence/GATE-001-OWNER-ACCEPTANCE.md`
