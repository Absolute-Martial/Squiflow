# Tenant Administration and Platform Administration

**Version:** v0.0.19

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

## 4. Platform administration is a separate backend and security boundary

The SquiFlow operator/platform-admin surface is intentionally separated from tenant/business administration.

Runtime boundary:

```text
apps/admin-web      Blazor Platform Admin UI
        ↓
services/admin-api  dedicated Platform Admin backend
```

`services/admin-api` is a separate ASP.NET Core executable/deployment/security boundary from tenant Web/Sync business hosts.

**Platform Admin Web must not depend on an ordinary tenant Web/Sync API process being available in order to perform platform-administration operations.** Normal platform-admin requests go to Admin API directly.

The backends may share reviewed Capability Cores/Foundation/infrastructure where appropriate, but ordinary platform-control work must not become:

```text
Admin Web
→ Admin API
→ tenant business API
```

Platform Admin is also a **private zero-trust control plane**. Reachability and application authority are separate gates.

Current deployment direction:

```text
approved registered Admin device
      +
Tailscale/approved private-network admission
      +
ZITADEL administrator identity
      +
platform authorization
      │
      ▼
Platform Admin Web/API
```

Cryptographically sensitive/high-risk operations additionally require the configured step-up, physical security/recovery factor, JIT/time-bounded elevation and/or independent approval according to risk.

Detailed network owner: `docs/operations/PRIVATE_ADMIN_NETWORK_AND_PODMAN.md`.

## 5. Platform Admin responsibilities

Create `apps/admin-web` and `services/admin-api` when the first real platform-control/Admin slice is implemented.

Potential responsibilities include:

- tenant/platform capability ceilings;
- platform feature/release controls;
- runtime health/incidents;
- provider configuration;
- support/break-glass **application** operations;
- privileged Worker/server application controls;
- platform authorization/operator administration;
- controlled cross-tenant support actions;
- encryption policy and key-lifecycle requests;
- device-key recovery/reprovisioning workflows;
- backup restore/recovery orchestration where owned by the application control plane.

Platform operators authenticate through ZITADEL but require separate platform-level SquiFlow/OpenFGA authority. Tenant roles can never imply platform authority.

Exact OpenFGA store/model separation for platform versus tenant authorization remains an implementation detail; security isolation between them is mandatory.

There is no architectural assumption that `SuperAdmin = everything`. Platform Admin entry and individual capabilities remain separate checks.

Illustrative capability separation:

```text
AdminPlatform.Access
Platform.Security.View
Platform.Encryption.Policy.View
Platform.Encryption.Policy.Change
Platform.Keys.Rotate
Platform.Keys.Revoke
Platform.Keys.Destroy
Platform.Device.Recovery
Platform.Backup.Restore
Platform.Identity.Configure
Platform.Support.AccessTenant
```

Exact permission names remain implementation/catalog details.

## 6. Admin API security and dependency rules

Admin API has its own:

- authentication/session validation;
- platform OpenFGA/authorization integration;
- registered Admin-device validation for protected access;
- trusted private-ingress/network context integration;
- rate/admission limits;
- audit/correlation;
- health/readiness;
- deployment configuration;
- service credentials/scopes;
- endpoint inventory;
- observability and failure handling.

It does not reuse a tenant/business session as proof of platform-admin authority.

Network location is never enough. A private/RFC1918/Tailscale address, tailnet membership, or trusted-proxy header alone cannot grant Platform Admin authority.

Admin API may access platform-owned persistence/provider/control-plane dependencies directly through least-privilege integrations where that is the correct ownership boundary. It must not require an ordinary tenant API process to proxy those calls.

Where Admin API and tenant business hosts touch shared authoritative data, they preserve the same invariants/transactions/authorization model through the same reviewed capability/application paths or explicit persistence contracts rather than duplicating rules differently.

## 7. Encryption/key management from Platform Admin

SquiFlow does not implement a custom cryptographic vault.

The normal key-management path is:

```text
Platform Admin Web
      │
      ▼
Platform Admin API
      │
      │ policy/lifecycle command
      ▼
SquiFlow key-management adapter
      │
      ▼
OpenBao (initial self-hosted candidate)
```

HashiCorp Vault remains a future provider alternative behind the same narrow SquiFlow-owned boundary.

Platform Admin owns policy/workflow/authorization/audit around operations such as rotation, revocation, recovery and restore. OpenBao/Vault owns cryptographic root/key storage and cryptographic operations.

The Admin UI/API does not normally expose raw KEKs, DEKs, OpenBao root/seal material, recovery shares, or a generic `ShowMasterKey`/raw-key export operation.

Encryption administration is **not** tenant customer-data browsing authority. Cross-tenant support access is a separate permission and workflow.

Detailed owners:

- `docs/security/ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md`;
- `docs/security/ENCRYPTION_POLICY_KEY_LIFECYCLE_AND_PRIVILEGED_ACCESS.md`;
- `docs/security/OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE.md`.

## 8. Critical server tasks during normal operation

