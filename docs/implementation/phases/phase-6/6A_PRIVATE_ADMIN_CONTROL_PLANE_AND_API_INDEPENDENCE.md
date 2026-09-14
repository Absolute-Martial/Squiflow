# Phase 6A — Private Admin Control Plane and API Independence

## Runtime boundary

Create Admin Web/Admin API only when a real platform-control use case exists.

```text
registered Admin device
+ approved private network/Tailscale admission
+ ZITADEL administrator identity
+ platform authorization
→ Admin Web
→ Admin API
```

Admin API is a separate executable/deployment/security boundary from tenant business APIs.

Admin-related domain/security contracts may exist earlier when needed. The independent Admin runtime should be pulled forward only when a real control-plane workload earns the process/security boundary; if pulled forward, its protection/failure-independence obligations come with it immediately.

## Independence

Normal platform administration must not be:

```text
Admin Web → Admin API → tenant Core/Web/Sync API
```

Admin API calls the owning platform/capability/provider boundary directly where appropriate while preserving shared business invariants.

## Exposure

Bind/publish Admin services only through the approved private topology. Network location is admission evidence, never application authority.

## First controls

Use only real controls needed now, such as key/provider health, device recovery, tenant/platform capability ceilings, Worker controls once Worker exists, or recovery operations owned by the application plane.

## Exit gate

A real platform operation can succeed while the ordinary tenant API process is stopped, provided underlying dependencies are healthy.