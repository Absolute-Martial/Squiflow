# Phase 01-admin-platform task index

Generated from full task metadata. Read the complete file and accepted dependency handoffs before dispatch.

| Task | Status | Model | Release | Dependencies |
|---|---|---|---|---|
| [ADM-001 — Qualify existing membership and tenant lifecycle](ADM-001_qualify-existing-membership-and-tenant-lifecycle.md) | VERIFY_EXISTING | GPT-6.1 Sol | REQUIRED | BAS-001 |
| [ADM-002 — Qualify incoming protected request budgets](ADM-002_qualify-incoming-protected-request-budgets.md) | VERIFY_EXISTING | GPT-6.1 Sol | REQUIRED | ADM-001 |
| [ADM-003 — Review route access readiness and admission gaps](ADM-003_review-route-access-readiness-and-admission-gaps.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | ADM-002 |
| [ADM-004 — Decide and qualify live ZITADEL topology](ADM-004_decide-and-qualify-live-zitadel-topology.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | ADM-002 |
| [ADM-005 — Implement selected provider onboarding operation](ADM-005_implement-selected-provider-onboarding-operation.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | ADM-004, OPS-003, OPS-018 |
| [ADM-006 — Expose bounded platform registry reads](ADM-006_expose-bounded-platform-registry-reads.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-002, GATE-001 |
| [ADM-007 — Decide account suspension and identity recovery lifecycle](ADM-007_decide-account-suspension-and-identity-recovery-lifecycle.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | ADM-004, ADM-006, WEB-001 |
| [ADM-008 — Decide Owner Staff and delegation contract](ADM-008_decide-owner-staff-and-delegation-contract.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | ADM-001, ADM-004 |
| [ADM-009 — Persist one tenant grant proposal and status](ADM-009_persist-one-tenant-grant-proposal-and-status.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-008, ADM-002, GATE-001 |
| [ADM-010 — Apply and reconcile one permission grant](ADM-010_apply-and-reconcile-one-permission-grant.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-009 |
| [ADM-011 — Reconcile revocation and guarded Owner handoff](ADM-011_reconcile-revocation-and-guarded-owner-handoff.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-010, ADM-034 |
| [ADM-012 — Add one tenant custom role lifecycle](ADM-012_add-one-tenant-custom-role-lifecycle.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-011, ADM-008 |
| [ADM-013 — Decide first resource permission scope](ADM-013_decide-first-resource-permission-scope.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | ADM-008, ADM-012 |
| [ADM-014 — Introduce narrowly scoped platform operator lifecycle](ADM-014_introduce-narrowly-scoped-platform-operator-lifecycle.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | ADM-002, ADM-010, ADM-034 |
| [ADM-015 — Enroll one principal-bound Admin device](ADM-015_enroll-one-principal-bound-admin-device.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-014, OPS-015, ADM-034 |
| [ADM-016 — Rotate and revoke registered Admin certificates](ADM-016_rotate-and-revoke-registered-admin-certificates.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-015, ADM-017, OPS-018 |
| [ADM-017 — Define and drill Admin access recovery ceremony](ADM-017_define-and-drill-admin-access-recovery-ceremony.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | ADM-014, OPS-015 |
| [ADM-018 — Admit a real feature catalog and permission metadata](ADM-018_admit-a-real-feature-catalog-and-permission-metadata.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | ADM-008, GATE-001 |
| [ADM-019 — Persist one typed setting revision](ADM-019_persist-one-typed-setting-revision.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-018 |
| [ADM-020 — Publish one immutable tenant profile revision](ADM-020_publish-one-immutable-tenant-profile-revision.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-019, ADM-009 |
| [ADM-021 — Activate and roll back a published profile](ADM-021_activate-and-roll-back-a-published-profile.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-020, ADM-011 |
| [ADM-022 — Acquire tenant runtime only for a proven variant](ADM-022_acquire-tenant-runtime-only-for-a-proven-variant.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | ADM-021 |
| [ADM-023 — Provide authoritative administrative audit reads](ADM-023_provide-authoritative-administrative-audit-reads.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-006, ADM-010, ADM-020, OPS-013 |
| [ADM-024 — Publish bounded tenant branding overrides](ADM-024_publish-bounded-tenant-branding-overrides.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-021, WEB-001 |
| [ADM-025 — Implement local account availability commands](ADM-025_implement-local-account-availability-commands.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-007, ADM-006, ADM-002, GATE-001 |
| [ADM-026 — Implement one approved identity recovery operation](ADM-026_implement-one-approved-identity-recovery-operation.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | ADM-007, ADM-025, ADM-004, ADM-034, OPS-018 |
| [ADM-027 — Conditional verified custom-domain lifecycle](ADM-027-conditional-verified-custom-domain-lifecycle.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | ADM-021, ADM-010, OPS-015, WEB-001 |
| [ADM-028 — Conditional encryption policy and key-lifecycle contract](ADM-028-conditional-encryption-policy-and-key-lifecycle-contract.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | ADM-014, ADM-034, OPS-015 |
| [ADM-029 — Conditional one provider-owned key-lifecycle operation](ADM-029-conditional-one-provider-owned-key-lifecycle-operation.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | ADM-028, OPS-003, OPS-018 |
| [ADM-030 — Conditional time-bounded tenant support access](ADM-030-conditional-time-bounded-tenant-support-access.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | ADM-014, ADM-034, OPS-013 |
| [ADM-031 — Conditional first resource-permission enforcement](ADM-031-conditional-first-resource-permission-enforcement.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | ADM-013, ADM-011 |
| [ADM-032 — Tenant-authorized team and membership API](ADM-032-tenant-authorized-team-and-membership-api.md) | READY_AFTER_DEPENDENCIES | GPT-6.1 Sol | REQUIRED | ADM-012, ADM-025, ADM-034 |
| [ADM-033 — Conditional bounded feature-release controls](ADM-033-conditional-bounded-feature-release-controls.md) | CONDITIONAL | GPT-6.1 Sol | CONDITIONAL | ADM-021, ADM-018, OPS-021 |
| [ADM-034 — Backend high-risk action and step-up contract](ADM-034-backend-high-risk-action-and-step-up-contract.md) | DECISION_REQUIRED | GPT-6.1 Sol | REQUIRED | ADM-004, WEB-001, UIA-001, GATE-001 |

Conditional prerequisites and approved writable areas are in the full task and root tasks.json.