When Platform Admin exists, supported application-level controls such as Worker pause/drain/retry/quarantine, provider config, encryption policy/lifecycle requests, and cross-tenant support operations are exposed only through Admin Web → Admin API.

Do not expose these controls through:

- Workstation;
- Sync API;
- ordinary tenant Web API;
- tenant Settings;
- compatibility routes on another business backend.

Do not create generic `run SQL`, `set anything`, `force success`, `show master key`, or `mark payment/job complete` controls.

## 9. Failure independence

The purpose of a separate Admin API is not merely code organization. It provides an independent application control surface.

Required behavior:

- ordinary tenant Web/Sync API outage does not automatically make Admin API unavailable;
- Admin API outage does not block ordinary already-provisioned tenant business operation;
- deploying/restarting Admin API does not require restarting tenant Web/Sync API hosts;
- Admin API can inspect/control implemented platform resources it owns even when a business host is unhealthy, unless the underlying dependency itself is unavailable;
- an Admin API failure must not accidentally fail open into tenant authority.

This does not imply every data-plane operation is independent of OpenBao. If a specific server operation genuinely requires an online Transit/key-service cryptographic operation, that operation fails closed when the key service is unavailable. Application-level Transit encryption is therefore selective based on data classification rather than imposed on every ordinary field.

Workstation local DB access must not require continuous Admin/OpenBao connectivity after legitimate provisioning.

## 10. Application control plane is not infrastructure recovery

If Admin Web/Admin API itself is unavailable, recovery cannot depend on it.

A separate private infrastructure runbook may be used for:

- OpenBao bootstrap/unseal/recovery;
- restart/redeploy of Admin API/business API/Sync API/Worker;
- node replacement;
- DB recovery required for application startup;
- ZITADEL/OpenFGA/storage connectivity/config recovery required to restore application operation;
- network/config repair required to bring the control plane back.

This is not a second hidden business API and is never exposed to tenant users or Workstations.

OpenBao bootstrap must not create a circular dependency where Admin API is required to unseal the key service Admin API itself needs.

Owners:

- `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`;
- `docs/operations/PRIVATE_ADMIN_NETWORK_AND_PODMAN.md`;
- `docs/security/OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE.md`.

## 11. High-risk application operations

For high-risk platform operations actually implemented:

```text
proposal/current state
→ validate in Admin API
→ show material diff in Admin Web
→ ZITADEL step-up/recent authentication where required
→ registered Admin-device proof
→ current platform OpenFGA/SquiFlow authorization
→ physical security/recovery factor where required
→ JIT/time-bounded elevation where appropriate
→ independent approval where required
→ execute/enqueue through the owning provider/capability
→ verify
→ authoritative audit
```

The highest-risk destructive operations may require four-eyes approval, including key destruction, destructive full restore, disabling mandatory encryption controls, reducing recovery requirements, changing root identity/key-provider configuration, or granting highest platform-security authority.

Do not require enterprise approval workflows for ordinary low-risk tenant settings.

A hidden/disabled button is UX only; server authorization remains authoritative.

## 12. Break-glass and support access

Break-glass is a narrow emergency workflow, not a standing universal administrator account. It uses strong identity/device/private-infrastructure evidence, required physical/recovery factors or quorum, high-severity evidence/audit, and post-use credential/factor review/rotation where appropriate.

Cross-tenant support access is separate from encryption/platform administration. If implemented, it requires its own permission and reason/ticket/incident context, exact tenant/resource scope, time-bounded/JIT access, optional approval or tenant acknowledgement according to policy, automatic expiry/revocation, and authoritative audit.

## 13. Device/workstation lifecycle

Tenant device/workstation lifecycle is a tenant capability and may be surfaced on Web and, where useful/authorized, Workstation.

Tenant administrators may manage enrollment visibility, revocation/suspension, friendly name and supported device policy through server-authoritative operations.

The Web can show authorized organization device/sync state. A Workstation always may show its own local/device sync details, and may show other organization devices only when current authorization permits it.

User identity, SquiFlow tenant membership/OpenFGA authorization, device enrollment and local bytes are separate concerns.

Revoking a user does not necessarily delete a device record, and revoking a device does not erase bytes already stored offline.

Platform **Admin devices** are a different security lifecycle: protected Platform Admin access requires an explicitly registered, non-revoked Admin device. Revoking an Admin device prevents future protected Admin use from that credential/device but does not claim to erase secrets already exposed on a fully compromised running machine.

Detailed tenant device owner: `docs/workstation/DEVICE_AND_SYNC_MANAGEMENT.md`.

## 14. Authoritative audit

Material platform-security operations create durable authoritative audit state separate from lossy telemetry. Audit records carry safe evidence such as actor, action, target, tenant/device where applicable, reason/approval, before/after version, correlation, timestamp and outcome without recording raw keys, recovery shares, bearer tokens or provider secrets.

OpenTelemetry/Serilog may receive a safe operational copy, but they are not the authority for security/key-management history.
