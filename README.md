# SquiFlow

**Current architecture/documentation version: `v0.0.15`**

Start with [`MASTER_IMPLEMENTATION_PLAN.md`](MASTER_IMPLEMENTATION_PLAN.md).

This repository contains the current SquiFlow architecture baseline, implementation planning, adversarial review material, decision history, and supporting evaluation/reference documents.

## Current v0.0.15 baseline

- Small-team-first tenant model: Owner + Staff by default, with granular tenant-owned permissions.
- Tenant administration in the ordinary Web application's privileged Settings/Administration area.
- Separate platform administration Web application for SquiFlow operators.
- ASP.NET Core Core API and Worker as separate deployment/runtime boundaries.
- Windows Workstation remains the strongest local-first/offline client.
- Canonical browser identity authority; native Workstation login uses system-browser authorization-code + PKCE semantics.
- Tenant custom domains with ownership verification, TLS lifecycle, audit and fallback.
- Browser storage policy distinguishes secure cookies, memory, sessionStorage, localStorage, IndexedDB and Cache Storage.
- Web is online-first with preloaded application shell, draft preservation and selective future offline commands.
- Database products remain OPEN selections: PostgreSQL/SQLite are reference candidates; libSQL is an explicit local-store candidate.
- SquiFlow-native bounded rule engine is the baseline.
- OpenTelemetry remains provider-neutral instrumentation; New Relic + Aiven OpenSearch are the current managed observability targets.
- Historical versioned documents remain for traceability but do not override the current baseline.

## Repository navigation

- `MASTER_IMPLEMENTATION_PLAN.md` — consolidated current implementation plan.
- `docs/releases/SquiFlow_v0.0.15.tar.xz` — complete v0.0.15 architecture/documentation package.
- `docs/current/` — current high-value design documents surfaced for direct GitLab browsing.

## Versioning

SquiFlow architecture/documentation follows semantic-style pre-release versioning. This baseline is `v0.0.15`. A later architecture change should increment the version and update `VERSION`, `CURRENT_VERSION.txt`, this README, the master plan, and the current manifest.
