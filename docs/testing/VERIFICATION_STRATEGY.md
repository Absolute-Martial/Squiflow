# Verification and Failure-Injection Strategy

**Version:** v0.1.0

SquiFlow uses the smallest test layer that can prove a real invariant. Lean architecture does **not** mean shallow testing: edge/failure behavior that protects core capability remains required.

## 1. Test layers

### Domain/property tests
Use for pure invariants such as:
- money/rounding/allocation;
- allowed state transitions;
- rule determinism;
- quantity/unit calculations;
- semantic idempotency comparison.

### Application tests
Use for command validation, workflow continuation, authorization composition around mocked external decisions only where the external integration itself is not under test.

### Real persistence-adapter tests
Use the selected real PostgreSQL implementation for:
- transactions/constraints;
- expected-version, atomic-update, isolation, concurrency/locking, deadlock/serialization and bounded whole-transaction retry behavior;
- tenant isolation/RLS where applicable;
- idempotency atomicity;
- claims/leases when Worker exists;
- additive/backfill/switch/contract migrations with old/new reader/writer overlap and recovery.

### Workstation local-store tests
Use the selected SQLite/WAL implementation for:
- atomic business + outbox commit;
- restart/recovery;
- lock contention;
- schema migration;
- corruption/repair behavior;
- long-offline queue persistence;
- disk-full behavior.

### ZITADEL identity integration tests
Cover:
- OIDC discovery/issuer/audience validation;
- Web login/session establishment;
- Workstation Authorization Code + PKCE;
- redirect/state/nonce failure;
- account subject stability when email changes;
- session/revocation/logout behavior chosen for deployment;
- service-account least privilege for ZITADEL management APIs.

Where possible use an isolated real ZITADEL test environment/config rather than pretending token parsing alone proves identity integration.

### OpenFGA authorization tests
Use the real OpenFGA API/SDK against an isolated store for:
- pinned authorization model ID;
- Owner/Staff role behavior;
- tenant-created custom role tuples;
- multiple role assignments;
- permission/resource checks;
- tuple write/delete;
- model migration compatibility;
- consistency modes where used;
- PII-free opaque identifiers;
- cross-tenant relation attacks;
- ambiguous write/reconciliation behavior.

Do not mock OpenFGA in the tests whose purpose is to prove tuple/model/consistency behavior.

### API/authorization tests
Use the real ASP.NET Core pipeline for:
- ZITADEL-authenticated identity/session boundary;
- authoritative TenantContext;
- `IAuthorizationService` semantic requirement;
- OpenFGA allow/deny/error integration;
- domain/workflow rejection after OpenFGA allow;
- cross-tenant negative tests;
- idempotency/retry;
- request limits/custom-domain validation;
- stable HTTP status/Problem Details mappings;
- CSRF/antiforgery for cookie-authenticated mutations;
- mass-assignment/excessive-field and injection/SSRF defenses that live in the API pipeline.

### Web/application security tests

Use the real rendering/session/template/file paths as applicable for:

- stored, reflected, and client-side XSS/output encoding;
- sanitized rich content and Content Security Policy behavior where enabled;
- cookie scope/flags and Tenant Web versus Platform Admin Web/custom-domain separation;
- SQL/identifier/filter/sort injection through concrete data-access paths;
- outbound URL redirects/DNS/private-network/size/timeout controls;
- file name/path/type/size/decompression/template/parser abuse;
- secrets/tokens/customer data absent from normal logs, traces, dumps, client artifacts, and error responses.

Owner: `docs/security/APPLICATION_SECURITY_BASELINE.md`.

### Provider contract tests
`IObjectStore` and `IBackupTarget` are intentional replacement seams and therefore get contract tests.

`IObjectStore` contract proves, as supported by SquiFlow semantics:
- put/read;
- hash/size/metadata behavior;
- missing-object result;
- delete/retire semantics where exposed;
- cancellation/timeouts/stream disposal;
- provider errors map to stable SquiFlow results without hiding important provider diagnostics.

Run the same contract against `HuggingFaceObjectStore` and later paid adapters.

`IBackupTarget` contract proves:
- encrypted artifact upload;
- list/identify retained versions;
- download;
- integrity metadata;
- retention/delete where supported by SquiFlow policy.

