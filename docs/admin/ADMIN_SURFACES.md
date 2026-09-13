# Tenant Administration and Platform Administration

**Version:** v0.0.18

## 1. Tenant administration

Tenant administration is a SquiFlow capability, not a Web-only business implementation.

The ordinary authenticated **Blazor Web App** remains the primary tenant administration surface under privileged `Settings / Administration` routes. For small customers this should feel like ordinary Settings, not a separate enterprise console.

Where explicitly supported by the capability and useful to the customer, an authenticated **online Workstation** may expose selected tenant-administration actions such as:

- team/staff creation or invitation;
- role/permission assignment;
- device/workstation visibility and revocation;
- branch/program setup;
- other ordinary tenant-scoped administration specifically approved for the Workstation host.

These Workstation surfaces are only host adapters. They call the same server-authoritative tenant administration paths as Web. The Workstation never makes these changes locally authoritative or queues them for offline authority.

Some administration remains deliberately Web-only where desktop exposure provides no product value or would add unnecessary complexity. Rules/workflow/form authoring/publication, broad configuration editors, advanced reporting/administration and similar capabilities can remain Web-only unless a concrete Workstation requirement is accepted.

Includes only as implemented:
- Team and invitations;
- Roles/permission assignments;
- Branch/program setup;
- Rules/workflow/forms;
- Custom domains/branding;
- Feature settings;
- Devices/workstations;
- Tenant-level audit/configuration history.

Authentication is ZITADEL-backed. Application authorization is current OpenFGA/SquiFlow authorization, not a ZITADEL role claim alone.

Tenant-control backend operations remain server-authoritative tenant-scoped application behavior. Web/Workstation presentation never receives OpenFGA administrative credentials.

## 2. Role/permission changes

Role changes are not direct client-to-OpenFGA calls.

```text
Tenant Web OR authorized online Workstation
→ authoritative tenant administration endpoint/use case
→ ZITADEL-authenticated session/device context
→ current OpenFGA ManageRoles/delegation check
→ validate requested permission ceiling
→ durable/reconcilable authorization change
→ OpenFGA tuple write/delete
→ verify/apply SquiFlow revision + audit
→ client shows Applied only after authoritative outcome is known
```

The Workstation may cache permission snapshots for local UX/offline eligibility, but those snapshots cannot grant or revoke tenant authority.

## 3. Module, feature, setting, and permission administration

Tenant administration consumes reviewed SquiFlow capability/module descriptors:
- simple typed settings may use generated editors from safe display/validation metadata;
- complex, destructive, workflow-changing or security-sensitive settings require purpose-built screens;
- feature enable/disable shows dependency, data, durable-work and permission effects before publication;
- role editing lists stable capability-owned permission definitions rather than accepting arbitrary permission strings;
- disabled-feature grants are shown as dormant, not erased;
- re-enabling a feature shows the effective permission diff and requires authorized confirmation before dormant grants reactivate.

Feature availability, release channel, experiment assignment, settings, permissions and domain validity remain separate concepts. Enabling a feature never grants a role. Hiding a UI element never replaces server authorization.

Release-channel/Beta/experiment ownership is defined in `docs/architecture/FEATURE_RELEASE_AND_EXPERIMENTS.md`.

Installing/updating assemblies is a reviewed deployment operation, not a tenant Settings action. Initial releases do not accept arbitrary uploaded plug-ins.

## 4. Platform administration is a separate backend boundary

The SquiFlow operator/super-admin surface remains intentionally separated from tenant/business administration.

Runtime boundary:

```text
apps/admin-web      Blazor Platform Admin UI
        ↓
services/admin-api  dedicated Platform Admin backend
```

`services/admin-api` is a separate ASP.NET Core executable/deployment/security boundary from tenant Web/Sync business ingress hosts.

**Platform Admin Web must not depend on an ordinary tenant Web/Sync API process being available in order to perform platform-administration operations.** Normal super-admin requests go to Admin API directly.

