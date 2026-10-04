# WEB-015 — Catalog and pricing-policy authoring screens

Task ID: WEB-015
Phase: 04-tenant-web
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: WEB-002, COM-005, COM-007, COM-008
Release requirement: REQUIRED

## Outcome

Let authorized tenant operators maintain the supported catalog and publish/view price policies used by order entry. A sales draft editor alone does not provide administration of its source catalog/policies.

## Current basis and canonical inputs

Read [pricing owner](../../implementation/PRICING_COMPONENT_BOUNDARY.md), [business map](../../implementation/BUSINESS_OPERATION_END_TO_END.md), COM-005/007/008 accepted contracts, [current truth](../../../README.IMPLEMENTATION.md), [rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md).

## Scope and exclusions

Allowed areas: apps/web catalog browse/detail/availability and selected price policy editor/publication/history screens with browser tests. Dispatcher chooses one screen/flow per bounded slice. Existing server calculation/selection contracts remain authoritative. No browser money engine, unit-conversion framework, supplier-cost editor, arbitrary formulas or new backend endpoints.

## Decisions/prerequisites

GATE-002 is inherited from WEB-002. Confirm exact catalog fields, price source precedence, validity/currency/quantity rules, publish/override permissions and semantic retry contracts. Use purpose-built editing for policy-changing fields; fixed-contract display components may be assigned to Luna/Flash after freezing their inputs.

## Acceptance and edge cases

- Catalog entry and free-description behavior match accepted backend semantics and supported units.
- Price policy edits show draft/published versions, affected customer/program scope and explicit validity.
- Publish conflicts/stale revisions cannot overwrite another operator's accepted policy.
- Server-provided source/explanation and override ceiling are presented without local authoritative arithmetic.
- Historical issued prices remain unchanged after availability/policy changes.
- Session expiry, denied permissions, response loss and form validation show recoverable accurate state.
- Encoded content, keyboard navigation, focus and labels support usable ordinary operation.

## Security/static review

Review tenant scope, publication versus pricing authority, excessive DTO fields, encoded labels, session/CSRF and hidden-button bypass. No supplier/internal secret data enters a presentation-only field.

## Dynamic verification and unavailable-environment handling

Run `./eng/verify.sh` and actual Playwright/provider cases for create/edit/availability/publish, conflicting editors, denied policy authority and a draft consuming the new version. Register exact browser commands when the host/test runner is introduced; do not invent currently absent script names. Missing checks remain pending.

## Handoff

Return source ZIP/hash, changed paths, exact backend contract versions, screenshots using synthetic data, browser evidence and blockers. WEB-014 includes these administration flows.

## Assignable prompt

```text
Execute WEB-015 for one selected catalog/pricing authoring flow.
Inspect qualified catalog, price policy and override contracts.
Build tenant-authorized editors and explicit publication/history views.
Render server source/explanations without browser money authority.
Preserve revisions, stable retry keys and immutable issued facts.
Test conflicting editors, denied publishing and changed policy use.
Review encoded content, tenant scope and session/CSRF behavior.
Return source ZIP, hashes and exact real-browser evidence.
Do not commit or invent backend pricing/catalog behavior.
```
