# GATE-001 prerequisite evidence recovery assignment

Do not run or accept final GATE-001 until its named prerequisite handoffs are retained and independently reviewable. This assignment recovers evidence only; it does not reimplement the underlying scopes and does not grant acceptance by itself.

## Missing historical artifacts

The current source archive does not contain the historical paths previously cited by gate/focused owners, including:

- `artifacts/verification/adm-002/reviewer/REVIEW.md`;
- `artifacts/verification/ops-022/root-final-review.json`;
- `artifacts/orchestration/assignments/ADM-003-public-auth.md`.

The repository also lacks a retained independent post-fix ADM-003 reviewer receipt. Commit `6735370` contains the implementation and current focused tests are green, but focused implementer/current-head passes are not independent acceptance.

## Required outcome

1. Recover the original ADM-001/ADM-002/OPS-022 handoff receipts from the qualifying environment, or have the accountable gate owner explicitly replace each missing historical path with a tracked current receipt that contains equivalent source identity, commands, exits, pass/fail/skip totals, negative cases, non-claims and requalification triggers.
2. Execute [ADM-003 independent post-fix review](ADM-003-INDEPENDENT-REVIEW.md) and retain its `ACCEPTED` or `NEEDS_CORRECTION` receipt in the repository.
3. Preserve failures and source hashes; do not reconstruct receipts from prose summaries or mark a receipt accepted merely because focused tests pass.
4. Only after these prerequisites are retained may final GATE-001 run the normal repository gate and make its acceptance decision.

Tracked summaries under `docs/production-completion-review/evidence/` are useful reconciliation evidence, but they are not byte-for-byte substitutes for missing independent handoffs unless the accountable gate owner explicitly accepts that replacement in a retained decision.
