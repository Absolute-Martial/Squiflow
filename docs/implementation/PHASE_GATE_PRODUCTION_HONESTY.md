# Phase Gate Production-Honesty Contract

**Status:** Canonical implementation-governance owner  
**Applies to:** every active implementation phase, subphase, pull-forward, integration gate, gate sign-off, and the governance documents that describe them  
**Baseline:** v0.0.20

## 1. Core invariant

SquiFlow phases deliberately separate **breadth** from **depth**.

- **Breadth** is how much product/domain/platform scope is implemented. Breadth may remain intentionally narrow.
- **Depth / production honesty** is whether the scope that is claimed to exist is dependable for the guarantees it claims. Production honesty is not optional.

The governing rule is:

> **Scope is a choice; honesty is not. A phase may introduce the smallest useful scope, but every introduced responsibility must be production-honest for that declared scope before the gate can pass.**

Therefore `minimal`, `smallest`, `KISS`, `current responsibility`, and similar wording must never be interpreted as permission for prototype-grade behavior on a claimed product path.

## 2. Production-honest does not mean feature-complete forever

Production-honest means that a real consumer can rely on the declared scope **today without being misled about what it guarantees**.

The consumer depends on the responsibility:

- a tenant/user for product behavior;
- an operator for recovery/deployment/control-plane behavior;
- another capability/host for an application contract;
- a developer for a repository/foundation/architecture guarantee.

A production-honest responsibility may still gain more breadth later. Later work may add more commands, fields, capabilities, providers, performance envelopes, roles, workflows, or deployment topologies. Those are new scope and re-enter this same gate model.

Production-honest is not a claim that:

```text
all future features exist
all future scale is solved
the component will never change
every optional mechanism is implemented
the system is already deployed to paying customers
```

## 3. Three valid gate states

Every material responsibility considered by an active gate is classified as exactly one of:

### `NOT_INTRODUCED`

The responsibility is outside the current declared scope.

- No product/runtime path claims it exists.
- No authoritative/durable state relies on it.
- No downstream component is told to rely on it.
- Documentation does not present it as implemented.

This is an honest deferral and may be carried forward with owner/trigger/latest gate when material.

### `PRODUCTION_HONEST`

The responsibility exists and satisfies the applicable production-honesty bar for its declared scope with evidence.

It may continue evolving later, but the current behavior is not intentionally disposable or misleading.

### `BLOCKED`

The responsibility has been introduced on a claimed path but does not yet satisfy the production-honesty bar.

Examples:

```text
reachable endpoint with TODO authorization
state called durable but only kept in memory
retryable mutation without duplicate/outcome-unknown semantics
migration path that cannot recover from interruption
provider call presented as reliable without timeout/failure behavior
a gate-required invariant asserted only by tests written around the current shortcut
```

`BLOCKED` is a **gate failure**. It is not a carry-forward state.

Before the gate can pass, either:

1. finish the responsibility to `PRODUCTION_HONEST`; or
2. explicitly un-introduce/isolate it so it truthfully becomes `NOT_INTRODUCED`.

## 4. When a responsibility counts as introduced

A responsibility counts as introduced when any materially applicable condition is true:

- it is reachable on a claimed application/administrative/operator path;
- it accepts or mutates real business/security/configuration state;
- it persists authoritative, provisional, durable-processing, recovery, or compatibility state;
- another implemented component depends on its public/application contract;
- an external or independently versioned contract is published/supported;
- repository/phase/product documentation states that the behavior exists;
- release/operator instructions rely on it.

A prototype/POC does **not** count as introduced only when all relevant conditions hold:

- it is clearly labeled experimental/non-product;
- it is isolated from claimed runtime paths;
- it does not carry real customer/tenant authority or durable production data;
- real callers cannot accidentally depend on it;
- it cannot be used as evidence that a production gate passed.

A feature flag or hidden route does not automatically make unfinished production code a POC.

## 5. Mandatory gate intent

Every **active/qualifying** phase/subphase/integration gate must state a falsifiable **production intent**:

> What real user/operator/developer scenario becomes safe and honest to depend on after this gate passes?

Avoid intents such as:

```text
establish the foundation
create the architecture
finish the phase checklist
make the tests pass
```

Prefer intents such as:

