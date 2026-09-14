# Phase 5A — Rules, Fact Authority, and Decision Trace

## Required first rule

Implement one real tenant rule using bounded structured data, typed facts and deterministic evaluation.

The rule foundation is capability-selective. A capability with no real tenant variation can stay strongly typed and does not need a rule wrapper merely because Phase 5 exists.

## Fact authority

Classify rule inputs so a locally cached rule cannot turn stale server-required facts into authority. Examples of protected current facts include sensitive authorization, stock, payment, credit and hard resource limits.

## Publication

Published rule definitions are immutable/versioned. Invalid publication must leave the previous good version usable.

## Evaluation evidence

For material decisions retain enough safe evidence to explain:

```text
rule/version
fact versions/source classification
result
reason/decision trace
correlation/operation identity
```

Do not log entire sensitive business objects merely for rule debugging.

## Non-goals

No arbitrary C#, JavaScript, SQL, shell, plugin assembly or unrestricted expression execution.

## Exit gate

The same supported facts + rule version produce deterministic results and publication failure cannot corrupt active behavior.