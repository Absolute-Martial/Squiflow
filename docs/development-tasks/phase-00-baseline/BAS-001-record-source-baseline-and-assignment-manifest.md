# BAS-001 — Record the source baseline and assignment manifest

Task ID: BAS-001
Phase: 00-baseline
Status: VERIFY_EXISTING
Model: GPT-6 Luna (high)
Dependencies: none
Conditional dependencies: none
Release requirement: REQUIRED

## Outcome

Record a reproducible source snapshot and incoming-work manifest for later single-task assignments without changing runtime truth.

## Current basis and canonical inputs

Read [current truth](../../../README.IMPLEMENTATION.md), [production-honest gate](../../implementation/PHASE_GATE_PRODUCTION_HONESTY.md), [evidence permanence](../../implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md), [verification strategy](../../testing/VERIFICATION_STRATEGY.md), [shared rules](../AGENT_RULES.md) and [handoff](../HANDOFF_AND_INTEGRATION.md). Focused owners take precedence; historical tests do not qualify incoming edits.

## Scope and exclusions

Allowed areas: New safe baseline manifest/evidence under the assigned task artifact directory; read-only source inventory and catalog metadata corrections only. No feature implementation, unrelated refactor, Git mutation or product-version change.

## Decisions/prerequisites

Use the exact current checkout, full Git SHA and modified/untracked source paths. The account/budget work may differ from the last qualified commit; record it, do not infer its quality. Do not read/export secret contents. The integrator approves the source allowlist.

## Acceptance and edge cases

- Capture full baseline SHA and hash included dirty source files, including incoming Admin budgets.
- Differentiate tracked modifications, deliberate untracked source and unrelated package-lock changes.
- Record available SDK/Docker/provider/browser capabilities without claiming missing checks passed.
- Extraction recreates the allowlisted baseline and every included file hash agrees.
- No .git, bin, obj, private keys, runtime data or secret configuration enters the ZIP.

## Security/static review

Inspect the allowlist and ZIP member paths for traversal/symlink escapes and sensitive material. This is an inventory task: Sol reviews any discovered authority or contract ambiguity before edits.

## Dynamic verification and unavailable-environment handling

Run the catalog validator, verify ZIP extraction against the hash manifest, and record the normal repository gate as a separate integrator obligation. Do not launch a competing build against another agent’s run. Inspect actual commands, versions, exit codes, totals, failures and skips. Unavailable environments leave named evidence pending and corresponding introduced claims BLOCKED; static review cannot substitute for real runtime boundaries.

## Handoff

Return the reviewed changed-file list, safe evidence, source-only ZIP with SHA-256 and unresolved blockers. Map each material claim to its owner, permanent/recurring guard and requalification trigger. No commit/push. Requalification is required when relevant code, contract, provider/model, runtime, host topology or operating configuration changes.

## Assignable prompt

```text
Execute BAS-001 as a bounded read-only baseline inventory.
Record full Git SHA and included dirty-file states/hashes.
Preserve incoming Admin budget work and unrelated files.
Review the source allowlist before creating the ZIP.
Exclude Git metadata, build outputs, secrets and runtime data.
Validate extraction and every included file hash.
Record available tools and exact checks still pending.
Return manifest, source-only ZIP and SHA-256 for Sol review.
```
