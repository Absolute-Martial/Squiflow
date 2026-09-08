# Comprehensive Adversarial Gap Review

**Version:** v0.0.15

**Purpose:** Challenge the current planning repository file-by-file, distinguish real omissions from deliberate deferrals, and prevent the documentation itself from creating false confidence.

This review is a review artifact. It does not override the master plan/current decisions. When a gap is accepted, the owning current document must be updated rather than leaving the fix only here.

## 1. Cross-cutting findings

### A. The repository is still pre-implementation

At this review point the repository contains architecture/planning documents, not the target `apps/`, `services/`, `modules/` or executable test/CI structure.

That is not itself a defect, but the documentation must not read as though Phase 0 has already been implemented or proven.

**Correction:** README/master/phase plan must explicitly distinguish target architecture from current repository state.

### B. Physical hardware assumptions were under-specified

`stateless/disposable` described process-state semantics but could be misread as automatic failover. Current owned lower-spec desktop-class rack hardware needs actual inventory, power/storage/network qualification and manual recovery planning.

**Correction:** `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md` now owns this concern.

### C. 100 GB object-storage capacity was not a real budget

The file/object plan previously placed customer artwork, documents, exports, backups and diagnostics into one conceptual object-store category without accounting for the current ~100 GB ceiling.

**Correction:** capacity classes, retention/quota/admission behavior and backup separation are now explicit.

### D. Accessibility was missing from the curated GitLab baseline

Earlier architecture work expected keyboard/focus/screen-reader/non-color/scalable-text quality, but the curated current docs dropped it.

**Correction:** accessibility is restored as a release-level requirement in `docs/ux/ACCESSIBILITY_AND_INTERACTION_QUALITY.md`.

### E. Test attacks existed without a test architecture

The phase plan listed many hostile cases but did not say which test layer/environment proves them or how real DB/hardware qualification differs from merge CI.

**Correction:** `docs/testing/VERIFICATION_STRATEGY.md` now owns this.

### F. Notifications/integrations were named but ownerless

Worker examples included notifications/integrations, but no durable delivery/retry/privacy/webhook boundary existed.

**Correction:** `docs/integrations/NOTIFICATIONS_AND_EXTERNAL_DELIVERY.md` defines the capability without adding a service.

### G. Documentation can drift even without CSV

Removing stale generated CSVs does not automatically solve source duplication. Repeated architecture paragraphs across Markdown can still diverge.

**Correction required:** each topic needs one canonical owning document; master/current decisions summarize and link instead of becoming independent detailed specifications.

### H. Planning can become the product

The repository has increasingly rigorous security/distributed-system review while no executable Phase 0 exists yet.

The remedy is not lower correctness. It is **just-in-time decision closure** and a WIP limit: close only the decisions required to start the next vertical slice, implement it, then use evidence to refine later decisions.

---

## 2. File-by-file review

### `README.md`

**Good:** curated navigation, current decisions visible, historical CSV issue explained.

**Gap:** must say explicitly that target runtime directories are not yet implementation evidence. New operations/accessibility/testing/integration docs need surfacing.

### `MASTER_IMPLEMENTATION_PLAN.md`

**Good:** coherent runtime boundaries, local-first/server authority, tenant isolation, Web-only control plane.

**Gaps:**
- no explicit current hardware/capacity envelope;
- Guard named without definition;
- accessibility absent;
- hardware/device integration too narrow/implicit;
- no explicit notification/integration boundary;
- cross-cutting Money/Currency/Quantity/Time primitives under-specified;
- no planning-stop/WIP rule;
- phase-start load-bearing decisions are not listed clearly.

### `docs/admin/ADMIN_SURFACES.md`

**Good:** clean tenant-admin vs platform-admin separation.

**Gaps:**
- application control plane recovery when Admin Web/Core API itself is unavailable;
- device lifecycle (lost/revoked/re-enrolled/version-incompatible) is mentioned only as a settings category;
- accessible high-risk diff/approval UX was not stated.

**Disposition:** infrastructure break-glass belongs in operations doc; accessibility belongs in UX doc; device lifecycle should be expanded when onboarding/device management vertical slice starts.

### `docs/api/API_CONTRACT_IDEMPOTENCY_AND_RETRY.md`

**Good:** strong semantic idempotency/retry contract.

**Gaps:**
- restore semantics: losing idempotency receipts while retaining the business effect can recreate duplicates;
- operation-status/idempotency retention needs restoration/backup linkage;
- resource/capacity limits were abstract rather than tied to actual low-end deployment.

**Disposition:** cover restore in testing/operations qualification; do not turn idempotency into another service.

### `docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`

**Good:** prevents Desktop/API exposure of platform controls.

**Gap:** `Platform Admin Web only` cannot be the only recovery path if that plane is itself down.

**Correction:** normal application control remains Web-only; private infrastructure break-glass recovery is a separate plane and runbook, not a hidden business API.

### `docs/architecture/MULTI_TENANCY_ISOLATION.md`

**Good:** pooled baseline, typed TenantContext, RLS defense-in-depth POC, bridge/silo evolution without prebuilding it.

