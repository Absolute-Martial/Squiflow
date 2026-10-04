# OPS-015 — Reproduce the first secure deployment profile

Task ID: OPS-015
Phase: 03-runtime-operations
Status: READY_AFTER_DEPENDENCIES
Model: GPT-6.1 Sol
Dependencies: GATE-001, ADM-004, WEB-001, UIA-001
Release requirement: REQUIRED
Cross-track prerequisites: ADM-004, WEB-001, UIA-001

## Outcome

Rebuild one actual backend deployment profile from version-controlled definitions and non-secret configuration, including real identity dependencies and private Admin ingress.

## Current basis and canonical inputs

Read [implementation truth](../../../README.IMPLEMENTATION.md), [source admission](../../review/APPLICATION_BASELINE_IMPLEMENTATION_SOURCE_REVIEW.md), [shared agent rules](../AGENT_RULES.md), and [DEPLOYMENT_CAPACITY_AND_RECOVERY](../../operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md), [IDENTITY_AND_SESSIONS](../../security/IDENTITY_AND_SESSIONS.md), [OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE](../../security/OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE.md). These are planning assignments, not evidence of running infrastructure. Current normal qualification, including ADM-001 and ADM-002, precedes new implementation; historical results do not qualify the current checkout.

## Scope and exclusions

Allowed areas: deploy definitions/runbooks, existing host startup/config validation, secret-provider integration and deployment smoke tests. Exclude Kubernetes/HA promises, provisioning imagined hosts and choosing paid services without an accepted decision. Keep neutral `Application.*` identities and product version `v0.0.1`. No empty future projects or unrelated changes.

## Decisions/prerequisites

Accept topology, operator, DNS/edge/proxy trust, TLS issuance, clock monitoring and secret supply/rotation. ADM-004 owns real ZITADEL topology; private Admin device certificates must be validated end to end rather than trusted forwarded text.

## Acceptance and edge cases

- A clean/replacement environment starts the same verified bytes with validated configuration and no manual hidden machine state.
- Public CoreApi and private AdminApi exposure match the declared routing/access profile.
- OIDC discovery/audience/model configuration and exact client-certificate chain validation work against real dependencies.
- Untrusted forwarded headers/client certificates cannot spoof scheme, identity or device authority.
- TLS expiry/renewal, DNS/edge outage and clock-skew conditions have observable safe failure/recovery behavior.
- Secrets rotate without source/artifact/log disclosure; authorized smoke journeys prove useful service beyond health status.

## Security/static review

Review network rules, trusted proxies, TLS and least-privilege process/filesystem permissions; review selected secret/key mechanism and private recovery path. Inspect the complete changed dependency and authority path; redact credentials and sensitive content from errors, logs and artifacts.

## Dynamic verification and unavailable-environment handling

Provision a disposable clean environment from the definitions; execute live IdP/OpenFGA/Admin certificate and edge/clock/rotation drills with sanitized evidence. Run applicable focused checks and the normal `./eng/verify.sh` without masking parallel failures. If required tooling/provider access is unavailable, report the exact missing evidence and keep introduced claims `BLOCKED`; never substitute mocks for the property.

## Handoff

Follow [handoff/integration rules](../HANDOFF_AND_INTEGRATION.md). Supply a source-only ZIP, changed-file inventory, exact commands/results, static-review findings, known non-claims, and claim/evidence/regression/requalification mapping. Exclude secrets, SDKs and build outputs. Do not commit or push.

## Assignable prompt

```text
Implement only OPS-015: Reproduce the first secure deployment profile.
Read the linked current owners and shared rules before editing.
Confirm dependencies and the accepted initial workload/profile.
Qualify one reproducible secure backend profile, including real IdP connectivity.
Preserve capability authority and neutral Application.* identities.
Add focused permanent regression evidence for each material claim.
Run exact dynamic checks and inspect their complete results.
Report unavailable checks and blockers without qualifying them.
Deliver the source-only ZIP and reviewable integration handoff.
Do not commit, push, or expand unrelated responsibilities.
```
