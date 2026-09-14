# Phase 1E — Integrated Phase-1 Production-Honesty Gate

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Production intent

After Phase 1 passes, a real user can authenticate through the declared Web/Workstation flows and SquiFlow can make current tenant/application authorization decisions for the implemented surfaces without relying on forged client tenant context, stale provider assumptions, UI hiding, unreconciled authorization changes, or incomplete transitional session/security behavior.

## Scope contract

Before sign-off, classify every introduced Phase-1 identity/authorization/session/device responsibility as:

```text
NOT_INTRODUCED
PRODUCTION_HONEST
BLOCKED
```

`BLOCKED` must be empty. Future platform authorization, stronger step-up/MFA policy, broader device lifecycle, or other unintroduced breadth may remain `NOT_INTRODUCED`; a reachable protected operation with deferred auth/session/tenant correctness may not.

## Gate conditions

Phase 1 passes for its declared scope when:

- ZITADEL authenticates Web and Workstation through the accepted flows that are actually introduced;
- SquiFlow maps identity by `(issuer, subject)`;
- TenantContext comes from server-authoritative membership/resource context rather than request data;
- OpenFGA decisions are pinned/model-aware and remain separate from SquiFlow business validation;
- Owner/Staff plus at least one real custom-role/permission flow is proven where custom roles are in declared scope;
- device enrollment/revocation semantics needed by current Workstation behavior are explicit;
- Web session/CSRF/output-security basics are proven for implemented surfaces;
- provider outages and authorization changes fail safely;
- cross-system authorization changes reconcile correctly;
- authorization changes that overlap active operations follow an explicit tested decision point;
- all transitional restrictions from 1A→1B→1C→1D are either still mechanically enforced or explicitly closed by qualified behavior;
- active capability development can continue without provider SDK leakage into host-neutral code.

## Evidence requirement

The gate evidence must name the implemented login/session/authorization flows and include applicable hostile/negative proof such as:

- wrong issuer/audience;
- PKCE/state/nonce/callback tampering as applicable;
- cross-tenant identifiers;
- revoked permission/session/device behavior;
- provider outage;
- ambiguous tuple/application-change outcome;
- authorization-change overlap with protected business operations;
- CSRF/XSS/input/output-sensitive behavior for real exposed surfaces;
- sign-out/session-expiry/network interruption outcomes for Workstation behavior that actually exists.

Do not infer production honesty from mocked provider calls alone when the claim depends on ZITADEL/OpenFGA/ASP.NET behavior.

## Permanent regression requirement

The evidence record must name which checks remain:

```text
PER_MR
SCHEDULED
PRE_RELEASE
```

or another accepted cadence from the evidence/permanence owner.

Phase 1 cannot pass on the basis of one successful provider demo or one manual hostile review. Cheap security/tenant/session tests remain blocking; provider/process scenarios that cannot run continuously retain a recurring cadence and requalification trigger.

## Carry-forward rule

Only genuinely `NOT_INTRODUCED` items may be carried forward.

For every material security deferral record:

```text
behavior while absent
source of that behavior/default
why it is safe for current reachable scope
evidence enforcing/testing that absence behavior
trigger/latest gate
regression guard
```

Examples such as exact production MFA/step-up policy, Admin-device registration, offline permission-snapshot retention, or provider recovery procedures are deferrable only when the current behavior in their absence is deliberate and tested. Accidental provider/framework defaults are not sufficient.

## Completion meaning

Passing Phase 1 means the declared trust foundation is `PRODUCTION_HONEST` and protected against silent regression for current use. It does not mean identity/authorization is finished forever. New breadth re-enters the same gate model when introduced.