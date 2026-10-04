# Tenant settings, profiles, usage and permission administration

Task ID: WEB-012
Phase: 04-tenant-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: WEB-002, ADM-032, ADM-024, OPS-011, OPS-012, OPS-013
Conditional dependencies: ADM-027 when custom domains are selected; ADM-031 when resource-scoped roles are selected; ADM-033 when tenant channel opt-in is selected
Release requirement: REQUIRED

## Outcome

Expose qualified tenant Settings for team/roles, profiles, usage and branding, plus explicitly selected domain/release controls. Dedicated catalog/pricing and customer policy authoring belong to WEB-015 and WEB-016. Workstation device controls wait their separate future backend.

## Current basis and canonical inputs

Web runtimes remain NOT_INTRODUCED; folders do not prove implementation. Re-read `README.IMPLEMENTATION.md`; `docs/decisions/CURRENT_DECISIONS.md`; `docs/decisions/OPEN_DECISIONS.md`; `docs/admin/ADMIN_SURFACES.md`; `docs/security/TENANT_PERMISSIONS.md`; `docs/architecture/TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md`; `docs/requirements/RESOURCE_CONSUMPTION_AND_LIMITS.md`; `docs/web/CUSTOM_DOMAINS.md`. Follow `docs/development-tasks/AGENT_RULES.md` and `docs/development-tasks/HANDOFF_AND_INTEGRATION.md`.

## Scope and exclusions

Write the selected apps/web Settings pages and browser checks. No platform/operator authority, universal role editor, arbitrary scripts/plug-ins, generic config setter or assumed custom-domain backend is permitted.

## Decisions/prerequisites

Backend prerequisites: selected settings catalog; qualified tenant delegation ceilings/authorization-change reconciliation, profiles/config history, usage limits, device lifecycle and verified domains only for exposed pages. Supported rules require deliberate consent/publication and compatible running-work behavior. Implementation waits GATE-002; decisions may be prepared earlier.

## Acceptance and edge cases

- Tenant roles never grant platform privilege or exceed accepted delegation ceilings.
- Pending/unknown tuple changes are never labelled Applied before verification.
- Last-Owner protection and high-risk transfer use qualified guarded flows.
- Feature enablement grants no permission; dormant grants show effective re-enable diff.
- Profiles/rules show material effects and supported version/revalidation choices before publication.
- Usage is authoritative accounting, not sampled telemetry or automatic SaaS billing.
- Domains show verification/TLS states and safe callbacks; unsupported settings stay absent.
- Keyboard flow, focus, labels and announced validation/status errors cover accessibility basics.

## Security/static review

Review permission catalogs, property projections, secret configuration, CSRF and step-up needs. No OpenFGA/provider credentials enter the browser; low-risk settings do not acquire invented enterprise approvals.

## Dynamic verification and unavailable-environment handling

Run real backend/provider Playwright tests for delegation denial, tuple-write uncertainty, last-Owner protection, profile changes and selected domain lifecycle. Retain each introduced setting regression and omit unearned controls. Record commands, versions, inspected results and missing prerequisites. Never claim an unrun check passed; static review does not substitute for browser, provider or runtime evidence.

## Handoff

Provide a source-only ZIP, changed-file list, safe evidence and claim/guard/requalification mapping. No Git commit/push. Preserve unrelated Admin budget edits and product v0.1.0. Implementation is assigned later; this catalog does not authorize starting it.

## Assignable prompt

```text
Confirm the exact tenant settings scope.
Read each qualified settings/backend owner.
Build simple typed editors or earned purpose-built screens.
Keep tenant and platform authority separate.
Show diff, version, consent and pending outcomes.
Enforce delegated ceilings server-side through contracts.
Explain authoritative usage without inventing plans.
Test revocation, reconciliation and last-Owner safety.
Do not create generic role/rule/config engines.
Return source ZIP and settings-specific evidence.
```
