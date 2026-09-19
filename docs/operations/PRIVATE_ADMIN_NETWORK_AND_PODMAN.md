# Private Platform Admin Network and Podman Deployment Boundary

**Status:** Accepted architecture direction for the current deployment profile  
**Version:** v0.1.0

## 1. Current deployment assumption

The current server profile uses **Podman containers**. Platform Admin and OpenBao are security-sensitive control-plane components and must not be exposed like ordinary tenant Web/API traffic.

Tailscale is the preferred current private-network ingress candidate. Another private network/VPN may replace it later if it provides equivalent or stronger authenticated private access and the deployment is updated explicitly.

## 2. Do not let the application infer trust from private IP addresses

Platform Admin must not authorize access with logic such as:

```text
if RemoteIpAddress is 10.x / 192.168.x / 100.x then allow admin
```

Reasons include:

- a private address is not an administrator identity;
- other LAN/tailnet machines may be compromised or unauthorized;
- container/rootless port forwarding may obscure or transform source addresses;
- proxy headers are spoofable unless accepted only from a trusted proxy path.

Network-path evidence is one gate, not application authorization.

## 3. Preferred Podman/Tailscale topology

The Admin backend is not directly published on a public/all-interface host port.

Target topology:

```text
Approved Admin device
       │
       │ Tailscale/private network
       ▼
Tailscale policy / Grants
       │
       ▼
Tailscale Serve or equivalent trusted private reverse proxy
       │
       │ localhost only
       ▼
127.0.0.1:<admin-port> on Podman host
       │
       ▼
Platform Admin Web/API container
```

The Podman publication should bind the Admin service to loopback or otherwise make it unreachable except through the trusted private ingress path, for example conceptually:

```text
127.0.0.1:8443 -> container:8443
```

rather than an all-interface/public binding.

Exact port numbers remain deployment configuration rather than architecture.

## 4. Network policy is deny-by-default

Tailnet/private-network membership alone does not imply Platform Admin permission.

The network policy should allow only explicitly approved administrator identities/devices/groups to reach the Admin endpoint. Ordinary tenant users, Workstations, shared infrastructure devices, and unrelated tailnet members do not automatically gain reachability.

Use Tailscale Grants/ACL-equivalent policy according to the current Tailscale version/configuration. SquiFlow authorization still runs after network admission.

## 5. Tailscale identity is network evidence, not SquiFlow authority

When using Tailscale Serve or another trusted private proxy, identity metadata may be propagated to the backend only when the backend is unreachable except through that trusted proxy.

A Tailscale login/header must not automatically become `PlatformAdmin`.

The application still requires:

```text
private network path verified
        +
ZITADEL administrator authentication
        +
registered active SquiFlow Admin device
        +
platform authorization
        +
step-up/physical factor for protected operations
```

Do not trust client-supplied forwarded/identity headers from arbitrary direct connections.

## 6. Registered administrator devices

Platform Admin access is restricted to explicitly registered Admin devices according to `ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md`.

A Tailscale user identity helps prove the network actor, but SquiFlow keeps its own Admin-device lifecycle for application security. Personal administrator laptops should remain human/user identities rather than being casually converted into machine tags merely to represent SquiFlow authorization.

Infrastructure/service machines may use Tailscale tags/identities where appropriate to their machine role.

## 7. Other private-network providers

The architecture does not require SquiFlow business code to depend on Tailscale specifically.

Equivalent deployments can use another WireGuard/VPN/private access solution if they preserve the security properties:

- no public Admin listener;
- authenticated private ingress;
- explicit network authorization;
- trusted gateway/proxy boundary;
- registered Admin-device/application authorization;
- TLS/mTLS/device-certificate controls as appropriate;
- no `private IP = administrator` shortcut.

A generic `TrustedNetworkContext` concept may exist at the Admin hosting/infrastructure boundary, but do not build a large VPN abstraction before a second implementation is real.

## 8. OpenBao exposure

OpenBao is more restricted than Platform Admin.

Normal topology:

```text
Admin browser
     │
     ▼
Platform Admin Web/API
     │
     ▼
SquiFlow key-management adapter
     │
     ▼
OpenBao private service endpoint
```

Do not expose the ordinary OpenBao HTTP API directly to tenant users, Workstations, or the public Internet.

Direct operator access to OpenBao exists only for an explicitly documented bootstrap/unseal/recovery procedure on the private infrastructure path.

## 9. OpenBao in Podman

For the current Podman profile:

- OpenBao runs in a dedicated container/process boundary with only required mounts/ports/capabilities;
- its persistent Integrated Storage/Raft data, if selected, lives on protected durable host storage;
- seal/recovery material is not baked into the container image;
- production secrets are not passed through source-controlled container files;
- the service port is private and not publicly published;
- snapshots/backup follow the OpenBao-supported recovery process;
- container restart does not imply destructive reinitialization.

The exact rootless/rootful Podman choice, systemd/Quadlet representation, storage paths, and resource limits are deployment implementation details to document in the production runbook.

## 10. Bootstrap/unseal path

Platform Admin cannot be the only path for starting the key service it depends on.

A private bootstrap path may be:

```text
approved operator machine
     │
     │ Tailscale/private infrastructure access
     │ + physical/recovery factor
     ▼
private bootstrap/unseal endpoint/CLI on server
     │
     ▼
OpenBao unsealed
     │
     ▼
normal Admin API key-service identity becomes usable
```

This bootstrap path is narrowly scoped and must not become a general-purpose OpenBao management UI exposed to the network.

## 11. No continuous hardware-key attachment requirement for tenant operation

The operator's physical security factor is not intended to remain attached to the rack so ordinary business requests can run.

It is used for bootstrap/recovery/high-risk operations according to policy and can then be removed. Ordinary data-plane operations continue using already provisioned service identities/key-service state and Workstation local protected keys.

If a particular server-side protected-field operation requires online OpenBao Transit, key-service availability is an explicit runtime dependency for that operation and it fails closed when unavailable.

## 12. Public tenant traffic stays separate

Tenant business traffic may use the public edge according to the normal deployment design:

```text
Internet
   │
   ▼
public edge
   ├── tenant Web/WebApi
   └── approved Sync endpoints
```

Platform Admin uses the private control-plane path:

```text
approved Admin device
   │
   ▼
private network
   │
   ▼
private Admin ingress
```

Do not create a hidden `/platform-admin` route on the ordinary public Web/API host as a convenience bypass.

## 13. Verification

Before production, prove:

- public Internet cannot connect directly to Platform Admin port;
- normal LAN/non-approved tailnet identities cannot connect;
- allowed private-network identity can reach the private ingress;
- application still denies a reachable user without ZITADEL/platform authorization;
- a copied Admin session on an unregistered device is denied for protected access;
- forged Tailscale/forwarded identity headers sent directly to the backend cannot bypass the trusted proxy boundary;
- Admin API is not using apparent Podman `RemoteIpAddress` as its authority;
- OpenBao is not publicly reachable;
- loss of the public edge does not destroy the documented private recovery route;
- private recovery cannot be used as a normal tenant/business API.

## 14. Architectural invariant

> Platform Admin is private by network topology first and zero-trust by application policy second. Podman does not expose the Admin/OpenBao services publicly; a trusted private-network ingress admits only approved identities, and SquiFlow independently authenticates the administrator, verifies the registered Admin device, authorizes the exact operation, and requires stronger physical/step-up evidence for cryptographically sensitive actions.

Related owners:

- `docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`
- `docs/admin/ADMIN_SURFACES.md`
- `docs/security/ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md`
- `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`
