# Notifications, Webhooks, and External Delivery

**Version:** v0.0.15

This capability was referenced indirectly by Worker/integration planning but did not have a clear owner or failure contract. It is now a bounded cross-cutting capability, not a new microservice requirement.

## 1. Scope

Potential delivery channels include only those justified by a real workflow, such as:

- email;
- SMS/provider message;
- tenant/customer webhook;
- other provider integrations added later.

Do not implement every channel before a business journey needs it.

## 2. Business transaction boundary

A notification is normally a consequence of business truth, not the truth itself.

Example:

```text
Order becomes ReadyForPickup
→ authoritative transaction commits
→ outbox creates notification intent
→ Worker delivers/retries
```

Email/SMS failure must not roll back an already committed Order state.

For a remote integration whose response is itself required to authorize/complete the business transaction, model that explicitly rather than pretending every integration is fire-and-forget.

## 3. Delivery record

A durable delivery attempt/effect record should carry conceptually:

- DeliveryId;
- TenantId where tenant-owned;
- channel/provider;
- destination reference/protected destination;
- template/schema/version;
- semantic idempotency key;
- correlation/causation;
- attempt count;
- provider reference/result;
- status;
- safe error category;
- next retry/reconciliation time where applicable.

Do not put unrestricted customer content/secrets into queue/log payloads.

## 4. States

Use explicit delivery/effect states such as:

```text
Pending
Sending
Delivered
FailedRetryable
FailedPermanent
OutcomeUnknown
CancelledBeforeSend
```

`OutcomeUnknown` is required when a provider may have accepted the request but SquiFlow did not receive a reliable acknowledgement.

## 5. Idempotency and retry

Use provider idempotency/reference semantics where available.

Retries are finite, classified, backoff/jitter aware and bounded by provider/tenant budget. A retry does not create duplicate SMS/email/webhook business intent merely because the transport attempt changed.

## 6. Webhooks

Outbound tenant/customer webhooks require:

- destination ownership/configuration through Web administration;
- URL/SSRF validation policy;
- request signing/authentication;
- replay-resistant timestamp/nonce or equivalent verification guidance;
- secret rotation with overlap/grace;
- per-destination concurrency/backpressure;
- bounded redirects or no redirects according to policy;
- timeout/response-size limit;
- delivery history/retry;
- disable/quarantine path for repeatedly failing destinations.

A webhook destination can never grant the remote system SquiFlow tenant/admin authority by itself.

## 7. Templates and privacy

Tenant-editable templates are bounded data/templates, not arbitrary server-side code.

Define which facts a template can reference. Prevent HTML/script/template injection according to output channel.

Operational logs should contain identifiers/result categories rather than full message bodies or sensitive destinations by default.

## 8. Notification preferences and workflow

Do not send every workflow state change externally.

For each notification ask:

- who needs to know;
- whether the in-app work inbox is sufficient;
- whether the event is material enough for external notification;
- who can configure/disable it;
- what happens if delivery is delayed;
- whether duplicate notifications are harmful;
- whether a customer consent/legal rule applies.

## 9. Provider failure and cost

Paid providers are part of the resource budget.

Track:
- send rate;
- retries;
- provider failures;
- per-tenant/channel usage where relevant;
- cost/quota exhaustion;
- backlog age.

Provider outage or exhausted quota produces a visible degraded delivery state without corrupting the originating business transaction.

## 10. Implementation timing

Do not build a general communications platform in Phase 0.

Introduce this capability when the first complete journey needs external delivery, using the existing Core API/outbox/Worker boundaries. A separate notification service is justified only by later scale/deployment/fault-isolation evidence.
