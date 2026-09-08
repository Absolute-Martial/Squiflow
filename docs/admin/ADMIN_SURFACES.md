# Tenant Administration and Platform Administration

**Version:** v0.0.15

## 1. Tenant administration

Tenant Owner/delegated settings live in the ordinary authenticated **Blazor Web App** under privileged `Settings / Administration` routes.

For a two-person customer this should feel like ordinary Settings, not a separate enterprise console.

Includes:
- Team and invitations;
- Roles/permission assignments;
- Branch/program setup;
- Rules/workflow/stage/forms publication;
- Custom fields/forms;
- Custom domains/branding;
- Feature settings;
- Devices/workstations;
- Tenant-level audit/configuration history.

Backend contract uses explicit `/tenant-admin/...` APIs and authoritative server authorization.

### Web-only rule

Tenant role/permission assignment, workflow/rule/form publication and other tenant control-plane changes are initiated only from Web administration. The Desktop can consume effective configuration/permissions but cannot modify them.

## 2. Platform administration

`apps/admin-web` is a separate **Blazor Web App/security surface** for SquiFlow operators:
- tenants/subscriptions/entitlements;
- platform feature/configuration changes;
- runtime health/incidents;
- provider configuration;
- platform security;
- support/break-glass **application** operations;
- privileged Worker/server control-plane actions.

Backend contract uses `/platform-admin/...` policies.

## 3. Critical server tasks are Platform-Admin-Web only during normal operation

Examples:
- pause/drain/resume Worker classes;
- retry/quarantine/reconcile privileged failed jobs;
- rotate platform/provider application secrets through an approved flow;
- modify resource/deployment/runtime policies exposed as supported application controls;
- database/storage maintenance or restore orchestration where the application is healthy enough to coordinate it;
- global provider/domain configuration;
- cross-tenant support actions.

These are not exposed through Desktop, `/sync/...`, ordinary `/api/...`, or tenant Owner settings unless a specific tenant-scoped operation is intentionally designed there.

## 4. Application control plane is not infrastructure recovery

`Platform Admin Web only` cannot mean the platform is unrecoverable when Admin Web/Core API is itself down.

Keep a separate **private infrastructure break-glass plane** for recovery tasks that cannot pass through the application, for example:
- restart/redeploy a failed process/node;
- repair enough network/configuration for Core API/Admin Web to boot;
- replace failed physical hardware;
- restore the DB when the application cannot start;
- recover a secret/config dependency required to start the app.

This path:
- is private and least privilege;
- uses the selected infrastructure-access mechanism/runbook;
- is unavailable to tenant users/Workstations;
- is not a second hidden business/admin API;
- records recovery/operator evidence where feasible;
- returns normal control to Platform Admin as soon as the application is healthy.

Owner: `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`.

## 5. Safety workflow

The browser is never the authorization authority.

High-risk application operations use:

```text
Draft/Proposal
→ validate
→ exact material diff
→ risk classification
→ step-up MFA / approval where required
→ execute or enqueue durable command
→ verify outcome
→ audit
→ rollback/correction where possible
```

A user must distinguish a proposed change from an active one.

Do not provide generic `run SQL`, `set any config`, `force success`, or `mark payment/job complete` controls.

## 6. Accessible administration

High-risk control must remain operable and reviewable without depending on color/mouse-only interaction.

Material diffs expose structured before/after values and consequences to keyboard/screen-reader users. Focus/validation/approval state follows `docs/ux/ACCESSIBILITY_AND_INTERACTION_QUALITY.md`.

A disabled/hidden button is not authorization; API/application authorization remains authoritative.

## 7. Device/workstation lifecycle boundary

Tenant Settings can manage supported device/workstation lifecycle data such as enrollment visibility, revocation/suspension, friendly name and policy.

Device identity and user membership are different concerns. Revoking a user does not necessarily destroy a device record; revoking a device does not erase already stored offline bytes.

Exact credential rotation/re-enrollment implementation is owned by identity/device implementation and remains a Phase-1 design detail, not a reason for Desktop to become an administration authority.
