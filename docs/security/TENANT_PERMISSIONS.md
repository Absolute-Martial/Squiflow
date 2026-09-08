# Tenant Owner Roles, Permission IDs, and OpenFGA Authorization

**Version:** v0.0.15

SquiFlow is small-team-first. `Owner` and `Staff` are default templates, not fixed product roles.

**OpenFGA is the selected application-authorization engine.** ZITADEL authenticates the user; OpenFGA answers relationship/permission questions; SquiFlow still owns tenant isolation, business/domain state, workflow validity, idempotency, and concurrency.

## 1. Who decides Staff rights

The tenant Owner controls ordinary staff rights inside the tenant's entitlement and SquiFlow's non-overridable platform/security constraints.

The Owner may create custom roles such as Manager, Accounts, Designer, Sales, Stock or Print Operator. SquiFlow does not require those roles to exist.

## 2. Stable permission vocabulary

SquiFlow defines stable business capabilities such as:

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

These are product capabilities, not Web screen names or HTTP route names.

Tenant users may create custom role **instances** and choose which supported capabilities they contain. They do not invent arbitrary executable permission semantics.

## 3. OpenFGA modeling rule

Follow OpenFGA's domain-oriented custom-role pattern rather than generating a new authorization model for every tenant role edit.

Conceptually:

```text
OpenFGA authorization model
  defines SquiFlow object types + stable permissions/relations

relationship tuples
  assign users to tenant-specific custom role objects
  associate those role usersets with supported permissions/resources
```

Tenant-created role names/instances live as data/tuples. Stable SquiFlow permissions/relations live in the versioned authorization model.

Do not build a generic authorization meta-model that can express arbitrary code/policy languages merely because OpenFGA is flexible.

Use opaque SquiFlow identifiers in tuples, for example:

```text
user:01...
organization:01...
role:01...
order:01...
```

Do not put email addresses, customer names, free-form notes or other unnecessary PII in tuple identifiers.

## 4. Pin authorization model versions

Production OpenFGA calls specify an explicit `authorization_model_id` rather than silently targeting whichever model was most recently created.

Authorization models are versioned and immutable. A model change follows an explicit rollout/migration:

```text
create new model
→ validate/model-test
→ migrate required tuples where needed
→ deploy compatible application code
→ switch configured model ID
→ verify/shadow/check as appropriate
→ retire old path later
```

A tenant Owner editing a role normally changes tuples, not the model ID.

## 5. Permission assignment is Web-only

Roles and grants change only through authenticated tenant Web administration:

```text
Tenant Web → Settings → Team / Roles
→ /tenant-admin/... API
→ authenticate via ZITADEL-backed session
→ current tenant/delegation validation
→ durable authorization-change operation
→ OpenFGA tuple write/delete
→ verify/reconcile result
→ update SquiFlow role metadata + TenantAuthorizationRevision + audit
→ return applied state
```

The Desktop:
- can display effective permissions;
- can use a versioned snapshot for local UX/offline eligibility;
- **cannot write OpenFGA tuples, create roles, or grant/revoke permissions**.

Platform-level/operator authorization remains separate from ordinary tenant Owner authority.

## 6. Cross-system change correctness

OpenFGA and the SquiFlow business database are separate systems, so do not pretend a role/grant change is one ACID transaction across both.

A permission change must have a durable operation/idempotency identity and explicit states such as:

```text
Pending
ApplyingAuthorization
Applied
RetryableFailure
OutcomeUnknown
ReconciliationRequired
Rejected
```

Important rule:

> The UI/API must not report a grant/revocation as successfully applied until the intended OpenFGA state is known to be applied.

If the process crashes after an OpenFGA write but before SquiFlow records completion, retry/reconciliation checks the intended tuple state and completes idempotently rather than blindly duplicating/reversing it.

For revocation, do not rely on an old Workstation snapshot or token role claim after the server-side change is applied.

The exact durable change protocol is a Phase-1 implementation detail and is recorded as OPEN, but silent cross-system partial success is not acceptable.

## 7. ASP.NET Core integration

ASP.NET Core `IAuthorizationService` remains the Core API authorization integration primitive.

Typical request path:

```text
ZITADEL-authenticated actor
→ authoritative SquiFlow TenantContext
→ coarse endpoint policy
→ tenant-scoped resource load where appropriate
→ IAuthorizationService / semantic requirement
→ OpenFGA Check for relationship/permission where applicable
→ SquiFlow domain/workflow/business-state validation
→ expected-version/concurrency check
→ transaction
```

Examples of semantic application requirements:

```text
ApproveQuoteRequirement
RefundPaymentRequirement
AdjustInventoryRequirement
ManageRolesRequirement
```

A handler may invoke OpenFGA, but it remains side-effect-free with respect to the business command. Authorization succeeds or fails; mutation happens afterward.

