# Tenant Administration and Platform Administration

**Version:** v0.0.18

## 1. Tenant administration

Tenant Owner/delegated settings live in the ordinary authenticated **Blazor Web App** under privileged `Settings / Administration` routes.

For a two-person customer this should feel like ordinary Settings, not a separate enterprise console.

Includes only as implemented:
- Team and invitations;
- Roles/permission assignments;
- Branch/program setup;
- Rules/workflow/forms;
- Custom domains/branding;
- Feature settings;
- Devices/workstations;
- Tenant-level audit/configuration history.

Authentication is through ZITADEL-backed Web session. Application authorization is current OpenFGA/SquiFlow authorization, not a ZITADEL role claim alone.

Tenant-control backend operations stay on the ordinary business backend under explicit `/tenant-admin/...` policies because they are still tenant-scoped application behavior.

Tenant role/permission assignment, workflow/rule/form publication and other tenant control-plane changes are initiated only from Web administration. Desktop consumes results/snapshots but cannot modify OpenFGA relationships or tenant control state.

## 2. Role/permission changes

Role changes are not direct browser-to-OpenFGA calls.

```text
Tenant Web
→ Core API /tenant-admin/roles...
→ ZITADEL-authenticated session
→ current OpenFGA ManageRoles/delegation check
→ validate requested permission ceiling
→ durable/reconcilable authorization change
→ OpenFGA tuple write/delete
→ verify/apply SquiFlow revision + audit
→ UI shows Applied only after outcome is known
```

The browser never receives OpenFGA administrative credentials.

## 3. Module, feature, setting, and permission administration

The tenant Settings UI consumes reviewed SquiFlow module descriptors:
- simple typed settings may use generated editors from safe display/validation metadata;
- complex, destructive, workflow-changing or security-sensitive settings require purpose-built screens;
- feature enable/disable shows dependency, data, durable-work and permission effects before publication;
- role editing lists stable module-owned permission definitions rather than accepting arbitrary permission strings;
- disabled-feature grants are shown as dormant, not erased;
- re-enabling a feature shows the effective permission diff and requires authorized confirmation before dormant grants reactivate.

Feature availability, settings and permission assignment are separate commands and revisions. Enabling a feature never grants a role. Hiding a UI element never replaces Core API authorization.

Installing/updating module assemblies is a reviewed deployment operation, not a tenant Settings action. Initial releases do not accept arbitrary uploaded plug-ins.

## 4. Platform administration is a separate backend boundary

The SquiFlow operator/super-admin surface is intentionally separated from the tenant/business backend.

Runtime boundary:

```text
apps/admin-web      Blazor Platform Admin UI
        ↓
services/admin-api  dedicated Platform Admin backend
```

`services/admin-api` is a separate ASP.NET Core executable/deployment/security boundary from `services/core-api`.

**Platform Admin Web must not depend on Core API being available in order to perform platform-administration operations.** Normal super-admin requests go to Admin API directly, not through `/platform-admin/...` routes hosted by Core API.

The two backends may share reviewed libraries/modules/contracts where appropriate, but they must not have a runtime HTTP dependency such as:

```text
Admin Web
→ Admin API
→ Core API
```

for ordinary platform-control work.

This separation exists because platform operators can perform cross-tenant, provider, runtime, support, and security-sensitive actions whose availability and attack surface should not be coupled to the tenant business API.

## 5. Platform Admin responsibilities

Create `apps/admin-web` and `services/admin-api` when the first real platform-control/Admin slice is implemented.

Potential responsibilities:
- tenants/subscriptions/entitlements;
- platform feature/config changes;
- runtime health/incidents;
- provider configuration;
- support/break-glass **application** operations;
- privileged Worker/server application controls;
- platform authorization/operator administration;
- controlled cross-tenant support actions.

Platform operators authenticate through ZITADEL but require separate platform-level SquiFlow/OpenFGA authority. Tenant roles can never imply platform authority.

Exact OpenFGA store/model separation for platform versus tenant authorization remains a Phase-6 implementation detail; security isolation between them is mandatory.

## 6. Admin API security and dependency rules

Admin API has its own:
- authentication/session validation;
- platform OpenFGA/authorization integration;
- rate/admission limits;
- audit/correlation;
- health/readiness;
- deployment configuration;
- service credentials/scopes;
- endpoint inventory;
- observability and failure handling.

It does not reuse a tenant/Core API session as proof of super-admin authority.

Admin API may access platform-owned persistence/provider/control-plane dependencies directly through least-privilege infrastructure integrations where that is the correct ownership boundary. It must not require Core API to proxy those calls.

Where Admin API and Core API both touch shared authoritative data, they must use the same data invariants/transactions/authorization model through shared reviewed application/domain code or explicit persistence contracts rather than duplicating business rules differently.

## 7. Critical server tasks during normal operation

When Platform Admin exists, supported application-level controls such as Worker pause/drain/retry/quarantine, provider config and cross-tenant support operations are exposed only through Admin Web → Admin API.

Do not expose these controls through:
- Workstation;
- `/sync`;
- ordinary tenant `/api`;
- tenant Settings;
- Core API `/platform-admin/...` compatibility routes.

Do not create generic `run SQL`, `set anything`, `force success`, or `mark payment/job complete` controls.

## 8. Failure independence

The purpose of a separate Admin API is not merely code organization. It provides an independent application control surface.

Required behavior:
- Core API outage does not automatically make Admin API unavailable;
- Admin API outage does not block ordinary tenant business API operation;
- deploying/restarting Admin API does not require restarting Core API;
- Admin API can inspect/control the implemented platform resources it owns even when Core API is unhealthy, unless the underlying shared dependency itself is unavailable;
- an Admin API failure must not accidentally fail open into tenant/Core API authority.

This does **not** imply that Admin API can function through a central database outage if the operation itself requires that database. Backend independence means no runtime dependency on the Core API process, not magical independence from shared infrastructure.

## 9. Application control plane is not infrastructure recovery

If Admin Web/Admin API itself is unavailable, recovery cannot depend on it.

A separate private infrastructure runbook may be used for:
- restart/redeploy of Admin API/Core API/Worker;
- node replacement;
- DB recovery required for application startup;
- ZITADEL/OpenFGA/storage connectivity/config recovery required to restore application operation;
- network/config repair required to bring the control plane back.

This is not a second hidden business API and is never exposed to tenant users or Workstations.

Owner: `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.

## 10. High-risk application operations

For high-risk operations actually implemented:

```text
proposal/current state
→ validate in Admin API
→ show material diff in Admin Web
→ ZITADEL step-up/recent authentication where required
→ current platform OpenFGA/SquiFlow authorization
→ approval where actually required
→ execute/enqueue
→ verify
→ audit
```

Do not require enterprise approval workflows for ordinary low-risk tenant settings.

A hidden/disabled button is UX only; Admin API authorization remains authoritative.

## 11. Device/workstation lifecycle

Tenant Settings may manage enrollment visibility, revocation/suspension, friendly name and supported device policy.

User identity, SquiFlow tenant membership/OpenFGA authorization, device enrollment and local bytes are separate concerns.

Revoking a user does not necessarily delete a device record, and revoking a device does not erase bytes already stored offline.
