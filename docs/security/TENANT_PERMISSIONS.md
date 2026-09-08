# Tenant Owner Roles, Permission IDs, and Resource Authorization

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

Permissions describe application actions, not Web screen names or HTTP routes.

## 3. Permission assignment is Web-only

Roles and permission grants are created/changed only through authenticated Web administration:

```text
Tenant Web → Settings → Team / Roles
→ /tenant-admin/... API
→ endpoint/function policy
→ current actor + delegation/resource authorization
→ role/grant validation
→ one authoritative transaction
     role/grant/membership change
     + TenantAuthorizationRevision increment
     + audit evidence
     + outbox/invalidation event
→ refreshed effective permission state
```

The Desktop:
- may display the current effective permissions;
- may disable/hide unavailable operations for UX;
- may evaluate allowed local UX/offline behavior using a versioned effective-permission snapshot;
- **must not grant, revoke or manufacture permission IDs/role assignments**.

Platform-level entitlements/permissions are managed only from SquiFlow Platform Admin Web.

## 4. Authorization is not one check

For an existing business resource, the normal server path is:

```text
Authenticate actor
→ derive authoritative tenant/platform context
→ endpoint/function policy
→ load/query resource inside that tenant/context where possible
→ ASP.NET Core resource/action authorization
→ canonical state + workflow/domain invariants
→ concurrency/version check
→ execute transaction
```

This addresses different classes of failure separately:
- **function authorization** — may this actor call this class of function at all?
- **object/resource authorization** — may this actor perform this action on this specific resource?
- **property authorization** — which sensitive fields may be read or changed?
- **business validity** — is the requested transition valid now?

A route beginning `/tenant-admin` or `/platform-admin` does not grant authority by itself.

## 5. ASP.NET Core implementation primitive

Use ASP.NET Core `IAuthorizationService` and resource-based authorization handlers for resource-specific decisions. Do not create a parallel home-grown authorization runtime merely to wrap the framework.

Coarse endpoint policies can run before resource loading. Fine-grained resource checks run imperatively after the resource or relevant authorization context is available.

Prefer semantic requirements for material actions, for example:

```text
ApproveQuoteRequirement
RefundPaymentRequirement
AdjustInventoryRequirement
ManageRolesRequirement
```

`OperationAuthorizationRequirement` is acceptable for genuinely CRUD-like resources, but important domain actions should not all collapse to generic `Update`.

Authorization handlers should be side-effect-free. Business mutation happens only after authorization succeeds.

For a Create command, authorize against the parent/scope/creation context because the final resource does not exist yet; do not manufacture a fake persisted object solely for authorization.

## 6. Tenant-scoped resource resolution

When a request contains a resource ID, do not first perform an unrestricted cross-tenant lookup and only later ask whether the user may access it.

Where practical, query using the authoritative tenant/context boundary:

```text
TenantId = CurrentTenant
AND ResourceId = RequestedId
```

Then perform finer action/resource authorization.

This reduces both cross-tenant access risk and cross-tenant existence leakage.

Random/UUID identifiers remain useful defense-in-depth but are never an authorization mechanism.

## 7. Scope

A role assignment can optionally be scoped to:
- Tenant;
- Branch/location;
- Program/department;
- Own/Assigned records only where that concept has clear business meaning.

Avoid a universal per-row ACL/relationship graph initially.

If later requirements introduce explicit resource sharing/deep relationship inheritance that roles/scopes cannot express cleanly, evaluate a relationship model then. Do not build Zanzibar-scale machinery speculatively.

## 8. State-aware operations

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

## 9. Tenant-created workflow stages

Tenant Owners may create configurable business workflow stages through Web administration where the module supports it.

Example:

```text
Canonical Order state: Accepted
Tenant stage: WaitingForArtwork
Tenant stage: DesignInProgress
Tenant stage: WaitingForPrint
```

Tenant-created stages do not replace protected states such as payment success/refund, posted invoice, stock movement or security/session state.

## 10. Property-level authorization and DTOs

Avoid binding client JSON directly to domain/persistence entities.

Request DTOs explicitly state which properties a command may accept. Response DTOs/projectors explicitly state which properties a caller may see.

Sensitive fields such as cost, margin, credit limit and privileged notes are included only when the caller has the required permission/context.

A field hidden in Web/Desktop UI is not protected unless the API also enforces the restriction.

## 11. Delegation safety

A user can delegate only permissions/scopes they are authorized to manage.

Role editing cannot manufacture:
- Owner/platform privilege;
- cross-tenant access;
- capability not included in tenant entitlement;
- direct DB/infrastructure access;
- bypass of protected business/security invariants.

A delegated role manager cannot grant more authority than their delegation ceiling.

## 12. Authorization revision and revocation freshness

SquiFlow adopts the **freshness lesson** from Zanzibar without adopting Zanzibar's relationship datastore/service.

Each tenant has a monotonically increasing `TenantAuthorizationRevision` (exact storage/type is implementation detail).

Advance it whenever effective tenant authorization can change, including:
- role definition/grant changes;
- membership suspension/removal;
- scope assignment changes;
- tenant entitlement changes that affect permissions;
- Owner transfer where authority changes.

The authorization mutation, revision increment, audit evidence and durable outbox/invalidation event commit atomically in the central store.

Initial server implementation should prefer authoritative checks over clever permission caching.

If a server cache is later justified:
- cache entries include the authorization revision in their identity/validity;
- a newer revision invalidates the old effective-permission result;
- sensitive commands must never rely on an unversioned stale authorization cache.

The Workstation effective-permission snapshot contains its authorization revision. Server sync still reauthorizes authoritatively; the snapshot is not a capability to bypass the server.

Pending/offline Workstation work that no longer has authority returns an explicit `AuthorizationChanged`/review result rather than a generic sync failure.

## 13. Search/read authorization

Authorization applies to reads as well as writes.

List/search/report queries must enforce tenant and relevant permission/resource filtering. If a derived read/search model is introduced, it must declare how authorization changes invalidate/rebuild/filter that projection and what freshness is acceptable.

Do not fetch an unrestricted tenant/cross-tenant result set and rely on the browser to hide unauthorized rows.

## 14. Owner lockout protection

Ordinary role editing must not leave the tenant with no recoverable Owner-level administrator.

Ownership transfer/removal is a separate Web-only guarded operation with audit and step-up authentication where required.

## Source basis

- OWASP API Security Top 10 2023
- Zanzibar: Google's Consistent, Global Authorization System
- ASP.NET Core resource-based authorization guidance

The full source reconciliation is in `docs/review/SECURITY_AUTHORIZATION_SOURCE_REVIEW.md`.