**Gaps:**
- per-tenant deletion/export/restore semantics in a pooled DB need explicit product/support policy;
- tenant scope must also apply to telemetry/diagnostics, not only data/search/jobs/objects;
- future dedicated placement must preserve upgrade/support compatibility.

**Disposition:** keep pooled baseline; add these to production qualification rather than building dedicated routing now.

### `docs/architecture/REPOSITORY_STRUCTURE.md`

**Good:** discourages placeholder projects and mechanical layering.

**Gaps:**
- target tree can be mistaken for existing implementation;
- Guard is named but not defined;
- `packages/` vs `contracts/` could drift into overlapping dumping grounds.

**Correction:** define creation criteria and Guard; keep wire-version contracts separate from reusable implementation primitives.

### `docs/data/FILES_AND_OBJECT_STORAGE.md`

**Good:** object/metadata asymmetry and printing separation are correctly recognized.

**Gaps:**
- current 100 GB capacity/tenant usage/retention/admission absent;
- backup was mixed into primary durable-object examples;
- upload bandwidth/resume policy absent;
- staged-file cleanup/disk exhaustion under-specified;
- quarantine/content-security policy under-specified;
- long-offline Workstation references versus GC need a retention rule.

**Correction:** operations doc owns capacity/backup; this file should be patched with storage classes and safe staging/security behavior.

### `docs/data/PERSISTENCE_SELECTION.md`

**Good:** central/local provider decisions remain genuinely open; pooled isolation requirements are now real.

**Gaps:**
- hardware durability qualification (power/disk-full/replacement restore) was too generic;
- DB connection pool/admission/resource envelope needs actual-node benchmark;
- backup/restore must include object/idempotency/job consistency, not DB alone.

### `docs/decisions/CURRENT_DECISIONS.md`

**Good:** current direction is explicit.

**Gap:** hardware reality, accessibility, Guard/device boundary, test architecture and 100 GB storage ceiling were absent from accepted baseline.

**Correction:** add only accepted requirements, not guessed thresholds/provider choices.

### `docs/decisions/OPEN_DECISIONS.md`

**Good:** deferred items are separated from active blockers.

**Gaps:**
- actual server hardware inventory/power-protection topology;
- final backup target plus RPO/RTO;
- formal accessibility conformance/jurisdiction target;
- first supported printer/device integration matrix;
- SaaS tenant commercial billing/metering model versus manual entitlement management;
- initial client-portal implementation timing;
- onboarding/import migration scope;
- exact dynamic-form definition contract.

### `docs/domain/BUSINESS_MODEL.md`

**Good:** practical small-shop domain avoids invented MRP/reservation/wastage complexity.

**Gaps that are expensive to retrofit:**
- Money/Currency/Rounding;
- Quantity/Unit;
- Time/Timezone/business date;
- numbering/issued-document policy;
- duplicate Party/contact merge/correction;
- privacy/retention lifecycle;
- external notification/integration consequences;
- data import/migration path.

**Correction:** define common primitives before broad modules, while keeping jurisdiction-specific tax/numbering policy open.

### `docs/implementation/PHASES_AND_GATES.md`

**Good:** vertical sequence, hostile cases and deferral discipline are strong.

**Gaps:**
- no explicit WIP limit/planning stop condition;
- no phase-start decision closure list;
- hardware/capacity/backup/accessibility/peripheral qualification was late or absent;
- test-layer ownership was absent;
- client-portal/notification/import timing unclear.

**Correction:** patch phases rather than adding parallel project tracks.

### `docs/observability/OBSERVABILITY.md`

**Good:** OTel remains provider-neutral and business correctness does not depend on export.

**Gaps:**
- free-tier ingest/retention/quota assumptions were not bounded;
- three managed providers increase operational/account/secret complexity;
- no explicit telemetry drop/sampling/local-spool capacity policy;
- tenant/private-data redaction and tenant-isolation tests need more detail;
- support-facing symptom → evidence → probable-cause path is under-specified.

**Correction:** keep managed providers, but treat quotas/retention as capacities that can exhaust.

### `docs/review/CSV_AUDIT_AND_SOURCE_CLEANUP.md`

**Good:** correctly removes stale CSVs from design authority.

**Gap:** Markdown duplication can fail in the same way.

**Correction:** add canonical-topic ownership rule and keep review docs subordinate to current design docs.

### `docs/review/RELIABILITY_API_AND_PATTERN_SOURCE_REVIEW.md`

**Good:** catalog patterns are classified instead of blindly adopted.

**Gap:** hardware-specific `vertical/single-node first` must not imply cloud-style node replacement or unlimited storage/network. `Sidecar` reference to Guard needs a real definition.

### `docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md`

**Good:** source-backed OIDC/OWASP/Zanzibar/.NET mapping is careful and avoids overbuilding.

**Gap:** no major architecture flaw found here; main risk is that source-review prose duplicates current auth docs and later drifts.

**Disposition:** source review remains evidence/traceability, not implementation authority.

### `docs/review/SKEPTICAL_IMPLEMENTATION_GATES.md`

**Good:** strong how/why/what-if questions.

