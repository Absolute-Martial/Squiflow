# Platform Admin Web topology decision

**Product version:** `v0.0.1`

**Decision owner:** `UIA-001`

**State:** topology selected; Admin Web runtime and trusted device-proof transport remain `NOT_INTRODUCED` until their implementation gate.

## Selected topology

Platform Admin Web is an independent private **Blazor Web App / Interactive Server** application with its own origin, process/deployment, cookie scope and ZITADEL **Platform Admin** application/client. It is not a privileged route inside Tenant Web and it never proxies authority through CoreApi.

Normal platform operations target AdminApi directly from the Admin Web server-side application boundary. Tenant cookies, tenant permission snapshots and tenant custom domains are not accepted as Platform Admin authority.

## Identity and device proof

Human authentication uses the ADM-004 Platform Admin project/application and a narrowly scoped server-managed `Secure`, `HttpOnly` session. The Admin Web never receives reusable OpenFGA administration credentials.

The existing AdminApi additionally requires a registered principal-bound client-certificate fingerprint observed on the trusted TLS connection. That requirement is preserved. A generic `X-Admin-Device`, forwarded identity header, private source IP or browser claim is not an accepted replacement.

Interactive Server cannot safely turn one shared server certificate into per-human device proof under the current AdminApi schema. Therefore Admin Web implementation has an explicit backend prerequisite: qualify one exact trusted device-proof transport that preserves the current principal/device binding (for example direct per-admin mTLS where feasible, or a cryptographically authenticated trusted-edge delegation contract). Until that is implemented and independently reviewed, Admin Web remains `NOT_INTRODUCED`; this decision does not weaken AdminApi.

Certificate private keys remain device/edge held according to the selected transport. They are never uploaded into application JavaScript or exported into tenant sessions.

## Availability and recovery

Admin Web/AdminApi private reachability and recovery are independent of Tenant Web/CoreApi availability and public tenant edge health. Losing CoreApi must not remove the operator recovery path.

## Exclusions

- no shared Tenant/Admin cookie or OIDC application;
- no CoreApi proxy for platform authority;
- no header-based substitute for the registered certificate boundary;
- no public Admin ingress claim;
- no Admin Web scaffold in this decision task.

## Requalification triggers

Requalify on AdminApi device-proof changes, trusted-edge/TLS termination changes, Platform Admin ZITADEL application changes, cookie/session delegation changes, or private ingress topology changes.