Run it against `KaggleBackupTarget` and later paid adapters.

### Guard/desktop process tests
Run the real process boundary for:
- Guard launches Workstation;
- Workstation crash/exit;
- Guard crash while Workstation remains usable;
- bounded restart/backoff;
- hang/heartbeat behavior including false-positive resistance;
- sleep/hibernate/clock jump;
- safe mode;
- Guard/Workstation version mismatch;
- update handoff/recovery;
- helper cleanup where helpers later exist;
- bounded diagnostics/resource observations.

A mocked `IGuard` does not prove process supervision.

### Observability runtime tests
Use the real OpenTelemetry/runtime path where the behavior under test depends on propagation/export/processing.

Prove representative:
- trace/log correlation across Workstation → Sync/API → authorization → DB/outbox/Worker;
- stable EventId/EventName/FailureCode uniqueness and compatibility;
- state-transition event emission;
- secret/PII redaction before external export where feasible;
- bounded metric cardinality under many tenants/entities;
- Workstation local-durable diagnostics while offline;
- collector/provider outage and bounded queue/spool behavior;
- diagnostic-bundle redaction and size bounds;
- telemetry self-health (drops/exporter/spool/sampling evidence);
- alert grouping/deduplication/rate control;
- UTC/monotonic clock behavior under sleep/clock jumps;
- observability resource overhead on the actual low-end deployment class.

Detailed acceptance lives in `docs/observability/OBSERVABILITY_VERIFICATION_ACCEPTANCE.md`.

### End-to-end journey tests
Keep these selective and meaningful, for example:

```text
Owner logs in through ZITADEL
→ creates/edits custom Staff role in Web
→ Core API applies/reconciles OpenFGA tuples
→ Workstation receives effective snapshot
→ Staff creates local order offline
→ Workstation crashes and Guard restarts it
→ order survives locally
→ reconnect
→ server rechecks OpenFGA + tenant/domain state
→ sync commits authoritatively
→ correlated evidence can reconstruct the path without leaking secrets
```

## 2. Architecture dependency tests

Automate rules such as:
- domain/application code does not depend on ASP.NET Core merely for convenience;
- Web/Desktop do not directly become central DB or OpenFGA business-authority clients;
- Hugging Face/Kaggle/ZITADEL/OpenFGA provider SDK types do not leak into business/domain records;
- New Relic/OpenSearch/Backtrace provider SDK types do not leak into business/domain contracts;
- `IObjectStore`/`IBackupTarget` are consumer-facing SquiFlow contracts rather than copied SDK surfaces;
- Guard does not reference business modules/ORM/OpenFGA authorization implementation;
- future Worker does not depend on presentation projects.

## 3. Failure injection

### Workstation/Guard
- abrupt Workstation termination after local commit;
- Guard restart independent of Workstation;
- both processes killed;
- crash loop/restart budget;
- false hang signal during sleep/slow machine;
- disk full;
- DB busy/locked;
- pending attachment missing/changed;
- months-old client returning;
- update interruption.

### Identity/authorization
- ZITADEL unavailable/degraded;
- wrong issuer/audience;
- expired/replayed auth transaction;
- PKCE mismatch;
- open redirect attempt;
- OpenFGA unavailable/degraded;
- OpenFGA allow but wrong tenant/resource in DB;
- OpenFGA model ID mismatch;
- role revocation immediately followed by sensitive action;
- tuple write succeeds but SquiFlow status persistence fails;
- duplicate/retried authorization mutation;
- stale lower-consistency check where higher consistency is required.

### Sync/API
- server commits and response is lost;
- duplicate same-key request;
- changed intent with same key;
- stale expected version;
- permission revoked while pending;
- retry storm/version mismatch;
- concurrent lost-update attempt, deadlock/serialization conflict and bounded whole-transaction retry;
- cache outage/cold start/stampede and cross-tenant/stale-sensitive cache key where a cache exists.

### Object/backup
- Hugging Face upload succeeds/DB metadata fails;
- DB metadata exists/object missing;
- provider times out mid-stream;
- object capacity approaches limit;
- Kaggle upload/download failure;
- backup artifact corruption/wrong key;
- backup excludes required state;
- restore to replacement environment.

