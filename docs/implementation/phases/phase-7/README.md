# Phase 7 — Files, Documents, Printing, and Backup/Restore

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Purpose

Phase 7 qualifies provider-bound large-object storage, file transfer, document/printing effects, and complete backup/restore for the state that exists so far.

It does not mean files/documents/backups are forbidden earlier. Provider contracts, small local artifacts, or basic backup concepts may already exist. Phase 7 is where the real provider-bound lifecycle and restore proof become qualified for the implemented system.

## Subphases

```text
7A  Object-storage ownership, integrity and provider adapter
7B  Attachment transfer, staging, capacity and consumption accounting
7C  Document generation, desktop process isolation and printing
7D  Backup target, encrypted recovery set and restore proof
7E  Integrated Phase-7 production-honesty gate
```

## Continuing development

Capabilities, Workstation, Web, Sync, Admin, Worker and data models continue evolving. A capability may start using objects/documents only when its own lifecycle/ownership rules are defined, and other unrelated capabilities remain free to advance during this phase.

If an earlier real capability needs durable files/printing/backup behavior before the planned parent phase, pull the relevant subphase foundation forward rather than inventing temporary provider-specific or non-recoverable behavior.

## Phase-specific evidence and regression map

- **7A — object storage:** contract/integration tests permanently verify SquiFlow-owned keys/metadata/hash/lifecycle/tenant ownership and provider-error mapping. Real provider exercises run `SCHEDULED`/`PRE_RELEASE`; business code/provider-type leakage is blocked `PER_MR` by architecture checks.
- **7B — transfer/capacity/consumption:** bounded upload/download/resume/retry tests run `PER_MR`; consumption/limit races, duplicate upload accounting, interrupted transfers and near-capacity behavior are recurring integration/load evidence. Cross-tenant references are hostile-test failures, not SLO events.
- **7C — documents/printing/process isolation:** deterministic document semantics and committed-business separation run `PER_MR`; Windows spooler/device/helper-process crash/restart and resource behavior run in representative Windows `SCHEDULED`/`PRE_RELEASE` environments where required.
- **7D — backup/restore:** an archive/upload success is never enough. The recovery set must be downloaded, decrypted through the intended key-recovery path, restored and validated. Restore is an `OPERATOR_DRILL` with explicit freshness/requalification after material schema/provider/key/deployment changes, plus `PRE_RELEASE` production-readiness evidence.
- **7E — integration:** provider bootstrap choices remain replaceable and no file/backup claim survives if its last relevant restore/integration evidence is stale after material change.

## Transitional contract

Object/file use may begin before the complete backup phase only when its durability/recovery promise is stated honestly. If an object is authoritative customer data, its recovery obligation follows immediately; Phase 7 placement does not permit unrecoverable authoritative bytes.

A print/document UI may exist before physical-print qualification, but printing must be disabled or explicitly non-production until its failure semantics are qualified. A printer failure never rolls back already committed business truth.

Do not call an uploaded archive a backup until an actual restore proof exists for the recovery claim.

## Hard invariants versus SLOs

Tenant/object ownership, integrity/hash/lifecycle correctness, no duplicate durable consumption where semantics say once, and recoverability claims are hard invariants. Transfer latency, document-generation latency and provider availability may use measured SLOs/error budgets when operation exists.