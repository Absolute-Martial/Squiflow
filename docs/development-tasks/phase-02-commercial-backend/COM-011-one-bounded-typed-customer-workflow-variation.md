# COM-011 — One bounded typed customer workflow variation

Task ID: COM-011
Phase: 02-commercial-backend
Status: DECISION_REQUIRED
Model: GPT-6.1 Sol
Dependencies: COM-007, ADM-021
Conditional dependencies: OPS-005 when the selected policy requires timed occurrences; OPS-010 when external notification is selected
Release requirement: REQUIRED

Planned assignment only. Apply [agent rules](../AGENT_RULES.md) and [handoff rules](../HANDOFF_AND_INTEGRATION.md).

## Outcome

Implement one agreed customer/program-specific guidance variation through typed versioned rules, supplementary fields and stages, proving adaptation without creating a universal workflow or form engine.

## Current basis and canonical inputs

Recheck `README.IMPLEMENTATION.md`, scoped `AGENTS.md`, [business model](../../domain/BUSINESS_MODEL.md), [accepted/open decisions](../../decisions/CURRENT_DECISIONS.md), [open registry](../../decisions/OPEN_DECISIONS.md), and [BUSINESS_OPERATION_END_TO_END.md](../../implementation/BUSINESS_OPERATION_END_TO_END.md), [WORKFLOW_DESIGN.md](../../workflow/WORKFLOW_DESIGN.md), [NATIVE_RULE_ENGINE.md](../../rules/NATIVE_RULE_ENGINE.md). Focused owners outrank historical plans.

## Scope and exclusions

Allowed areas: Web-administration-only authoring/publication APIs; one capability-owned effective selection path, bounded typed form schema, pure evaluation and retained version evidence with focused tests. No tenant scripts, uploaded SQL, visual app builder or financial truth defined by custom stages.

## Decisions/prerequisites

Owner selected the first case on 2026-10-08: **required program reference before Order commitment**. The focused [workflow owner](../../workflow/WORKFLOW_DESIGN.md) records the selection. Status remains `DECISION_REQUIRED`: clarify whether reference means existing program attribution or a supplementary external reference, then close typed shape, selection/precedence, publication authority and active-work version decisions. ADM-018–021 durable profile prerequisites remain unimplemented; a case selection is not their qualification.

Choose the actual variation, permitted typed fields/operators/actions, inheritance conflict rule, bounds, publication authority and compatible version policy. Approval behavior is supplied by COM-012; deadlines need OPS-005 only when the chosen case requires scheduling.

## Acceptance and edge cases

- Validate, simulate and publish immutable compatible definition/form/rule versions.
- Guidance explains missing information, allowed next action, actor and prohibition reasons.
- Unknown facts, conflicting outputs and complexity exhaustion fail explicitly with no partial effect.
- Protected permissions, invoice, stock and payment facts cannot be overridden by configuration.
- Active work stays pinned or follows an explicit migration/revalidation decision.
- Historical labels and supplied fields remain renderable after retirement.

## Security/static review

Review fact authority classes, bounded evaluation, configuration privilege and sensitive decision traces. Neutral evaluation has no I/O; stale cached facts cannot authorize central effects.

## Dynamic verification and unavailable-environment handling

Test one default and one customer/program variant, version changes, missing required data and invalid publication. PostgreSQL proves pinning/publication continuity; real-host tests prove skipped server checks are denied. Run `./eng/verify.sh` when available. Without SDK/Docker/credentials, implement tests, finish static review and hand off exact unrun commands/expected outcomes; do not qualify unrun runtime claims.

## Handoff

Deliver source-only ZIP, changed files, decisions, evidence, recurring guards and requalification triggers. State non-claims and material responsibility states. Preserve incoming work; no commit/push.

## Assignable prompt

```text
Implement COM-011: One bounded typed customer workflow variation only.
Read current owners and applicable instructions.
Inspect existing callers and tests; preserve incoming work.
Close listed decisions before dependent contracts.
Implement the smallest complete scope and focused tests.
Review security, authority, durability and concurrency.
Update focused behavior/decision documentation.
Run available checks; name exact unrun checks.
Deliver source-only ZIP and evidence handoff.
No commit/push or unrun qualification claims.
```
