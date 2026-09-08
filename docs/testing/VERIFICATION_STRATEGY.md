# Verification and Failure-Injection Strategy

**Version:** v0.0.15

SquiFlow uses the smallest test layer that can prove a real invariant. Do not create interfaces/mocks merely to increase unit-test count.

## 1. Test layers

### Domain/property tests
Use for pure logic that genuinely exists:
- allowed state transitions;
- rule determinism;
- permission/delegation logic;
- idempotency semantic comparison;
- quantity/rounding rules actually used by implemented features.

### Application tests
Use for use-case orchestration where a fake collaborator is useful and the collaborator's real behavior is not the subject of the test.

Do not invent an interface solely so every class can be mocked.

### Real persistence-adapter tests
Run the actual candidate/selected DB for behavior that cannot be trusted to an in-memory substitute:
- transactions;
- constraints;
- concurrency/locking;
- claims/leases where implemented;
- migrations;
- tenant isolation/RLS where applicable;
- idempotency atomicity;
- restart/recovery behavior that the provider exposes.

### Workstation local-store tests
Run the actual SQLite/libSQL candidate/selection for:
- atomic business + outbox writes;
- restart recovery;
- locking/busy behavior;
- schema migration;
- long-offline queue persistence;
- corruption/repair behavior that can be reproduced safely.

### API/authorization tests
Use the real ASP.NET Core pipeline for:
- authentication/TenantContext;
- function/resource/property authorization;
- cross-tenant negative tests;
- idempotency/retry;
- error contracts;
- request limits;
- custom-domain/Host validation.

### Selected end-to-end journeys
Use only where crossing the real runtime boundaries is what needs proof, for example:

```text
Owner grants Staff permission in Web
→ Staff signs in to Workstation
→ creates local order
→ reconnects
→ Core API authorizes/syncs
→ server accepts or explicitly conflicts
```

Later phases extend this with Worker/object/printing only when those components exist.

## 2. Architecture tests

Test only real boundaries that exist.

Examples:
- domain/application code does not depend on ASP.NET Core merely for convenience;
- Web/Desktop do not directly become central DB clients;
- provider-specific Hugging Face/DB types do not leak into business/domain records;
- Workstation does not gain platform-control-plane authority;
- future Worker must not depend on presentation projects.

Do not require architecture tests for placeholder projects that were never created.

## 3. Failure injection

### Workstation
- process termination after local commit;
- restart with pending outbox;
- disk full/low space;
- local DB busy/locked;
- lost in-memory sync wake signal;
- sleep/hibernate during sync;
- missing/changed staged attachment;
- old client returning after a long period;
- printer/spooler failure when printing is implemented.

### Sync/API
- server commits and response is lost;
- duplicate same-key request;
- same key + changed intent;
- partial sync batch failure;
- stale expected version;
- permission revoked while pending;
- tenant/object ID tampering;
- retry storm;
- protocol/version mismatch.

### Worker — when Phase 6 exists
- crash before effect;
- crash after external effect before completion persistence;
- stale lease owner;
- no-progress work;
- repeated transient failure;
- poison work/quarantine;
- pause/drain/restart;
- priority starvation.

### Hugging Face object storage — when Phase 7 exists
- upload succeeds/metadata fails;
- metadata succeeds/object missing;
- capacity approaches limit;
- upload interrupted;
- cross-tenant object reference;
- object hash mismatch.

### Kaggle backup — when Phase 7/10 exists
- encrypted artifact upload succeeds but local record fails;
- remote artifact missing/corrupt;
- checksum mismatch;
- wrong/missing decryption key;
- download succeeds but restore fails;
- raw readable customer data accidentally selected for direct upload (must be rejected by backup tooling/process).

### Identity/security
- wrong issuer/audience;
- PKCE/state/replay/open-redirect attack;
- stale authorization revision;
- custom Host/domain confusion;
- cross-tenant BOLA/BFLA/property attack;
- session revoked during an operation.

## 4. Actual hardware qualification

Run relevant benchmarks/failure tests on the actual lower-spec deployment class, not only a developer laptop/CI runner.

Measure as needed:
- Core API latency under constrained CPU/RAM;
- DB pool/resource saturation;
- Workstation idle/active resource use;
- local/object transfer bandwidth;
- disk/free-space behavior;
- restart/recovery time;
- future Worker backlog age when Worker exists.

Record hardware/OS/DB/configuration with the result.

## 5. Test data and tenant safety

Use synthetic test data. Include at least Tenant A and Tenant B for tenant-isolation attacks.

Do not use production customer data in normal CI/test environments.

Useful fixtures can include:
- Owner + Staff tenant;
- organization/program tenant;
- noisy/large tenant;
- long-offline Workstation;
- conflicting concurrent actors.

## 6. Migration/compatibility

Test the supported upgrade window for what actually exists:
- central DB schema;
- Workstation local schema;
- API/sync contract;
- durable messages once Worker exists;
- rule/workflow/form snapshots once Phase 5 exists.

Skipped Workstation releases are a specific case.

## 7. Backup restore proof

An encrypted Kaggle upload is not enough.

A restore proof checks:
- backup artifact downloads;
- checksum verifies;
- key material is available;
- archive decrypts;
- DB/application state restores;
- object metadata/bytes reconcile for the backed-up scope;
- tenant isolation still holds;
- idempotency/job state does not recreate completed effects unexpectedly.

Before the first paying customer, this proof must be real, not prose.

## 8. CI versus release qualification

Use:
- fast deterministic tests on merge;
- real adapter/integration tests in CI where practical;
- scheduled failure/load tests only when useful;
- actual-hardware/recovery/backup restore qualification before production.

CI success must not imply a physical/restore test ran when it did not.

## 9. Testability rule

A requirement is testable when it can state:
1. observable expected outcome;
2. failure/attack input;
3. authoritative component being exercised;
4. required recovery/cleanup;
5. where evidence runs: merge CI, real adapter, scheduled, actual hardware, or manual restore/runbook exercise.

Testing should drive confidence, not an interface/helper hierarchy.
