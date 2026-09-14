# Phase 8D — Deployment, Runtime, and Release Hardening

## Current topology only

Harden the deployment mechanisms actually selected. For the current server profile this may include Podman containers, private Admin exposure and host service/runbook definitions.

If containers are used, prove:

- reviewed/pinned base image;
- non-root/least privilege where practical;
- secrets outside image layers;
- minimal ports/capabilities/writable paths;
- health/shutdown/resource limits;
- final-image dependency/security scan;
- immutable image/artifact promotion.

## Network recovery

Exercise edge/DNS/TLS/certificate/time failure and verify the private recovery route does not depend on the same broken public edge.

## Configuration

Preflight required configuration/dependencies/capacity before unsafe exposure. One build/version should not become different untraceable bytes per environment.

## Restraint

Do not create Kubernetes, service mesh, multi-cluster delivery, canary controllers or other topology not justified by the current deployment. If later scale/topology earns one, that new boundary must re-enter the workload-selection and qualification process.

## Exit gate

The current topology is reproducible and hardened without introducing orchestration/IaC complexity that the real deployment does not yet need.