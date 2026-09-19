# Deployment and CI/CD representation

**Current state:** the first backend/API vertical slice is implemented; no production deployment profile is qualified.

This directory records deployment/reproducibility direction. It must not imply that a target process exists merely because architecture reserves a path for it.

## Current executable inventory

- `SquiFlow.CoreApi` — public application bootstrap/liveness, generated OpenAPI v1 contract, configured JWT validation, authenticated account resolution and active tenant-membership listing.
- `SquiFlow.DbMigrator` — one-shot ordered IdentityAccess/Tenancy PostgreSQL migration with bounded advisory-lock coordination.

Potential executable ownership locations remain documented in `docs/architecture/REPOSITORY_STRUCTURE.md`, including Workstation, Guard, tenant Web, and later earned SyncApi/Worker/Admin/helper processes.

## Reintroduction rule

Each executable change includes the minimum operational representation needed to run and verify its declared scope: configuration inputs, startup/shutdown behavior, health/liveness semantics where applicable, structured diagnostics, secret handling, and repository-owned verification commands.

Do not add Kubernetes/Terraform/container/service-manager complexity merely because deployment will eventually exist. Add the simplest reproducible packaging/deployment mechanism appropriate to the actual current topology.

## CI/CD

`eng/verify.sh` is the repository-owned restore/build/test entry point. `.github/workflows/verify.yml` is a thin wrapper around it and uses a hosted runner because current PostgreSQL integration evidence requires Docker without an owned runner being documented yet. Move to a self-hosted runner only when its patching, isolation, capacity and credential ownership are explicit.

Runner registration credentials, SSH private keys, PATs, provider secrets, and deployment secrets must never be committed here.

## Production claims

The current slices qualify their narrow CoreApi and PostgreSQL migration/query behavior only. They make no production deployment, availability, edge/TLS, tenant-owned persistence/RLS, sync, Worker, Admin, backup/restore, update, or recovery qualification claim. Those properties are proven with the owning implementation rather than inferred from this directory.
