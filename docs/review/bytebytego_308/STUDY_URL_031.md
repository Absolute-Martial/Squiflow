# URL 031 — A Detailed Guide to Idempotency, Delivery Semantics, and Deduplication

## Review method

This occurrence is reviewed independently from earlier archive/URL material. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: no comparison, pattern catalog, popularity claim, or source diagram selects architecture by itself.

## A. Identification

- **URL occurrence:** `031`
- **PDF page:** `275`
- **Source URL:** `https://blog.bytebytego.com/p/a-detailed-guide-to-idempotency-delivery`
- **Source access:** paid article with a public preview; no subscription controls bypassed.
- **Related supplied visual:** archive page `143`, REST API design best-practices cheat sheet.
- **Visual inspected:** PDF page `275` at full size.

## B. Core concept

### SOURCE

The public preview starts with the ambiguous-timeout problem: a charge request can time out even though the charge may have succeeded. Retrying can duplicate the effect while refusing to retry can lose the intended operation. The preview defines idempotency as the property that makes a retry safe by causing repeated execution to reach the same state. Its visible outline covers three delivery semantics, duplicate creation at producer/broker/consumer stages, natural versus engineered idempotence, idempotency keys, deduplication windows, and the limits of “exactly once.”

### INFERENCE

The important lesson is end-to-end scope. A mechanism can suppress one duplicate path while leaving another path untouched. Therefore the architectural question is not “does the broker/API support deduplication?” but “what identifies the intended business effect, where can it duplicate, and what durable evidence exists after every ambiguous boundary?”

### EXTERNAL KNOWLEDGE / CAVEAT

“At-most-once,” “at-least-once,” and “exactly-once” are scoped guarantees, not whole-system adjectives. A broker may provide transactional or deduplication behavior inside its own boundary while a consumer can still repeat an external payment after a crash. Natural mathematical idempotence and application-engineered idempotence are also different: a non-idempotent business action can be made retry-safe through a stable operation identity plus durable receipt/effect state.

## C. Important concepts

- ambiguous timeout / response loss;
- semantic operation identity;
- same key + same intent versus same key + changed intent;
- producer, transport/broker, and consumer/effect duplicate entry points;
- transport `MessageId` versus business `IdempotencyKey`;
- atomic receipt + business mutation + outbox when one authoritative store can own them;
- provider-side idempotency/effect references for external effects;
- deduplication-retention window as part of the guarantee;
- `OutcomeUnknown` and reconciliation;
- scoped exactly-once claims rather than system-wide slogans;
- restore consistency between business effects and idempotency evidence.

## D. Diagram / visual explanation

The supplied related visual is a REST API design cheat sheet. Its idempotency section shows repeated requests, an HTTP-method idempotence table, and an implementation sketch with an idempotency key/payload store. This is useful as a reminder that retry safety requires stored evidence, but the visual is not enough to prove SquiFlow correctness: where that evidence commits, how long it lives, how it binds to intent, and what happens across external side effects are the harder questions.

## E. How it works — step by step

1. Caller creates one stable semantic idempotency key for one intended business operation.
2. Server authenticates and resolves authoritative tenant/actor context.
3. Server binds the key to operation kind + authority scope + semantic request identity.
4. Same-key/same-intent replay returns the existing semantic result/status rather than repeating the effect.
5. Same-key/different-intent is rejected.
6. Where possible, idempotency receipt + business mutation + audit/outbox commit atomically.
7. If a provider effect lies outside the transaction, provider idempotency/reference plus local `OutcomeUnknown`/reconciliation is used.
8. Retry policy remains finite and failure-classified.
9. Retention of the receipt is explicit and the guarantee after expiry is not overstated.
10. Restore drills prove completed effects do not return without evidence needed to suppress a late retry.

## F. Why it matters

This is central to SquiFlow because Workstations can be offline, responses can disappear after authoritative commit, Workers can redeliver, and external providers can succeed while the local process sees a timeout. Money, stock, document issuance, authorization changes, and other material effects cannot rely on “the network probably delivered the response.”

## G. Trade-offs / limitations

Benefits include safe retry, deterministic duplicate handling and stronger reconciliation evidence. Costs include receipt storage/retention, canonical semantic-request comparison, concurrency around first key use, restore coupling and external-effect reconciliation. After deduplication evidence expires, the original guarantee may no longer hold.

