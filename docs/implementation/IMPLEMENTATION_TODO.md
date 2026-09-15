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
= useful environment/tooling work that does not invalidate an already-qualified semantic claim
```

## Active

### TODO-0E-001 — Qualify the Payments capability slice

**State:** ACTIVE / BLOCKED  
**Owner:** `docs/implementation/phases/phase-0/0E_STATUS.md`  
**Required evidence:** successful `restore -> build -> test` execution for the current 0E branch on a legitimate execution host.  
**Done when:** the introduced 0E Payment-status and dependency claims are promoted to `PRODUCTION_HONEST` or deliberately un-introduced.

## Operational follow-up from qualified 0B

### TODO-CI-001 — Restore an executable GitLab runner path

**State:** OPERATIONAL FOLLOW-UP; not a Phase-0B blocker  
**Why retained:** GitLab accepts and schedules the repository's verification job, but hosted quota currently prevents runner start. GitHub supplied the successful executable evidence that qualified 0B.  
**Trigger/action:** when hosted quota or a self-managed runner becomes available, run the existing `.gitlab-ci.yml` contract and record the first successful GitLab execution.  
**Constraint:** do not create a second GitLab-specific build/test contract.

## Triggered later / NOT_INTRODUCED

### TODO-FOUNDATION-001 — Reconsider shared Foundation only under real cross-capability pressure

**State:** TRIGGERED LATER / NOT_INTRODUCED  
**Trigger:** two or more real capabilities independently need the same stable product-wide semantic/contract and capability ownership would otherwise duplicate business meaning or create invalid dependency direction.  
**Current result:** Parties and Payments do not yet expose such a shared semantic.  
**Constraint:** do not manufacture a consumer or resurrect the deleted ApplicationKernel to satisfy this TODO.

### TODO-0C-001 — Activate host/process composition when a real executable responsibility exists

**State:** TRIGGERED LATER / NOT_INTRODUCED  
**Owner when activated:** `docs/implementation/phases/phase-0/0C_HOST_AND_PROCESS_COMPOSITION_FOUNDATION.md`  
**Trigger:** a real current operation needs an executable composition root and can answer the 0C lifecycle/fault/security/resource/deployment questions.  
**Constraint:** do not create an empty Workstation/CoreApi/Web/Guard host merely because 0C is numerically next.

### TODO-PARTIES-001 — Extend Party identity/lifecycle only from a real journey

**State:** TRIGGERED LATER / NOT_INTRODUCED  
**Trigger:** a real operation needs stable Party identity, mutation/lifecycle/contact/profile/merge behavior, or a resolved Customer/Account relationship.  
**Constraints:** internal-ID encoding remains unselected; Customer/Party/Account/Commercial Relationship remains discovery-sensitive.

### TODO-PAYMENTS-001 — Extend payment behavior only when a real payment operation requires it

**State:** TRIGGERED LATER / NOT_INTRODUCED  
**Trigger:** a current journey needs amount/currency, rounding, payment identity, transition rules, retry/idempotency, outcome-unknown reconciliation, refund/reversal behavior, persistence, API, authority, or settlement.  
**Constraints:** resolve the focused money/rounding/authority/open decisions before hardening those semantics.

## Completed phase note

Phase 0B remains **COMPLETE / QUALIFIED**. The items above are carry-forward triggers or operational follow-up; they are not hidden 0B blockers.
