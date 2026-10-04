# ADM-027 — Conditional verified custom-domain lifecycle

Task ID: ADM-027
Phase: 01-admin-platform
Status: CONDITIONAL
Model: GPT-6.1 Sol
Dependencies: ADM-021, ADM-010, OPS-015, WEB-001
Conditional dependencies: OPS-003 when DNS/certificate reconciliation runs automatically; OPS-005 when renewal uses scheduled occurrences
Release requirement: CONDITIONAL

## Outcome

Provide one supported tenant hostname lifecycle with verified ownership, durable mapping, TLS and safe identity callbacks when custom domains are selected for the release.

## Current basis and canonical inputs

Read [current truth](../../../README.IMPLEMENTATION.md), [custom domains](../../web/CUSTOM_DOMAINS.md), [identity](../../security/IDENTITY_AND_SESSIONS.md), [shared rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md). Domain architecture is accepted; runtime persistence/routing is absent.

## Scope and exclusions

Allowed areas: one domain-owned normalized registration/verification/activation/removal contract, PostgreSQL state, tenant-authorized CoreApi adapters and reviewed edge/IdP configuration integration. Name exact approved paths before editing. Keep platform operational credentials in adapters. Exclude a portal, DNS hosting platform and arbitrary callback registration.

## Decisions/prerequisites

Owner selects the actual hostname/edge/certificate provider, verification proof, renewal and reassignment policy, callback lifecycle and fallback origin. Unselected scope remains absent with a reason; a text box in Settings is not domain implementation.

## Acceptance and edge cases

- Normalized hostname uniqueness is enforced under concurrent tenant registrations.
- Typed hostname and Host/forwarded-host headers never grant membership or business access.
- Active requires confirmed ownership, routing and valid TLS; incomplete results remain pending.
- Removal stops routing and retires identity callbacks before reassignment; fresh ownership proof is mandatory.
- Renewal/reconciliation retries preserve one semantic registration and safe fallback access.
- Current tenant permission is checked on first attempts and replay; no tenant acquires Admin origin authority.

## Security/static review

Review trusted proxies, DNS/redirect destinations, domain takeover, callback allowlists, credential scope and safe error projection. Inspect the entire domain/edge/IdP path.

## Dynamic verification and unavailable-environment handling

Run `./eng/verify.sh` plus real DNS/TLS/edge/IdP tests against an authorized test domain: collision, stale proof, expiry, removal/reassignment and fallback login. Add permanent state/race guards. Missing provider/domain access leaves those exact checks pending; local fakes cannot qualify ownership or TLS.

## Handoff

Return the complete source-only change, SHA-256, exact domain/provider test commands, sanitized evidence, changed paths, blockers and owner-accepted activation policy. WEB-012 consumes the qualified contract later.

## Assignable prompt

```text
Execute only ADM-027 after custom domains are explicitly selected.
Read current domain, identity and deployment owners.
Confirm one hostname/edge/certificate/callback contract with the owner.
Implement bounded registration, verification, activation and safe removal.
Preserve tenant authority and fallback access independently of Host input.
Test concurrency, renewal and domain reassignment with real boundaries.
Review callbacks, proxies, credentials and takeover failure cases.
Return source ZIP, hashes, exact evidence and pending checks.
Do not commit, broaden scope or label unverified TLS as active.
```