## 8. OpenFGA does not own every rule

Keep these outside OpenFGA unless a concrete model proves otherwise:
- order/payment canonical state transitions;
- pricing calculations;
- stock arithmetic;
- credit exposure calculation;
- rule-engine facts;
- workflow transition validity;
- idempotency;
- optimistic concurrency;
- database tenant filtering/RLS.

For example:

```text
OpenFGA: user may attempt quotes.approve on this tenant/resource
SquiFlow domain: quote is currently Submitted and may transition to Approved
```

Both must pass.

## 9. Tenant isolation remains independent

OpenFGA authorization is not a substitute for pooled database tenant isolation.

When a request contains a resource ID, constrain the resource query by authoritative `TenantContext` where practical before fine resource authorization:

```text
TenantId = CurrentTenant
AND ResourceId = RequestedId
```

Then perform the OpenFGA/application action check.

A valid OpenFGA relation must never turn an unrestricted cross-tenant SQL query into acceptable data access.

## 10. Custom roles and scopes

Role assignments may support:
- Tenant;
- Branch/location;
- Program/project;
- Own/Assigned semantics where the resource model makes that clear.

OpenFGA is well suited to relationship-based scope, but do not create per-row relationship tuples for every entity merely because the engine can. Model resource relationships only where they simplify a real authorization rule.

Tenant-defined roles are first-class role objects/tuples, while supported permission relations remain defined by SquiFlow's authorization model.

## 11. State-aware operations

Do not create a permission name for every workflow status.

Use:

```text
OpenFGA permission/resource relationship
+
canonical resource state
+
workflow transition guard
```

Example:

```text
OpenFGA permits quotes.approve
AND quote state = Submitted
AND Submitted → Approved transition is currently valid
```

## 12. Property-level authorization

Request/response DTOs explicitly allow accepted/exposed properties.

Sensitive fields such as cost, margin, credit limit and privileged notes are exposed only where the action/context permits them.

A hidden Web field is not security. The API projection/command contract and authorization decision enforce it.

## 13. Delegation safety

A tenant role manager can grant only authority inside the actor's permitted delegation ceiling.

A role edit cannot create:
- platform-operator privilege;
- cross-tenant access;
- capability outside the tenant's entitlement;
- direct DB/infrastructure access;
- bypass of protected financial/security/domain invariants.

The API validates the requested role/permission relationship before writing OpenFGA tuples.

## 14. Authorization freshness and consistency

OpenFGA supports lower-latency and higher-consistency query modes. SquiFlow must choose consistency by operation risk rather than relying unknowingly on cache/replica behavior.

Initial direction:
- permission/role change verification and revocation-sensitive/high-risk operations use a consistency mode that provides the required freshness;
- ordinary low-risk reads may use the lower-latency mode when measured safe;
- do not force maximum consistency universally if it materially harms performance without benefit.

OpenFGA model ID, tuple state, and SquiFlow `TenantAuthorizationRevision` have different roles:
- model ID = authorization schema/version;
- tuples = current relationships/role assignments;
- `TenantAuthorizationRevision` = SquiFlow-visible effective authorization/config revision used for snapshots, invalidation and audit correlation.

The Workstation snapshot includes the SquiFlow authorization revision and never becomes a server capability token.

## 15. Owner lockout protection

Ordinary role editing must not leave the tenant with no recoverable Owner-level administrator.

Ownership transfer/removal is a separately guarded Web operation, can require ZITADEL step-up authentication, updates OpenFGA relationships deliberately, and is audited/reconciled like other high-risk authorization changes.

## 16. Testing requirements

At minimum test:
- Owner/Staff default role behavior;
- tenant-created custom role;
- multiple role assignments;
- role permission added/removed while user is logged in;
- OpenFGA write succeeds but SquiFlow completion persistence is interrupted;
- retry/reconciliation after ambiguous tuple write;
- pinned authorization model ID versus accidentally latest model;
- model migration with old/new app versions;
- lower-latency stale result versus higher-consistency path where applicable;
- cross-tenant object ID substitution;
- PII does not appear in tuple identifiers;
- Workstation offline action is rejected after server-side permission revocation;
- domain state rejects an action even when OpenFGA permission is allowed.

## Source basis

- OpenFGA concepts: https://openfga.dev/docs/concepts
- OpenFGA custom roles: https://openfga.dev/docs/modeling/custom-roles
- OpenFGA roles/modeling guidance: https://openfga.dev/docs/best-practices/modeling-roles
- OpenFGA model/version guidance: https://openfga.dev/docs/getting-started/immutable-models
- OpenFGA consistency modes: https://openfga.dev/docs/interacting/consistency
- OpenFGA tuple best practices: https://openfga.dev/docs/getting-started/tuples-api-best-practices
- ASP.NET Core resource-based authorization guidance
