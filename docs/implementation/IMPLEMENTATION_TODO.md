# Implementation TODO and Carry-Forward

**Purpose:** Preserve unfinished or trigger-dependent implementation work without misclassifying completed phase gates as incomplete.

This file is not an implementation permit list. Every item remains subject to the focused owner, current decisions, `AGENTS.md`, and the production-honest gate model.

Use these meanings:

```text
ACTIVE
= current introduced responsibility that must be finished or un-introduced

TRIGGERED LATER
= NOT_INTRODUCED now; activate only when the stated real condition occurs

OPERATIONAL FOLLOW-UP
= useful environment/tooling work that does not invalidate an already-qualified claim
```

## Active

**None.** Phase 0 qualification has `BLOCKED = none`.

## Operational follow-up

### TODO-CI-001 — Restore an executable GitLab runner path

**State:** OPERATIONAL FOLLOW-UP; not a Phase-0 blocker  
**Why retained:** GitLab accepts and schedules the repository verification job, but hosted quota currently prevents runner start. GitHub supplied successful executable evidence for qualified 0B and the final Phase-0 four-project solution.  
**Trigger/action:** when hosted quota or a self-managed runner becomes available, run the existing `.gitlab-ci.yml` contract and record the first successful GitLab execution.  
**Constraint:** do not create a second GitLab-specific build/test contract.

## Triggered later / NOT_INTRODUCED

### TODO-FOUNDATION-001 — Reconsider shared Foundation only under real cross-capability pressure

**State:** TRIGGERED LATER / NOT_INTRODUCED  
**Trigger:** two or more real capabilities independently need the same stable product-wide semantic/contract and capability ownership would otherwise duplicate business meaning or create invalid dependency direction.  
**Current result:** Parties and Payments do not expose such a shared semantic.  
**Constraint:** do not manufacture a consumer or resurrect the deleted ApplicationKernel to satisfy this TODO.

### TODO-0C-001 — Introduce host/process composition only when a real executable responsibility exists

**State:** TRIGGERED LATER / NOT_INTRODUCED  
**Owner when activated:** `docs/implementation/phases/phase-0/0C_HOST_AND_PROCESS_COMPOSITION_FOUNDATION.md` plus the current phase/responsibility owner at activation time.  
**Trigger:** a real current operation needs an executable composition root and can answer the lifecycle/fault/security/resource/deployment questions.  
**Constraint:** Phase 0 qualification does not require an empty Workstation/CoreApi/Web/Guard host.

### TODO-PARTIES-001 — Extend Party identity/lifecycle only from a real journey

**State:** TRIGGERED LATER / NOT_INTRODUCED  
**Trigger:** a real operation needs stable Party identity, mutation/lifecycle/contact/profile/merge behavior, or a resolved Customer/Account relationship.  
**Constraints:** internal-ID encoding remains unselected; Customer/Party/Account/Commercial Relationship remains discovery-sensitive.

### TODO-PAYMENTS-001 — Extend payment behavior only when a real payment operation requires it

**State:** TRIGGERED LATER / NOT_INTRODUCED  
**Trigger:** a current journey needs amount/currency, rounding, payment identity, transition rules, retry/idempotency, outcome-unknown reconciliation, refund/reversal behavior, persistence, API, authority, or settlement.  
**Constraints:** resolve the focused money/rounding/authority/open decisions before hardening those semantics.

### TODO-PHASE1-001 — Activate runtime trust/security implementation when a reachable protected surface exists

**State:** TRIGGERED LATER / NOT_INTRODUCED as runtime implementation  
**Owner when activated:** `docs/implementation/phases/phase-1/` and focused security owners.  
**Trigger:** a real externally reachable user/tenant operation or another current responsibility requires authenticated identity, tenant authorization, session behavior, or provider-backed trust guarantees.  
**Constraint:** accepted ZITADEL/OpenFGA directions and detailed Phase-1 governance do not by themselves justify provider/runtime scaffolding before a protected surface exists.

## Completed phase note

Phase 0A, 0B, 0D, 0E and integrated 0F are **COMPLETE / QUALIFIED** for their introduced responsibilities. 0C remains `NOT_INTRODUCED` because no executable responsibility exists. This is a valid Phase-0 result under `0F_PHASE_0_INTEGRATION_GATE.md`; future triggers above are not hidden Phase-0 blockers.
