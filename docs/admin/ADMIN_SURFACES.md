# Tenant Administration and Platform Administration

**Version:** v0.0.15

## Tenant administration

Tenant Owner/delegated settings live in the ordinary authenticated Web application under privileged Settings/Administration routes.

Includes Team, Roles/Permissions, branches/programs, rules/workflow, custom forms/fields, custom domains/branding, devices/workstations and tenant-level settings/audit.

For a two-person business this should feel like ordinary Settings.

Backend contract uses explicit `/tenant-admin/...` APIs and authoritative server authorization.

## Platform administration

`apps/admin-web` is the separate SquiFlow operator surface for tenants/subscriptions, platform entitlements, global configuration, incidents, runtime health, provider configuration, platform security and support operations.

Backend contract uses `/platform-admin/...` policies.

## Safety

The browser is never the authorization authority. High-risk operations use explicit commands, validation, exact diff, risk classification, step-up authentication/approval where required, durable audit, verification and rollback where possible.
