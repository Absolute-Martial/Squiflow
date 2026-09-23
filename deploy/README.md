# Deployment and CI/CD representation

**Current state:** narrow backend/API slices are implemented; no production deployment profile is qualified.

This directory records deployment/reproducibility direction. It must not imply that a target process exists merely because architecture reserves a path for it.

## Current executable inventory

- `Application.CoreApi` — public application bootstrap/liveness/readiness, generated OpenAPI v1 contract, configured JWT validation, authenticated account and active tenant-membership resolution, pinned-model OpenFGA workspace/order checks, and Orders draft create/read/browse/abandon operations backed by tenant-scoped PostgreSQL RLS.
- `Application.DatabaseMigrator` — one-shot ordered IdentityAccess/Tenancy/Orders PostgreSQL migrations with bounded advisory-lock coordination.

CoreApi fails startup until deployment supplies exact public branding, OIDC trust, pinned OpenFGA API/store/model/credential configuration, non-wildcard-all `AllowedHosts` and a restricted runtime PostgreSQL connection. Checked-in configuration exposes bounded OIDC/OpenFGA timing, public-bootstrap cache and database/profile-runtime resource policies; deployment qualification must review and override them where measured needs differ. DbMigrator separately requires `ConnectionStrings__PrimaryDatabase`, a deployment-specific nonzero `Migration__AdvisoryLockKey`, and `Migration__LockTimeoutSeconds` from 1 through 300. Run it with separate migration credentials; then apply and verify the version-controlled CoreApi runtime-role contract in `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`. Do not reuse a lock key from a different application migration boundary sharing the same PostgreSQL cluster.

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
