# Deployment and CI/CD representation

**Current state:** narrow backend/API slices are implemented; no production deployment profile is qualified.

This directory records deployment/reproducibility direction. It must not imply that a target process exists merely because architecture reserves a path for it.

## Current executable responsibilities

- `Application.CoreApi` — public bootstrap/health/OpenAPI and protected account, membership, workspace, customer organization/program and Orders draft APIs. Current operations and evidence are owned by `README.IMPLEMENTATION.md`.
- `Application.DatabaseMigrator` — one-shot ordered IdentityAccess/Tenancy/Customers/Orders PostgreSQL migrations with bounded advisory-lock coordination.

CoreApi fails startup until deployment supplies exact public branding, OIDC trust, pinned OpenFGA API/store/model/credential configuration, non-wildcard-all `AllowedHosts` and a restricted runtime PostgreSQL connection. Checked-in configuration exposes bounded OIDC/OpenFGA timing, public-bootstrap cache and database/profile-runtime resource policies; deployment qualification must review and override them where measured needs differ. DbMigrator separately requires `ConnectionStrings__PrimaryDatabase`, a deployment-specific nonzero `Migration__AdvisoryLockKey`, and `Migration__LockTimeoutSeconds` from 1 through 300. Run it with a migration credential separate from CoreApi. The distinct database provisioner applies and verifies the version-controlled existing-role grants in `deploy/database/` as described by `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`. Do not reuse a lock key from a different application migration boundary sharing the same PostgreSQL cluster.

Review `RequestBudget__ProtectedRequestTimeoutSeconds` (1–120; checked-in 30)
alongside database/provider timeouts, process admission and the eventual edge
policy. This is cooperative cancellation, not a hard kill or rollback guarantee;
`docs/implementation/CORE_API_REQUEST_BUDGETS.md` owns its exact response/retry
contract. Public health/readiness and bootstrap retain their separate policies.

`/health/live` reports process liveness without dependency calls. `/health/ready` returns only HTTP 200 or 503 with an empty body; it checks primary PostgreSQL connectivity and read access to the configured immutable OpenFGA model. Checks have a five-second timeout and a five-second process-local single-flight status cache so probe bursts do not multiply provider traffic. It does not prove migrations, tenant permissions, an authorized business journey or high availability. Keep the route on the monitoring/edge health path and pair it with the authorized smoke journey described in the deployment owner.

Potential executable ownership locations remain documented in `docs/architecture/REPOSITORY_STRUCTURE.md`, including Workstation, Guard, tenant Web, and later earned SyncApi/Worker/Admin/helper processes.

## Reintroduction rule

Each executable change includes the minimum operational representation needed to run and verify its declared scope: configuration inputs, startup/shutdown behavior, health/liveness semantics where applicable, structured diagnostics, secret handling, and repository-owned verification commands.

Do not add Kubernetes/Terraform/container/service-manager complexity merely because deployment will eventually exist. Add the simplest reproducible packaging/deployment mechanism appropriate to the actual current topology.

## CI/CD

`eng/verify.sh` is the repository-owned restore/build/test entry point. `.github/workflows/verify.yml` is a thin wrapper around it and uses a hosted runner because current PostgreSQL integration evidence requires Docker without an owned runner being documented yet. Move to a self-hosted runner only when its patching, isolation, capacity and credential ownership are explicit.

Runner registration credentials, SSH private keys, PATs, provider secrets, and deployment secrets must never be committed here.

## Production claims

The current slices qualify their narrow CoreApi, Orders, PostgreSQL RLS, and migration/query behavior only. They make no production deployment, availability, edge/TLS, sync, Worker, Admin, backup/restore, update, or recovery qualification claim. Those properties are proven with the owning implementation rather than inferred from this directory.

## Manual pricing permission rollout

The current model includes independent `manual_pricer` and computed
`can_apply_manual_price`. Publish the immutable model, grant intended accounts
explicit pricing tuples, pin its ID, then deploy the matching API. Initial
creation/preview require create plus pricing; full priced replacement requires
edit plus pricing, including unchanged submitted prices and retries. Read and
abandonment retain their separate authority. No startup grant/model mutation or
fallback to an older model exists. A missing pricing relation produces safe 503;
model readability alone is not qualification. Run an authorized smoke journey
including allowed/denied price entry and revocation. See
`docs/implementation/PRICING_COMPONENT_BOUNDARY.md`.

### Commitment and individual-record upgrade

Apply the module-owned Customers individual-record and Orders commitment
migrations using the separate one-shot migrator, then reapply
`database/grant-core-api-runtime.sql` with provisioning authority. Runtime DDL,
record/receipt deletion and Orders line UPDATE remain forbidden. The exact new
column grants are verified by the provisioning script. Publish the checked-in
tenant OpenFGA model and explicitly pin the new returned model ID; merely
publishing a newer model does not change an existing configured pin. Assign the
new independent relations only through the currently owned administrative path;
no tenant role-admin API is implied.

Configure `Admission__MaximumConcurrentRequestsPerTenant` (default 8, required
integer 1–256) alongside the existing global cap. Limits are process-local and
must be sized against real downstream capacity. Commitment rollback is deliberately
refused while committed orders exist, preserving their authority. Restore effects
and receipts together. This upgrade does not qualify a real identity deployment,
backup/restore drill or the remaining commercial lifecycle.
