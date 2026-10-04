# WEB-016 — Customer policy, forms and approval configuration screens

Task ID: WEB-016
Phase: 04-tenant-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: WEB-002, COM-011, COM-012, ADM-021
Release requirement: REQUIRED

## Outcome

Expose the one accepted customer/program guidance variation with typed supplementary fields, approval configuration and deliberate versioned publication. Work discovery in WEB-006 remains its distinct operator flow.

## Current basis and canonical inputs

Read [workflow](../../workflow/WORKFLOW_DESIGN.md), [rules](../../rules/NATIVE_RULE_ENGINE.md), [profiles](../../architecture/TENANT_APPLICATION_PROFILES_AND_EXTENSIBILITY.md), COM-011/012 handoffs, [current truth](../../../README.IMPLEMENTATION.md), [rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md).

## Scope and exclusions

Allowed areas: apps/web purpose-built configuration/preview/publication/history screens for the accepted variation and browser regressions. Split edit/preview and publish/running-work effects into reviewed slices if needed. No generic visual workflow designer, uploaded scripts/SQL, universal form builder or financial effects defined by UI stages.

## Decisions/prerequisites

Confirm supported typed schema, applicability/precedence, publishing actor, compatible version changes, approval/continuation owner and grandfather/revalidate/migrate rules. The backend defines every policy effect. GATE-002 is inherited from WEB-002; no absent policy is simulated for UI completeness.

## Acceptance and edge cases

- Tenant/customer/program scope and effective version are visible and server-resolved.
- Author can supply only accepted field types/options, not executable expressions.
- Preview explains missing information and required next actor using the server evaluator.
- Material publication shows accepted running-work and permission effects before confirmation.
- An unavailable approver has the qualified reassignment/continuation path, never a UI force-approve bypass.
- Repeated publish, stale editor and changed policy versions preserve durable historical interpretation.
- Keyboard/focus/error announcements and encoded supplementary text remain usable and safe.

## Security/static review

Review author/publisher/approver separation, script/template injection, tenant/customer selectors, hidden form fields, session/CSRF and the limits of preview guidance.

## Dynamic verification and unavailable-environment handling

Run `./eng/verify.sh` and actual Playwright/API/provider cases for author→preview→publish→new work, in-flight old work, stale revision and unauthorized publishing. Use real configured evaluator results; mocked stages cannot prove admission. Report exact unrun checks.

## Handoff

Return source ZIP/hash, selected schema/contract versions, screenshots with synthetic data, browser evidence, preserved historical cases and blockers. WEB-014 must include this configuration-to-execution journey.

## Assignable prompt

```text
Execute WEB-016 only for the accepted customer/program variation.
Inspect policy, form, approval and publication contracts first.
Build purpose-built typed editors and server-backed previews.
Show scope, version and effects on running work before publish.
Keep authoring, publishing, approval and execution authority separate.
Test in-flight history, stale edits and unavailable-approver recovery.
Review injection, hidden fields, tenant scope and session/CSRF.
Return source ZIP, hashes and real browser/provider evidence.
Do not commit or create a general workflow/form engine.
```
