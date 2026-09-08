# Verification and Failure-Injection Strategy

**Version:** v0.0.15

SquiFlow's phase documents contain many attack cases, but those cases need an explicit test architecture so they do not remain prose-only requirements.

## 1. Test layers

Use the smallest test layer that can prove the invariant.

### Domain/property tests
For pure business rules and invariants:
- money/rounding/allocation invariants;
- allowed state transitions;
- rule-engine determinism;
- permission/delegation logic without HTTP;
- idempotency semantic comparison helpers;
- quantity/unit calculations.

### Application tests
For use cases with mocked or test adapters only where the boundary itself is not under test:
- command validation;
- authorization requirement composition;
- workflow continuation;
- outbox/job creation decisions.

### Persistence adapter tests
Run against the real candidate database implementation, not an in-memory substitute, for:
- transactions;
- constraints;
- concurrency;
- claims/leases/fencing;
- migrations;
- tenant isolation/RLS where applicable;
- idempotency receipt atomicity;
- backup/restore primitives.

### Workstation local-store tests
Run the actual candidate SQLite/libSQL adapter for:
- atomic business + outbox writes;
- restart/recovery;
- lock contention;
- schema migration;
- corruption/repair behavior;
- long-offline queue persistence.

### API/authorization tests
Use the real ASP.NET Core pipeline for:
- authentication/tenant-context boundary;
- function/resource/property authorization;
- cross-tenant negative tests;
- idempotency/retry;
- Problem Details/error contracts;
- request limits;
- custom-domain/Host validation.

### End-to-end journey tests
Prove complete journeys across actual runtime boundaries only for important vertical slices, for example:

```text
Web Owner grants Staff role
→ Workstation refreshes effective permissions
→ Staff creates local order offline
→ reconnects
→ Core API authorizes/syncs
→ Worker creates a document
→ printing fails then retries
```

Do not make every test an expensive full end-to-end test.

## 2. Architecture dependency tests

Automate forbidden dependency rules such as:

- Domain/Application must not depend on ASP.NET Core;
- ordinary Web/Desktop must not depend on persistence-provider implementations;
- Admin Web does not become a DB/SSH client;
- provider-specific types do not leak into stable domain/application contracts;
- Workstation does not reference platform-control-plane implementation;
- Worker does not depend on presentation projects.

These tests make repository boundaries executable rather than diagram-only.

## 3. Failure injection

Happy-path tests are insufficient.

Required failure classes include:

### Local Workstation
- abrupt process termination after local commit;
- power-loss-equivalent/restart test appropriate to local DB;
- disk full/low disk;
- DB busy/locked;
- lost in-memory sync wake signal;
- sleep/hibernate during sync;
- missing/changed staged attachment;
- local data tamper/corruption;
- months-old client returning.

### Sync/API
- server commits and response is lost;
- duplicate same-key request;
- same idempotency key with changed intent;
- partial sync batch failure;
- stale expected version;
- permission revoked while operation pending;
- tenant ID/object ID tampering;
- retry storm/thundering herd;
- version/protocol mismatch.

### Worker
- crash before effect;
- crash after external effect but before completion persistence;
- stale lease owner resumes;
- no-progress/hung helper;
- repeated transient failure;
- poison work/quarantine;
- pause/drain/restart;
- priority starvation attempt.

### Data/object storage
- DB succeeds/object fails;
- object succeeds/metadata fails;
- restore DB without matching object version;
- object capacity near hard limit;
- network interruption mid-upload;
- backup restore onto replacement hardware.

### Identity/security
- wrong issuer/audience;
- expired/replayed login transaction;
- PKCE mismatch;
- open redirect;
- cross-tenant BOLA/BFLA/property attack;
- stale authorization revision;
- custom Host/domain confusion;
- session revoked while form/action is open.

## 4. Hardware/resource qualification

Benchmarks run on the actual deployment class, not only a developer laptop/CI runner.

Measure at least:
- API latency under constrained CPU/RAM;
- DB pool saturation;
- Worker backlog age;
- document/image helper peak RSS and release after work;
- Workstation idle/active resource use;
- object transfer bandwidth;
- disk utilization/free-space behavior;
- restart/recovery time.

A benchmark result records hardware/OS/database version/configuration so it is reproducible.

## 5. Accessibility verification

Use the requirements in `docs/ux/ACCESSIBILITY_AND_INTERACTION_QUALITY.md`.

Automated checks are supplemented by keyboard, focus, screen-reader/manual smoke and state/error tests on core journeys.

## 6. Test data and tenant safety

Test fixtures deliberately include at least Tenant A and Tenant B so cross-tenant failures can be attacked.

Do not use production customer data in normal CI/test environments.

Synthetic fixtures include:
- small two-user tenant;
- organization/program tenant;
- large-data/noisy tenant;
- long-offline Workstation;
- conflicting concurrent actors.

## 7. Migration and compatibility matrix

Test supported upgrade paths rather than only latest-to-latest.

Cover the supported window for:
- central DB schema;
- local Workstation schema;
- API/sync protocol;
- durable message envelopes;
- rule/workflow/config snapshots;
- Web/Admin/API rolling compatibility where independent deploys occur.

Skipped Workstation releases are a specific test case.

## 8. Restore verification

Backup tests are incomplete until restore proves:

- application can start;
- tenant isolation still holds;
- DB/object references reconcile;
- idempotency/job state does not accidentally recreate completed effects;
- secrets/config needed for recovery are available through the intended process;
- audit/history remains explainable according to retention policy.

## 9. CI versus qualification

Not every hostile hardware test belongs in every commit pipeline.

Use:
- fast deterministic tests on every merge;
- adapter/integration tests in CI when practical;
- scheduled/nightly failure/load tests;
- release qualification on actual deployment hardware;
- manual restore/security/accessibility exercises where automation cannot prove the behavior.

CI status must not imply that hardware/recovery qualification was run if it was not.

## 10. Definition of a testable requirement

A requirement is incomplete if it cannot state:

1. observable expected outcome;
2. failure/attack input;
3. authoritative component under test;
4. cleanup/recovery expected afterward;
5. whether it is merge-CI, scheduled, release-qualification or manual evidence.