```text
a Workstation user can commit the declared offline-capable operation and survive the supported crash/restart paths without losing accepted local intent

a server can authoritatively admit the declared mutation with current tenant/authorization/invariant/idempotency/concurrency guarantees

an operator can restore the declared recovery set onto a replacement environment and verify usable state
```

For a developer-only architectural gate, the intent may name a developer/next-phase guarantee rather than inventing an end-user scenario.

A future roadmap direction that is still wholly `NOT_INTRODUCED` is **not yet an active gate** and therefore must not invent a production intent/evidence map merely to look complete.

## 6. Mandatory scope contract

Before active gate sign-off, record:

```text
PRODUCTION INTENT
- one falsifiable statement

WITHIN SCOPE — PRODUCTION_HONEST
- responsibility / claim
- owner
- exact guarantee
- evidence

NOT YET IN SCOPE — NOT_INTRODUCED
- deferred responsibility
- why it is not needed now
- preservation constraint
- trigger / latest gate when material

BLOCKED
- must be empty before gate pass
```

The within-scope statement must be specific enough that a reviewer can falsify it.

Bad:

```text
sync works
security is complete
database is production ready
```

Better:

```text
supported Workstation protocol v3 upload retries cannot duplicate the declared CreateOrder semantic effect, including response loss after central commit
```

## 7. Production-honesty bar

Apply the categories that are material to the declared scope. A category can be marked not applicable only with a reason; `not implemented yet` is not a valid reason for a responsibility already introduced.

### Correctness and domain meaning

- Success behavior matches the owning business/technical contract.
- Invalid/edge inputs materially relevant to the scope have explicit behavior.
- Historical/issued truth is not silently rewritten where revision/correction is required.
- Tests are derived from the contract/invariant, not used to redefine a shortcut as correct.

### Authority and security

- Authentication/authorization/tenant/resource/business authority is enforced at the owning boundary where applicable.
- A reachable protected path does not carry `TODO auth later` debt.
- Secrets/sensitive data follow the applicable handling/redaction/encryption requirements.
- Failure of an authority dependency does not silently become allow.

### Durability and state

- State called durable survives the supported failures of the runtime that currently exists.
- Authoritative, provisional, derived, cached, ephemeral, and presentation-only state are not misrepresented as one another.
- Accepted durable work is not held only by process memory/wakeup queues.

### Failure, recovery, and ambiguity

- Material dependency/process/storage failures have explicit safe outcomes.
- Retry ownership/budget is bounded where retries exist.
- Ambiguous external effects are reconciled rather than guessed where applicable.
- Recovery paths claimed by the scope are actually exercised.

### Concurrency and idempotency

- Shared-state races have an invariant-specific mechanism where material.
- Retryable semantic mutations cannot silently duplicate the protected effect.
- Idempotency does not hide changed intent under the same identity.

### Compatibility and migration

- Versioned/durable/independently deployed contracts have explicit supported/unsupported behavior.
- Migrations/schema evolution can survive the supported overlap/recovery path.
- Historical fixtures/evidence remain available when compatibility is claimed.

### Resource and operational bounds

- Queues, retries, batches, payloads, connections, disk/memory/network/provider usage are bounded where exhaustion can occur.
- The implementation does not depend on an unbounded loop/buffer/backlog to remain correct.
- Current deployment/operator constraints needed by the scope are documented.

### Observability and audit

- Material failures/state transitions produce useful evidence without leaking secrets/PII.
- Authoritative audit/usage/security state is not replaced by lossy telemetry where durable evidence is required.
- A silent failure is not considered an acceptable degraded mode.

### Reproducibility and verification

- Another developer/operator can reproduce the claimed verification from repository-owned instructions and evidence without undocumented tribal knowledge.
- Normal developer verification should target a clean-checkout path that is practical for an unfamiliar developer; approximately one hour is a useful target for ordinary local setup, excluding explicitly documented external/operator ceremonies.
- If a property requires a real DB/framework/process/provider/restore/hardware test, mocks alone do not prove it.

## 8. Evidence cannot be circular

A green test suite is not enough if the suite merely encodes the current implementation shortcut.

Evidence must trace to one or more of:

- accepted requirement/invariant;
- focused architecture/decision owner;
- declared API/contract semantics;
- explicit failure/recovery promise;
- workload/capacity target;
- security/authority requirement;
- provider/platform behavior actually depended upon.

