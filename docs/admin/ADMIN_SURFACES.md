# Tenant Administration and Platform Administration

**Version:** v0.0.15

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

Backend contract uses explicit `/tenant-admin/...` APIs and authoritative server authorization.

Tenant role/permission assignment, workflow/rule/form publication and other tenant control-plane changes are initiated only from Web administration. Desktop consumes results/snapshots but cannot modify OpenFGA relationships or tenant control state.

## 2. Role/permission changes

Role changes are not direct browser-to-OpenFGA calls.

```text
Tenant Web
→ /tenant-admin/roles...
→ ZITADEL-authenticated session
→ current OpenFGA ManageRoles/delegation check
→ validate requested permission ceiling
→ durable/reconcilable authorization change
→ OpenFGA tuple write/delete
→ verify/apply SquiFlow revision + audit
→ UI shows Applied only after outcome is known
```

The browser never receives OpenFGA administrative credentials.

## 3. Platform administration

`apps/admin-web` is a separate future Blazor Web App/security surface for SquiFlow operators.

Do not create that project in Phase 0. Create it in Phase 6 when the first real platform-control/Admin journey exists.

Potential responsibilities:
- tenants/subscriptions/entitlements;
- platform feature/config changes;
- runtime health/incidents;
- provider configuration;
- support/break-glass **application** operations;
- privileged Worker/server application controls.

Platform operators authenticate through ZITADEL but require separate platform-level SquiFlow/OpenFGA authority. Tenant roles can never imply platform authority.

Exact OpenFGA store/model separation for platform versus tenant authorization remains a Phase-6 implementation detail; security isolation between them is mandatory.

Backend contract uses `/platform-admin/...` policies.

## 4. Critical server tasks during normal operation

When Platform Admin exists, supported application-level controls such as Worker pause/drain/retry/quarantine, provider config and cross-tenant support operations are exposed there rather than through Desktop, `/sync`, ordinary `/api`, or tenant Settings.

Do not create generic `run SQL`, `set anything`, `force success`, or `mark payment/job complete` controls.

## 5. Application control plane is not infrastructure recovery

If Admin Web/Core API itself is unavailable, recovery cannot depend on it.

A separate private infrastructure runbook may be used for:
- restart/redeploy;
- node replacement;
- DB recovery required for app startup;
- ZITADEL/OpenFGA/storage connectivity/config recovery required to restore application operation;
- network/config repair required to bring the app back.

This is not a second hidden business API and is never exposed to tenant users or Workstations.

Owner: `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.

## 6. High-risk application operations

For high-risk operations actually implemented:

```text
proposal/current state
→ validate
→ show material diff
→ ZITADEL step-up/recent authentication where required
→ current platform OpenFGA/SquiFlow authorization
→ approval where actually required
→ execute/enqueue
→ verify
→ audit
```

Do not require enterprise approval workflows for ordinary low-risk tenant settings.

A hidden/disabled button is UX only; API authorization remains authoritative.

## 7. Device/workstation lifecycle

Tenant Settings may manage enrollment visibility, revocation/suspension, friendly name and supported device policy.

User identity, SquiFlow tenant membership/OpenFGA authorization, device enrollment and local bytes are separate concerns.

Revoking a user does not necessarily delete a device record, and revoking a device does not erase bytes already stored offline.
