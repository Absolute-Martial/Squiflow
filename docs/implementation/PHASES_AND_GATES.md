# Sequential Implementation Phases and Gates

**Version:** v0.0.15

SquiFlow should not open many parallel incomplete tracks. Each phase proves a vertical slice and its failure/recovery behavior before the next dependent phase expands.

## Phase 0 — repository/runtime skeleton

Deliver:
- `apps/web`, `apps/admin-web`, `apps/desktop`;
- `services/core-api`, `services/worker`;
- business modules/packages/persistence abstractions;
- error/result/execution-context contracts;
- architecture dependency tests;
- basic CI/build/test/package pipeline.

Gate:
- each runtime builds separately;
- forbidden dependencies are caught;
- no provider/reference project leaks into domain/application contracts.

## Phase 1 — identity + smallest tenant

Deliver:
- canonical Web identity authority;
- Owner tenant bootstrap;
- invite one Staff user;
- Web-only role/permission assignment;
- Workstation system-browser login + PKCE/device enrollment;
- effective permission retrieval.

Attack:
- invitation expires;
- Owner removes/suspends Staff;
- permission changes while Staff is logged in;
- attempt to change permissions from Desktop;
- attempt to grant beyond delegation/tenant entitlement.

Gate:
- only Web administration can modify grants;
- server authorization rejects stale/forged authority.

## Phase 2 — first local-first Workstation transaction

Deliver minimal Customer/Walk-in + Order slice:

```text
UI
→ local validation
→ local durable business + outbox transaction
→ immediate local result
```

Attack:
- power loss after commit;
- lost Channel signal;
- app restart;
- disk full;
- local DB busy/locked;
- user closes app immediately.

Gate:
- user work survives crashes;
- no server/network dependency for the approved local operation.

## Phase 3 — authoritative synchronization

Deliver:
- bounded upload batch;
- idempotency receipt;
- server tenant derivation/authorization;
- one authoritative central transaction;
- per-item result;
- remote change feed + cursor;
- local result/cursor durability.

Attack:
- server commits, response lost;
- duplicate request;
- permission revoked while pending;
- malformed/tampered tenant ID;
- partial batch failure.

Gate:
- no duplicate business effect;
- no cursor advancement before local apply;
- explicit `AuthorizationChanged`/conflict states.

## Phase 4 — conflict + long-offline

Deliver:
- one aggregate conflict UX;
- protocol/schema version negotiation;
- reauth/upgrade/resnapshot/repair paths;
- pending local work preservation.

Attack:
- weeks/months offline;
- tombstone expired;
- entity deleted/merged remotely;
- 10k+ pending changes;
- old rule snapshot.

Gate:
- no silent local data discard.

## Phase 5 — native rules + workflow

Deliver:
- one tenant rule;
- one configurable tenant workflow stage;
- one approval transition;
- Web-only authoring/publication;
- immutable effective snapshot to API/Workstation;
- decision trace.

Attack:
- invalid rule;
- conflicting rules;
- workflow definition changes while instance active;
- approver loses permission;
- two approvers act simultaneously.

Gate:
- last known good rule/workflow remains usable if publication fails.

## Phase 6 — Worker and platform control plane

Deliver:
- durable job/outbox;
- Worker claim/lease/retry;
- no-progress detection;
- pause/drain/resume;
- quarantine/reconciliation;
- Platform Admin Web controls.

Attack:
- Worker crashes after external effect;
- stale lease owner resumes;
- admin retries repeatedly;
- pause during active job;
- external outcome unknown.

Gate:
- critical Worker/server controls are reachable only from Platform Admin Web;
- no generic force-complete operation.

## Phase 7 — files/documents/printing

Deliver:
- local attachment staging;
- object upload + metadata lifecycle;
- document helper where isolation justified;
- printing status independent from transaction status.

Attack:
- object succeeds/metadata fails;
- metadata succeeds/object missing;
- printer out of paper;
- helper crashes;
- disk full during staging.

## Phase 8 — observability/admin hardening

Deliver:
- OpenTelemetry traces/metrics/log correlation;
- New Relic + Aiven OpenSearch export;
- Backtrace crash path;
- tenant/platform audit;
- step-up and exact-diff high-risk admin workflows.

Gate:
- telemetry failure never invalidates a business transaction;
- sensitive data is redacted/bounded.

## Phase 9 — payments/credit/inventory hardening

Deliver:
- `OutcomeUnknown` payment state;
- refund/reversal/correction;
- inventory concurrency policy;
- current credit exposure authority;
- owner/manual price permissions/audit.

Attack:
- provider charge succeeds but response lost;
- concurrent last-stock sale;
- offline stale credit exposure;
- duplicate refund/webhook.

## Phase 10 — release/resource/recovery qualification

Deliver:
- Workstation resource/idle tests;
- constrained single-node server tests;
- installer/update/rollback;
- schema/protocol/rule/config compatibility;
- backup/restore drill;
- long-running leak/soak tests.

## Deferred until baseline proves itself

Do not spend baseline implementation effort on:
- full browser offline/PWA business sync;
- Kafka;
- YugabyteDB;
- mandatory Redis;
- microservice-per-module extraction;
- global CRDT data model;
- advanced manufacturing/MRP/wastage;
- specialized search infrastructure without workload evidence.