For every material gate claim, reviewers should be able to answer:

```text
What claim are we proving?
Where is that claim owned?
What evidence could falsify it?
Did we test the layer that actually owns the property?
```

## 9. Minimum scope versus minimum quality

`Minimal` and `smallest` may reduce **breadth**:

```text
one capability instead of ten
one real provider instead of a generic provider ecosystem
one supported workflow instead of arbitrary workflow breadth
one real host instead of prebuilding all future hosts
```

They may not reduce **depth** of the declared claim:

```text
no fake persistence
no deferred security on an exposed path
no unrecoverable durable state called reliable
no silent unsupported-version reinterpretation
no unbounded retry/resource behavior hidden as simplicity
```

The preferred phrase for phase work is therefore:

> **smallest production-honest scope**

rather than `minimum implementation` or `complete enough`.

## 10. Carry-forward rule

Only `NOT_INTRODUCED` responsibilities may be deferred as future scope.

A carry-forward entry records, where material:

```text
Item
State = NOT_INTRODUCED
Why deferred
Owner
Current preservation constraint
Trigger
Latest closing gate when it is honestly knowable
Current check preventing accidental introduction/violation
```

Do not invent a `Latest closing gate` merely to make a future roadmap look complete. If the real closing point depends on future workload/product knowledge, record it as unset until activation.

A `PRODUCTION_HONEST` responsibility can of course receive more breadth later; record the later breadth as new scope, not as debt in the already claimed behavior.

A `BLOCKED` responsibility cannot be carried forward.

## 11. Pull-forward rule

If a real current slice requires a responsibility scheduled for a later phase:

```text
identify the likely owner/direction
→ promote the responsibility from NOT_INTRODUCED
→ derive the active gate from current facts
→ declare its current scope
→ satisfy the production-honesty bar now
→ add evidence
→ continue
```

Do not introduce a temporary unsafe substitute on the assumption that a later roadmap phase will repair it.

## 12. Gate evidence record

A gate is signed off only when evidence records, in a phase status document, MR description, or another durable reviewed artifact:

1. production intent;
2. declared production-honest scope;
3. explicit non-scope;
4. zero `BLOCKED` responsibilities;
5. claim-to-evidence links/names;
6. known limits/non-claims;
7. material carry-forward items with triggers;
8. any verification that could not be run and why.

Do not create empty evidence-template files simply for symmetry. Record evidence when the gate is actually being qualified.

## 13. Gate review questions

A gate cannot pass unless the answer is `yes`, with evidence, to the applicable questions:

1. Can we state the real scenario this gate makes dependable?
2. Is the declared scope narrow and explicit rather than inferred from whatever code happens to exist?
3. Is every introduced responsibility `PRODUCTION_HONEST`?
4. Is `BLOCKED` empty?
5. Have we tested current-runtime failure paths that can invalidate the claim?
6. Are security/authority/durability/recovery claims real rather than postponed?
7. Are resource/retry/compatibility behaviors bounded where the scope introduces them?
8. Can another developer/operator reproduce the important evidence from the repository?
9. Can we state plainly what the system **cannot** yet do without misleading a customer, operator, or developer?
10. If the scope were depended on today, is there any known shortcut we expect to rewrite because it is not trustworthy enough for the claim?

If question 10 is `yes`, either fix the shortcut or un-introduce that claim. The gate does not pass.

## 14. Governance documents are claims too

The production-honesty rule applies to the roadmap and gate documents themselves.

A governance document is allowed to be precise only where the project has enough real responsibility/workload/implementation knowledge to justify that precision.

Future direction may state:

```text
likely responsibility class
known dependencies
current preservation constraints
activation trigger
```

Future direction must **not** state as current canonical fact:

```text
exact A/B/C subphase decomposition
exact evidence classes
exact regression cadence
exact hostile/failure inventory
exact transitional contract
exact provider/runtime mechanism
exact exit gate
```

unless real work has earned those decisions.

If detailed future thinking is useful, preserve it as non-authoritative carry-forward/anticipation. When the responsibility becomes real, rewrite the active governance from current facts rather than treating old speculation as a contract.

This is the governance equivalent of YAGNI: **do not prebuild the gate structure before the real claim exists.**