**Gaps:** hardware/power/capacity/bandwidth, accessibility, operability, peripheral failure, testing evidence and documentation ownership need their own questions.

### `docs/rules/NATIVE_RULE_ENGINE.md`

**Good:** safe bounded native rule model; no tenant arbitrary code.

**Gap:** local Workstation evaluation must classify rule fact freshness/authority. A rule requiring current credit exposure or globally shared stock cannot be treated like a purely local formatting/approval-threshold rule just because a snapshot exists.

**Correction required:** published rule metadata/evaluation context declares whether required facts are LocalSafe, Provisional or ServerRequired; server revalidates authoritative effects.

### `docs/security/IDENTITY_AND_SESSIONS.md`

**Good:** OIDC/PKCE/account key/logout boundaries are strong.

**Gaps:**
- device credential rotation/expiry/revocation/re-enrollment lifecycle needs design;
- bootstrap/recovery when no usable Owner remains needs an explicit support/security path;
- shared Windows PC/user-profile behavior should be tested so one user cannot inherit another user's local session/data accidentally.

### `docs/security/TENANT_PERMISSIONS.md`

**Good:** owner-controlled granular authorization with revision/freshness and resource/property checks.

**Gaps:**
- exact multiple-role combination semantics should be explicit (baseline can remain allow-union with no explicit Deny if that is selected);
- `Own/Assigned` semantics must be defined per resource, not a generic magic scope;
- tenant Owner recovery/transfer when the only Owner is unavailable needs a controlled path.

### `docs/server/CORE_API_AND_WORKER.md`

**Good:** clear API/Worker separation, durable work and external-effect safety.

**Gaps:**
- resource budgets need actual hardware values/benchmarks;
- scheduler occurrence/idempotency ownership remains abstract;
- notifications/integrations were named without a current delivery contract;
- Platform Admin Web outage requires separate infrastructure recovery;
- helper/native process lifecycle should connect to the defined Guard boundary.

### `docs/sync/SYNC_AND_AUTHORITY.md`

**Good:** durable local transaction/cursor/idempotency/conflict authority is clear.

**Gaps:**
- resnapshot/backlog transfer can overwhelm low-bandwidth server/site;
- local DB/staging disk capacity and outbox growth need hard limits/recovery UX;
- tombstone/compaction retention needs to be compatible with supported offline duration;
- large attachment transfer should be prioritized separately from small semantic sync.

### `docs/web/CUSTOM_DOMAINS.md`

**Good:** ownership/TLS/fallback/audit baseline.

**Gaps:**
- custom branding must preserve accessibility/readability;
- domain removal/reassignment needs anti-takeover/callback cleanup behavior;
- client portal security/audience needs its own implementation gate when introduced.

### `docs/web/WEB_RUNTIME_AND_STORAGE.md`

**Good:** removes unnecessary browser-offline complexity.

**Gap:** online-only need not mean valuable form input survives only in RAM. For long/valuable forms, server-side drafts/autosave can improve crash/refresh/session-expiry recovery without IndexedDB/offline sync.

**Correction:** allow explicit online server-side drafts where the journey justifies them.

### `docs/workflow/WORKFLOW_DESIGN.md`

**Good:** continuation ownership, durable waits and version pinning are strong.

**Gaps:**
- deleting/renaming a configured stage while old instances reference it needs archived-definition display semantics;
- external notification is not the same as work ownership and should remain optional;
- offline transition eligibility must connect to local authority/fact freshness;
- two-person tenant approval/delegation must avoid deadlock if the only approver is unavailable.

### `docs/workstation/LOCAL_FIRST_DESKTOP.md`

**Good:** local-first principles are adapted rather than blindly applying CRDT/peer authority.

**Major omissions:**
- Guard is named elsewhere but not defined here;
- printer/device integration boundary is not defined;
- local disk/staging quota and cleanup are under-specified;
- shared-PC/user switching/session expiry behavior needs tests;
- update/restart with pending unsynced work needs a hard preservation rule;
- accessibility is absent;
- hardware sleep/driver/spooler/helper hostile cases are incomplete.

**Correction:** patch this document; printer is baseline, other peripherals remain requirement-driven.

### Root `VERSION` / `CURRENT_VERSION.txt`

No content issue. Keep these machine-simple and avoid putting architecture prose into them.

---

## 3. Deliberately deferred rather than forgotten

The following are not implementation work merely because this review names them:

- full Web offline/PWA business sync;
- per-tenant database/stack/queue by default;
- CRDT-wide business model;
- Kafka/event sourcing/full CQRS/Saga baseline;
- scanner/barcode/cash-drawer support without a confirmed customer journey;
- dedicated notification microservice;
- formal multi-region/active-active before RPO/RTO requires it;
- complex tenant SaaS billing engine before commercial model requires it;
- arbitrary tenant-specific schema/DDL.

## 4. Planning stop rule

Architecture review is useful only if it reduces implementation risk.

For each phase:

1. close only the decisions required to start that phase;
2. implement one complete vertical slice;
3. attack it with the phase/test gates;
4. record measured evidence;
5. revise later architecture only when the evidence changes an assumption.

Do not postpone Phase 0 because every future enterprise decision is not yet solved.