The backends may share reviewed Capability Cores/Foundation/infrastructure where appropriate, but ordinary platform-control work must not become:

```text
Admin Web
→ Admin API
→ tenant business API
```

This separation exists because platform operators can perform cross-tenant, provider, runtime, support and security-sensitive actions whose availability and attack surface should not be coupled to ordinary tenant traffic.

## 5. Platform Admin responsibilities

Create `apps/admin-web` and `services/admin-api` when the first real platform-control/Admin slice is implemented.

Potential responsibilities:
- tenant/platform capability ceilings;
- platform feature/release controls;
- runtime health/incidents;
- provider configuration;
- support/break-glass **application** operations;
- privileged Worker/server application controls;
- platform authorization/operator administration;
- controlled cross-tenant support actions.

Platform operators authenticate through ZITADEL but require separate platform-level SquiFlow/OpenFGA authority. Tenant roles can never imply platform authority.

Exact OpenFGA store/model separation for platform versus tenant authorization remains an implementation detail; security isolation between them is mandatory.

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

It does not reuse a tenant/business session as proof of super-admin authority.

Admin API may access platform-owned persistence/provider/control-plane dependencies directly through least-privilege integrations where that is the correct ownership boundary. It must not require an ordinary tenant API process to proxy those calls.

Where Admin API and tenant business hosts touch shared authoritative data, they preserve the same invariants/transactions/authorization model through the same reviewed Capability Core/server application paths or explicit persistence contracts rather than duplicating rules differently.

## 7. Critical server tasks during normal operation

When Platform Admin exists, supported application-level controls such as Worker pause/drain/retry/quarantine, provider config and cross-tenant support operations are exposed only through Admin Web → Admin API.

Do not expose these controls through:
- Workstation;
- Sync API;
- ordinary tenant Web API;
- tenant Settings;
- compatibility routes on another business backend.

Do not create generic `run SQL`, `set anything`, `force success`, or `mark payment/job complete` controls.

## 8. Failure independence

The purpose of a separate Admin API is not merely code organization. It provides an independent application control surface.

Required behavior:
- ordinary tenant Web/Sync API outage does not automatically make Admin API unavailable;
- Admin API outage does not block ordinary tenant business operation;
- deploying/restarting Admin API does not require restarting tenant Web/Sync API hosts;
- Admin API can inspect/control implemented platform resources it owns even when a business ingress host is unhealthy, unless the underlying shared dependency itself is unavailable;
- an Admin API failure must not accidentally fail open into tenant authority.

This does not imply Admin API can function through a central database outage if the operation itself requires that database.

## 9. Application control plane is not infrastructure recovery

If Admin Web/Admin API itself is unavailable, recovery cannot depend on it.

A separate private infrastructure runbook may be used for:
- restart/redeploy of Admin API/business API/Sync API/Worker;
- node replacement;
- DB recovery required for application startup;
- ZITADEL/OpenFGA/storage connectivity/config recovery required to restore application operation;
- network/config repair required to bring the control plane back.

This is not a second hidden business API and is never exposed to tenant users or Workstations.

Owner: `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.

## 10. High-risk application operations

For high-risk platform operations actually implemented:

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

A hidden/disabled button is UX only; server authorization remains authoritative.

## 11. Device/workstation lifecycle

Device/workstation lifecycle is a tenant capability and may be surfaced on Web and, where useful/authorized, Workstation.

Tenant administrators may manage enrollment visibility, revocation/suspension, friendly name and supported device policy through server-authoritative operations.

The Web can show authorized organization device/sync state. A Workstation always may show its own local/device sync details, and may show other organization devices only when current authorization permits it.

User identity, SquiFlow tenant membership/OpenFGA authorization, device enrollment and local bytes are separate concerns.

Revoking a user does not necessarily delete a device record, and revoking a device does not erase bytes already stored offline.

Detailed owner: `docs/workstation/DEVICE_AND_SYNC_MANAGEMENT.md`.