### Worker/external effects
When Worker exists:
- crash before/after external effect;
- stale lease;
- poison work;
- no-progress;
- pause/drain/restart;
- priority starvation.

### Observability
- OTLP collector unavailable;
- exporter timeout/429/quota exhaustion;
- collector queue saturation;
- local diagnostic area near/full;
- crash while Workstation is offline;
- log/bundle redaction hostile inputs;
- one noisy tenant producing high-volume telemetry;
- alert storm from crash loop/shared outage;
- severe Workstation clock skew/clock jump.

Expected throughout: telemetry failure cannot roll back or corrupt committed business state and cannot grow RAM/disk without bound.

## 4. Actual-hardware qualification

Benchmarks/failure tests run on the real deployment class, not only developer/CI machines.

Measure at least:
- API latency and DB pool saturation;
- Workstation + Guard idle/active/resource behavior;
- Guard false-positive hang/restart behavior under low CPU/RAM;
- Worker backlog age when implemented;
- document/image peak RSS;
- object transfer bandwidth;
- disk/free-space behavior;
- restart/recovery time;
- observability idle/active RSS/allocation/CPU/disk-write/network overhead;
- local diagnostic footprint and spool pressure.

Resource targets must protect function. If Guard or required observability evidence needs more than an arbitrary initial memory estimate to work reliably, adjust the budget from measurement rather than deleting required behavior.

## 5. Test data and tenant safety

Fixtures include at least Tenant A and Tenant B so tenant isolation can actually fail in tests.

Use opaque synthetic user/object IDs in OpenFGA and observability fixtures. Do not put production customer data/PII in ordinary CI/test stores.

Cross-tenant telemetry/support tests must prove Tenant A cannot retrieve Tenant B logs/traces/bundles and that a spoofed request-body tenant ID cannot become authoritative telemetry context.

## 6. Migration/compatibility matrix

Cover supported upgrade paths for:
- central DB schema;
- Workstation local schema;
- API/sync protocol;
- Guard/Workstation IPC/protocol;
- OpenFGA authorization model IDs/tuple migrations;
- ZITADEL client/session configuration changes;
- durable messages;
- rule/workflow/config snapshots;
- `IObjectStore`/`IBackupTarget` provider migration;
- stable EventId/EventName/FailureCode registry compatibility where external dashboards/runbooks/tests depend on it.

For destructive schema/contract contraction, also prove that supported old application instances, skipped Workstations, pending local sync, durable jobs/messages, stored idempotency results, and rule/workflow/form snapshots are compatible, migrated, drained, rejected explicitly, or outside the documented support window. Do not infer this from a successful migration on an empty database.

## 7. Restore verification

Backup testing is incomplete until restore proves:
- SquiFlow can start;
- tenant isolation still holds;
- DB/object references reconcile;
- idempotency/job state does not recreate completed effects;
- ZITADEL/OpenFGA can be reconnected/restored/reprovisioned according to managed/self-hosted topology;
- Guard/Workstation deployment can restart from durable local state;
- secrets/key recovery follows the intended secure procedure;
- business history remains explainable.

## 8. CI versus qualification

Use:
- fast deterministic tests on merge;
- stable event/failure registry uniqueness/compatibility tests on merge;
- redaction/cardinality architecture tests on merge where deterministic;
- secret/dependency/static checks appropriate to the implemented surface, with explicit triage rather than ignored scanner output;
- real adapter/integration tests in CI where practical;
- isolated ZITADEL/OpenFGA/provider contract tests on scheduled/appropriate pipelines;
- scheduled observability outage/load/cardinality/alert-storm tests;
- scheduled failure/load tests;
- release qualification on actual hardware;
- manual restore/security exercises where automation cannot prove the behavior.

Release qualification promotes the same immutable artifact, records checksum/provenance, runs deployment/migration preflight and authorized smoke checks, and exercises the selected rollback/roll-forward/maintenance recovery path. If containers are selected, scan/test the final image and its runtime identity/capability/resource configuration.

CI success must never imply that actual-hardware/restore/provider-migration exercises ran when they did not.

## 9. Definition of a testable requirement

A requirement is incomplete if it cannot state:
1. observable expected outcome;
2. failure/attack input;
3. authoritative component under test;
4. cleanup/recovery expected afterward;
5. evidence layer/environment.
