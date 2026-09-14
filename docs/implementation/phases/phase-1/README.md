# Phase 1 — Identity, Tenant Authorization, and Session Foundation

**Parent owner:** `docs/implementation/PHASES_AND_GATES.md`  
**Production-honesty owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Purpose

Phase 1 establishes the first production-honest trust boundary for real users and tenants. It does not freeze business-module development: capabilities, Workstation, Web, server hosts, Guard, observability, deployment and tests may all continue evolving while the trust foundation is introduced.

A capability does not have to wait for Phase 1 to exist. Phase 1 is the point where externally reachable authenticated behavior can rely on the declared identity/tenant-authorization/session guarantees rather than test identities or UI-only checks.

Phase 1 is kept as a detailed package because the accepted identity/authorization/session architecture already creates concrete trust boundaries whose safe behavior can be specified without inventing a future business topology.

## Active subphases

```text
1A  ZITADEL identity and account binding
1B  TenantContext, OpenFGA authorization, roles and devices
1C  Web/Workstation session and surface security
1D  Authorization change reconciliation, failure and degraded modes
1E  Integrated Phase-1 gate
```

These are active governance documents, so their claims require falsifiable evidence, permanent/recurring regression protection, and transitional restrictions where reachable intermediate states exist.

## Phase maturity added

After Phase 1, externally reachable business behavior may rely on real authenticated identity and current application authorization for the specific surfaces/flows that have actually qualified. This does not imply that every future Admin/device/MFA/offline-security responsibility has been implemented.

## Components that may continue/add

- any real capability module and its Web/Workstation/API adapter;
- tenant/account/device persistence needed by the trust model;
- authorization-sensitive application use cases;
- observability/audit evidence for identity/authorization paths;
- deployment/configuration required for ZITADEL/OpenFGA;
- tests against real isolated provider environments.

## Earned-only additions

Do not introduce Platform Admin, Worker, SyncApi, broker, service mesh, GraphQL, or new auth systems merely because identity/authorization now exists.

If a real current requirement needs one of those responsibilities, promote it from `NOT_INTRODUCED`, create its current owner/gate from the real workload, and satisfy the global production-honesty/evidence contracts. Do not activate it by restoring a speculative future phase file.

## Standards grounding

Identity/OAuth implementation claims must be grounded in the standards actually used by the selected topology, not the phrase `production-shaped`.

For the accepted Workstation browser flow and OAuth security baseline, relevant guidance includes:

- RFC 9700 — OAuth 2.0 Security Best Current Practice;
- RFC 8252 — OAuth 2.0 for Native Apps;
- RFC 7636 — Proof Key for Code Exchange (PKCE);
- OpenID Connect Core for OIDC-specific semantics.

RFC 9449 defines DPoP; it is not the generic browser/native-app specification. DPoP/mTLS remain threat-model/topology driven rather than automatic requirements.

## Transitional security contract

Subphase sequencing does not grant permission for an insecure reachable intermediate state.

Whenever the currently reachable Phase-1 surface has a later security guarantee still absent, record and enforce:

```text
what exists now
what operations are allowed
what operations are disabled/forbidden
what guarantees have actually qualified
what guarantees remain absent
technical enforcement preventing accidental use
real closing condition
```

Examples include authenticated-but-not-yet-authorized surfaces and authorized cookie-backed mutation before session/CSRF behavior has qualified. The exact transition applies only when that real intermediate surface exists.

## Sustainability

Provider SDK types stay behind infrastructure/host adapters. `(issuer, subject)` and SquiFlow-owned permission/tenant semantics remain stable even if identity/authorization providers are replaced later.

Security properties that qualify in Phase 1 remain regression-protected after Phase 1; later business phases do not weaken them merely because the trust phase is considered complete.

## Relationship to future phases

Phase 2–10 are currently direction-only `NOT_INTRODUCED` stubs. Phase 1 carry-forward entries may describe future needs and safe absence behavior, but they do not pre-write the future phase evidence map, cadence or subphase decomposition.

The Phase 2–10 labels themselves remain planning guidance rather than a frozen sequence. If real work needs a responsibility earlier or reveals a better grouping, promote/restructure it from current facts.

Non-authoritative future thinking lives in `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md`.