# Independent review assignment

Use an available smaller/Flash model for bounded fixed-contract review; use Sol for authority, money, concurrency, cryptography, recovery or new session boundaries. Attach the complete implementation task and baseline/returned changes. This is a read-only review, not another implementation task.

```text
Review <TASK-ID>.<slice> against the supplied source baseline and accepted contract.
Read AGENT_RULES.md, the complete task and its focused owners.
Baseline Git SHA: <SHA>; baseline ZIP SHA-256: <digest>.
Returned ZIP SHA-256: <digest>; approved write allowlist: <paths>.
Do not implement fixes, change decisions, commit or broaden scope.
Check that changed paths and dependencies match the assignment.
Trace task-specific authority, tenant/resource scope and safe failure behavior.
Review applicable idempotency, revision, retained facts, concurrency and recovery.
Check resource bounds, untrusted input, output and diagnostic disclosure.
Match acceptance claims to meaningful tests and actually inspected evidence.
Report each finding with file/location, trigger, consequence and smallest correction.
Separate confirmed defects, contract questions and missing dynamic evidence.
Return PASS_WITHIN_REVIEW_SCOPE, NEEDS_CORRECTION or EVIDENCE_PENDING with rationale.
Do not treat static review or a model verdict as release qualification.
```

Acceptance by the integrator is separate. Include affected task/contract IDs and severity grounded in a concrete scenario. Requalification is required when a fix changes the reviewed contract or material behavior; repeating already passed unrelated tests adds no evidence.
