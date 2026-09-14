# Phase 1 — Identity, Tenant Authorization, and Session Foundation

**Parent owner:** `docs/implementation/PHASES_AND_GATES.md`  
**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Purpose

Phase 1 establishes the first production-honest trust boundary for real users and tenants. It does not freeze business-module development: Customers, Orders, Workstation, Web, CoreApi, Guard, observability, deployment, and tests may all continue evolving while the Phase-1 security foundation is introduced.

A capability does not have to wait for Phase 1 to exist. Phase 1 is the point where externally reachable authenticated behavior can rely on the qualified identity/tenant-authorization/session foundation rather than test identities or UI-only checks.

## Phase production intent

After Phase 1 passes, a real tenant user can use the declared Web/Workstation authenticated surfaces and rely on stable identity binding, server-derived tenant context, current application authorization, safe session behavior, and reconciled authorization changes; provider failure or ambiguous state must not silently grant authority.

## Subphases

```text
1A  ZITADEL identity and account binding
1B  TenantContext, OpenFGA authorization, roles and devices
1C  Web/Workstation session and surface security
1D  Authorization change reconciliation, failure and degraded modes
1E  Integrated Phase-1 production-honesty gate
```

## Phase maturity added

After Phase 1, externally reachable business behavior may rely on a real authenticated identity and current application authorization model rather than test identities or UI-only role checks.

## Components that may continue/add

- any real capability module and its Web/Workstation/API adapter;
- tenant/account/device persistence needed by the trust model;
- authorization-sensitive application use cases;
- observability/audit evidence for identity/authorization paths;
- deployment/configuration required for ZITADEL/OpenFGA;
- tests against real isolated provider environments.

## Earned-only additions

Do not introduce Platform Admin, Worker, SyncApi, broker, service mesh, GraphQL, or new auth systems merely because identity/authorization now exists.

## Sustainability

Provider SDK types stay behind infrastructure/host adapters. `(issuer, subject)` and SquiFlow-owned permission/tenant semantics remain stable even if identity/authorization providers are replaced later.

## Standards grounding

Identity/OAuth implementation claims must be grounded in the standards actually used by the selected topology, not the phrase `production-shaped`.

For the accepted Workstation browser flow and OAuth security baseline, the relevant guidance includes:

- OAuth 2.0 Security Best Current Practice, RFC 9700;
- OAuth 2.0 for Native Apps, RFC 8252;
- PKCE, RFC 7636;
- OpenID Connect Core for OIDC-specific issuer/subject/nonce/token semantics.

RFC 9449 is DPoP and is **not** a generic browser-app requirement. Sender-constrained tokens such as DPoP/mTLS are adopted only if the selected threat model/topology earns them.

Provider libraries do not replace explicit validation of the guarantees SquiFlow depends on.

## Phase-specific evidence and regression map

- **1A — identity/account binding:** `SECURITY_HOSTILE + INTEGRATION`. Permanent checks cover issuer/audience, transaction/state/nonce/PKCE semantics as applicable, exact/approved redirect behavior, callback tampering, expired/replayed transaction behavior, stable `(issuer, subject)` mapping, provider outage, and secret/client configuration. Protocol validation tests run `PER_MR`; real isolated ZITADEL integration runs `SCHEDULED` and `PRE_RELEASE` where provider execution on every MR is impractical.
- **1B — tenant authorization/roles/devices:** `SECURITY_HOSTILE + INTEGRATION`. Permanent tests cover spoofed TenantId, cross-tenant resource IDs, wrong/past authorization model, revoked role/device, provider unavailable, tenant-vs-platform scope confusion, and DB/resource isolation remaining independent from OpenFGA allow. Cheap policy/tenant tests run `PER_MR`; real OpenFGA integration remains recurring.
- **1C — session/surface security:** `SECURITY_HOSTILE + INTEGRATION`. Permanent checks cover CSRF where cookie-backed, output/XSS behavior, field allow-lists/mass assignment, logout/expiry, secrets in client/logs, session/circuit restart, and Workstation sign-out/network behavior for any local state that exists. These checks block regression on every relevant MR.
- **1D — authorization change reconciliation:** `INTEGRATION + PROCESS_FAILURE`. Permanent tests cover response loss, provider timeout/ambiguous outcome, tuple success followed by local completion failure, process crash between durable steps, stale consistency reads, retry identity, and model revision changes. Cheap state-machine tests run `PER_MR`; real provider/process fault injection is recurring and pre-release as appropriate.
- **1E — integration:** cannot pass until all Phase-1 introduced claims name evidence plus regression cadence and no security responsibility remains `BLOCKED`.

## Transitional security contract

Subphase order does **not** license insecure intermediate product surfaces.

### After 1A but before 1B

Authentication may exist, but authentication alone grants no business authority.

Allowed:

- login/logout/account-binding UX;
- non-sensitive identity diagnostics;
- operations whose authorization/tenant guarantees were independently qualified earlier, if any.

Forbidden by default:

- protected tenant mutations that rely only on successful authentication;
- treating token claims or client-supplied TenantId as current application permission.

### After 1B but before 1C

Authorization may exist, but a Web/Workstation surface may expose only operations whose session/surface protections are already production-honest.

For example, a cookie-authenticated mutation is not allowed to become a claimed production path while required CSRF/session semantics remain unqualified. Restrict/disable such routes until the relevant 1C guarantee is active.

### Before 1D

Role/device/permission mutations that span SquiFlow and OpenFGA cannot be presented as safely applied unless their cross-system failure/ambiguous-outcome lifecycle is already qualified. If the mutation surface exists earlier, it must remain restricted to a production-honest subset whose outcome can be established safely.

The closing mechanism must be technical where the risk is material: route/permission/feature/build/deployment restriction rather than developer memory.

## Mutable authorization versus active operations

For every operation class that can overlap a role/device/permission change, define and test the authoritative decision point.

At minimum distinguish:

```text
local proposal
server admission
durable acceptance
execution attempt
external side effect
final commit
```

There is no universal rule that every long operation continuously reauthorizes or that every revocation retroactively cancels committed history.

Required principles:

- stale local permission never overrides current server admission where current authority is required;
- a short authoritative mutation normally rechecks permission at admission immediately before protected mutation/commit;
- a revocation after an already committed authoritative transaction does not erase the historical fact;
- a pending/long-running operation states whether authorization is fixed at durable acceptance or revalidated at execution/checkpoints;
- a security-sensitive operation that requires current permission must fail closed/wait/reconcile while permission state is ambiguous;
- each chosen rule is covered by regression tests.

## Security carry-forward absence rule

A security item may be `NOT_INTRODUCED` only when the behavior while it is absent is deliberately chosen and safe.

Every Phase-1 security deferral records:

```text
What does the system enforce while this item is absent?
Is that behavior explicitly configured or merely a provider/framework default?
What test proves the absence behavior?
What surface is disabled if no safe absence behavior exists?
```

Examples such as exact MFA/step-up policy or provider recovery procedures may be deferred only when the current reachable scope has an intentional tested policy in their absence. `Whatever the provider defaults to` is not sufficient unless explicitly accepted, pinned/configured where feasible, and verified.