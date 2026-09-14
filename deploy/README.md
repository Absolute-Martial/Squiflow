# Deployment and CI/CD representation

**Current state:** principles-first reset; no product executable is currently implemented.

This directory records deployment/reproducibility direction. It must not imply that a target process exists merely because architecture reserves a path for it.

## Current executable inventory

None.

Potential executable ownership locations remain documented in `docs/architecture/REPOSITORY_STRUCTURE.md`, including Workstation, Guard, tenant Web, compact CoreApi, and later earned WebApi/SyncApi/Worker/Admin/helper processes.

## Reintroduction rule

When the first executable is implemented, its change must include the minimum operational representation needed to run and verify it: configuration inputs, startup/shutdown behavior, health/liveness semantics where applicable, structured diagnostics, secret handling, and local verification commands.

Do not add Kubernetes/Terraform/container/service-manager complexity merely because deployment will eventually exist. Add the simplest reproducible packaging/deployment mechanism appropriate to the actual current topology.

## CI/CD

Repository CI/CD is allowed. GitHub and GitLab workflows should be thin wrappers around the same repository-owned verification commands/scripts, with self-hosted/self-managed runners preferred under hosted-minute constraints.

Runner registration credentials, SSH private keys, PATs, provider secrets, and deployment secrets must never be committed here.

## Production claims

The reset baseline makes no production deployment, availability, TLS, persistence, sync, Worker, Admin, backup/restore, update, or recovery qualification claim. Those properties are proven with the owning implementation rather than inferred from this directory.