## H. Alternatives / comparisons — fit, not winner/loser

```text
naturally idempotent operation
    -> use natural semantics where real

engineered semantic idempotency
    -> retryable non-idempotent business commands

transport/broker deduplication
    -> envelope duplicates inside that transport

provider idempotency key
    -> one external-provider boundary

business uniqueness/constraint
    -> prevents one class of duplicate effect

reconciliation
    -> ambiguous effect that cannot be atomically proven
```

These mechanisms can coexist. None is a universal substitute for all others.

## I. Real implementation considerations

Implementation must prove key scope, request-intent binding, concurrent first request handling, atomic receipt/effect behavior, retention, restore behavior, external-provider ambiguity, old Workstation retries, and duplicate entry at producer/transport/consumer boundaries.

### Implications for the Current Implementation

- **KEEP:** SquiFlow's documented semantic idempotency contract: caller/tenant authority + operation kind + idempotency key, with stable Workstation outbox-item keys independent of transport batch IDs.
- **KEEP:** atomically commit mutation + idempotency receipt + audit/outbox where the authoritative store can own them together.
- **KEEP:** provider idempotency/reference + `OutcomeUnknown` reconciliation when an external effect cannot join the local transaction.
- **KEEP:** separate `RequestId`, `MessageId`, `IdempotencyKey`, business ID, correlation and causation IDs.
- **KEEP:** “exactly once” only with a named proven scope.
- **IMPROVE NOW (implementation gate):** prove concurrent same-key arrival, same-key/changed-intent rejection, response-loss-after-commit, Worker redelivery, external-effect ambiguity, and restore-with-late-retry behavior when code exists.
- **NEEDS MEASUREMENT:** retention by command family; high-risk financial/security receipts may justify longer evidence than low-risk operational commands.
- **AVOID:** using HTTP request IDs or broker message IDs as the semantic business key.
- **AVOID:** advertising system-wide exactly-once from one DB/broker/provider feature.

**What are we actually doing and why?** We are engineering retry-safe semantic commands because ambiguous outcomes are a real Workstation/API/Worker/provider failure mode, not because a comparison favors one delivery semantic.

**What would falsify/change this?** If a command is proven naturally idempotent and has no meaningful duplicate effect, extra receipt machinery may be unnecessary. If provider/restore behavior cannot preserve the guarantee, the command must expose reconciliation/`OutcomeUnknown` rather than pretending the idempotency layer is stronger than it is.

### Critical interrogation — answers intentionally withheld

**Foundation**
1. What is the difference between natural idempotence and an endpoint engineered to be idempotent?
2. Why are producer, broker, and consumer duplicates separate problems?
3. What guarantee ends when an idempotency record expires?

**Critical reasoning**
1. What exact SquiFlow business identity does an idempotency key represent?
2. Why is `RequestId` insufficient after a network retry?
3. What must be atomic with a payment/refund idempotency receipt?
4. If a provider succeeds but SquiFlow never sees the response, which state is authoritative?
5. How can restore accidentally re-enable a duplicate effect?

**Trade-off**
1. When can a uniqueness constraint remove the need for a separate receipt, and when can it not?
2. When is provider-side idempotency sufficient for that provider call but insufficient for the SquiFlow business command?
3. How long should low-risk versus financial idempotency evidence live?

**Failure / edge**
1. Two same-key requests arrive concurrently on two future API nodes. What prevents two effects?
2. Same key is retried with one changed field after the original succeeded. What happens?
3. Worker crashes after provider success but before local completion. What happens on redelivery?
4. Business row restores but receipt does not. What is the safe recovery state?

**Implementation**
1. Which canonical fields are persisted to compare “same intent”?
2. Which DB constraint/transaction prevents concurrent first-use duplication?
3. Which telemetry distinguishes duplicate replay from a new operation?
4. How does the Workstation retain its semantic key across restarts and batch regrouping?

**System design interview**
1. Design retry-safe `RefundPayment` across HTTP, DB, Worker, and external provider.
2. Explain why broker exactly-once does not automatically produce end-to-end exactly-once.

**Challenge**
A refund commits centrally, the response is lost, the original idempotency receipt later ages out, and an old Workstation retries months later after a restore. State exactly what evidence must exist to prevent or safely reconcile a second refund.
