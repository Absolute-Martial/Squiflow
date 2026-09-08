# Tenant Owner Roles, Permission IDs, and State-Aware Authorization

**Version:** v0.0.15

SquiFlow is small-team-first. `Owner` and `Staff` are default templates, not fixed product roles.

## 1. Who decides Staff rights

The tenant Owner controls ordinary staff rights inside the tenant's entitlement and SquiFlow's non-overridable platform/security constraints.

The Owner may create custom roles such as Manager, Accounts, Designer, Sales, Stock or Print Operator, but SquiFlow does not force those roles to exist.

## 2. Permission definitions and IDs

Permission definitions are stable server-owned action capabilities, for example:

```text
customers.view
customers.create
customers.edit
orders.view
orders.create
orders.edit
orders.cancel
orders.apply_manual_price
orders.approve
inventory.view
inventory.adjust
payments.record
payments.refund
quotes.create
quotes.approve
documents.print
team.invite
team.suspend
roles.manage
domains.manage
rules.manage
workflow.manage
```

The human-readable key is part of the stable contract. A database/internal identifier may also exist, but tenant users do not invent arbitrary permission IDs.

## 3. Permission assignment is Web-only

Roles and permission grants are created/changed only through authenticated Web administration:

```text
Tenant Web → Settings → Team / Roles
→ /tenant-admin/... API
→ current actor + delegation scope validation
→ role/grant update
→ authorization/session version invalidation as required
→ audit
```

The Desktop:
- may display the current effective permissions;
- may disable/hide unavailable operations for UX;
- may react to permission revocation after sync/reauth;
- **must not grant, revoke or manufacture permission IDs/role assignments**.

Platform-level entitlements/permissions are managed only from SquiFlow Platform Admin Web.

## 4. Scope

A role assignment can optionally be scoped to:
- Tenant;
- Branch/location;
- Program/department;
- Own/Assigned records only where that concept has clear business meaning.

Avoid a universal per-row ACL engine initially.

## 5. State-aware operations

Use:

```text
permission + canonical resource state + workflow transition guard
```

rather than inventing a permission for every status combination.

Example:

```text
permission: quotes.approve
current state: Submitted
transition: Submitted → Approved
```

The permission says **who may attempt the business action**. The domain/workflow state says **whether that action is valid now**.

## 6. Tenant-created workflow stages

Tenant Owners may create configurable business workflow stages through Web administration where the module supports it.

Example:

```text
Canonical Order state: Accepted
Tenant stage: WaitingForArtwork
Tenant stage: DesignInProgress
Tenant stage: WaitingForPrint
```

Tenant-created stages do not replace protected states such as payment success/refund, posted invoice, stock movement or security/session state.

## 7. Delegation safety

A user can delegate only permissions/scopes they are authorized to manage.

Role editing cannot manufacture:
- Owner/platform privilege;
- cross-tenant access;
- capability not included in tenant entitlement;
- direct DB/infrastructure access;
- bypass of protected business/security invariants.

A delegated role manager cannot grant more authority than their delegation ceiling.

## 8. Revocation

Future authoritative commands re-check current permission.

Pending/offline Workstation work that no longer has authority returns an explicit `AuthorizationChanged`/review result rather than a generic sync failure.

A screen rendered before revocation is not proof of authorization.

## 9. Sensitive field visibility

Field-level restrictions are allowed only where genuinely useful, for example:
- cost;
- margin;
- credit limit;
- privileged internal notes.

Do not turn every field into a generic ACL.

## 10. Owner lockout protection

Ordinary role editing must not leave the tenant with no recoverable Owner-level administrator.

Ownership transfer/removal is a separate Web-only guarded operation with audit and step-up authentication where required.
