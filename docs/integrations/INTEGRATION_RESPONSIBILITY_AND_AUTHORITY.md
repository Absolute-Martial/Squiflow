# Integration Responsibility and Authority

**Status:** Accepted shared contract for external integrations.  
**Authority boundary:** This document owns the common questions every external integration must answer. It does not replace provider-specific owners such as identity, authorization, payments, object storage, backup, notifications/webhooks, or future focused integration documents. It defines no runtime framework and does not require a separate integration service.

## 1. Why this exists

External systems can accidentally become authoritative outside the narrow responsibility they actually own. SquiFlow must therefore make each integration's responsibility, authority, freshness, failure and recovery semantics explicit before implementation depends on it.

Use this distinction:

```text
provider capability
≠ SquiFlow business authority
```

An external system may be authoritative for a provider fact or external effect while SquiFlow remains authoritative for the business meaning, tenant relationship, workflow state, historical interpretation, or recovery decision.

## 2. Required integration questions

For every material external integration, answer only the questions that apply:

```text
Why does this integration exist?
Which user/business outcome depends on it?
What does the provider own?
What does SquiFlow own?
Which external facts/effects are authoritative?
Which SquiFlow facts remain authoritative?
What data crosses the boundary, in which direction, and why?
What data may be cached or snapshotted?
What freshness/version evidence is required?
How is the provider/system authenticated?
How is the SquiFlow actor/resource authorized?
What happens on duplicate/retry/response loss?
When is OutcomeUnknown or reconciliation required?
What happens when the provider is slow, unavailable, rate-limited or quota-exhausted?
What resource/cost limits apply?
How are provider contract/schema/version changes detected?
What compatibility/deprecation window is required?
What evidence supports migration or provider replacement?
What support/audit evidence is retained without leaking secrets/PII?
```

Do not force every provider through one generic adapter or lifecycle when its semantics differ.

## 3. Authority examples

### Identity

```text
ZITADEL
= authoritative configured identity/authentication/session provider facts

SquiFlow
= tenant membership, device/application context and business interpretation

OpenFGA
= current application relationship/permission decision engine

SquiFlow domain + database
= workflow/business validity and tenant data isolation
```

A valid identity-provider session is not sufficient authorization to a tenant resource.

### Object storage

```text
object provider
= retained bytes and provider-level object result

SquiFlow database + authorization context
= object identity, tenant/business ownership, historical relationship and lifecycle meaning
```

A bucket path or provider ACL does not become the sole business ownership model.

### Notifications/webhooks

```text
provider
= delivery acknowledgement/remote transport result where trustworthy

SquiFlow
= originating business fact, delivery intent, retry/reconciliation state and user-visible interpretation
```

A delivery provider outage cannot rewrite the originating business transaction.

### Payments and other external effects

When a provider performs a financial or other irreversible external effect, define precisely which provider result is authoritative, what uncertainty remains, how idempotency is preserved, and how SquiFlow reconciles `OutcomeUnknown`. Never infer success merely because a request was sent.

## 4. Source of truth is scoped, not global

Avoid statements such as `provider X is the source of truth` without naming **for what**.

Prefer:

```text
provider X is authoritative for [specific external fact/effect]
SquiFlow is authoritative for [specific business fact/relationship/interpretation]
```

Some integrations legitimately have shared or staged authority. Make the transition explicit rather than hiding it behind a generic `sync` label.

## 5. Cached and derived data

A cache, imported copy, webhook payload, report or provider snapshot does not become current authority merely because it is locally available.

Where stale data is permitted, define:
- source;
- version/freshness evidence;
- acceptable staleness;
- what decisions may use it;
- what decisions require current provider/SquiFlow authority;
- invalidation/reconciliation behavior.

## 6. Failure and recovery

For a material integration, distinguish at least where applicable:

```text
explicit provider rejection
provider unavailable / timeout
rate or quota exhaustion
malformed/unexpected provider response
request accepted but acknowledgement lost
provider changed contract/version
provider state conflicts with SquiFlow state
```

Do not collapse these into one generic error if the recovery action differs.

Use bounded retry, idempotency and reconciliation according to the owning capability. Provider failure must never become accidental authorization success, silent duplicate financial effect, cross-tenant access, or fabricated business truth.

## 7. Replacement and abstraction

A provider abstraction is justified only by a real dependency-direction, replacement, fault, process, compatibility or multi-implementation need.

When justified:
- keep the contract SquiFlow-owned;
- keep it narrower than the provider SDK;
- preserve provider diagnostics needed for support;
- contract-test behavior that must survive replacement.

Do not create a generic integration framework merely because several providers exist. `IObjectStore` and `IBackupTarget` are justified current examples because provider replacement is already planned; that does not imply an `IWhateverProvider` for every dependency.

## 8. Security and privacy

Treat provider responses as external input even when the provider is trusted operationally.

Validate applicable schemas/statuses/sizes/redirects/URLs/signatures and timeouts. Use least-privilege credentials. Keep provider secrets out of business records, normal logs and client artifacts. Do not put unnecessary PII in external identifiers merely for convenience.

## 9. Cross-owner rule

The provider-specific focused owner remains authoritative for exact semantics. This shared document should be referenced conceptually, not copied wholesale into each owner.

If an integration decision changes tenant authority, business state, workflow, NFR, sync, security, limits, implementation sequencing or verification, update those affected owners under the Product Foundation cross-owner propagation rule.

## 10. Constructive challenge before adding an integration

Before introducing or expanding an integration, challenge it:

```text
What responsibility are we actually buying from this provider?
Could the work safely remain manual or use a simpler interface?
Are we creating a provider dependency before the product promise needs it?
Are we confusing provider convenience with business authority?
What happens if the provider disappears or changes terms?
Which irreversible data/effects make switching difficult?
What evidence would justify deeper integration?
```

A useful integration may still be accepted after these questions. The purpose is to expose costs and alternative boundaries, not to reject third parties categorically.