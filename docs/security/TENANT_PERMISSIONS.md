# Tenant Owner Roles and Permissions

**Version:** v0.0.15

SquiFlow is small-team-first. `Owner` and `Staff` are default templates, not fixed product roles.

The tenant Owner controls ordinary staff rights inside the tenant's entitlement and non-overridable platform/security constraints.

## Permission model

Permissions are action-oriented and stable, for example `orders.create`, `orders.cancel`, `payments.refund`, `domains.manage` and `roles.manage`.

A role assignment can optionally be scoped to Tenant, Branch, Program, or Own/Assigned records where that concept has a clear business meaning.

Avoid a universal per-row ACL engine at the beginning.

## State-aware operations

Use:

```text
permission + canonical state + workflow transition guard
```

rather than inventing a permission for every status combination.

## Delegation

A user can delegate only permissions and scopes they are authorized to manage. Role editing cannot manufacture Owner/platform privileges or entitlements the tenant does not possess.

## Revocation

Future authoritative commands re-check current permission. Pending/offline work that no longer has authority returns an explicit `AuthorizationChanged` or equivalent review result instead of a generic failure.

## Owner lockout

Ordinary role editing must not leave the tenant with no recoverable Owner-level administrator. Ownership transfer/removal is a separately guarded and audited operation.
