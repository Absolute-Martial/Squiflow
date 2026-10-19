# HAProxy ingress

Use HAProxy itself as the reverse proxy and API load balancer. A second load-balancer product is unnecessary for this topology. One API node works; add nodes when the deployment has the capacity and need. A single proxy or database remains a single point of failure.

This configuration forwards TLS unchanged. Public Web and CoreApi route by their configured TLS server names. AdminApi has a separate listener on a concrete private/VPN or loopback IP. The public listener has no Admin backend route. Applications terminate TLS and validate their own allowed hosts, tokens and permissions. AdminApi receives the person's actual TLS client certificate and applies its existing registration check; forwarded certificate/identity headers confer no authority.

The current Web has process-local login tickets and Blazor circuits, so this profile accepts one Web upstream. API upstreams may each contain 1–8 nodes sharing the same qualified PostgreSQL, authorization and configuration authority. This does not introduce replicated Web sessions, database failover or application high availability. The separate future Admin Web still requires its own accepted per-person certificate transport; this private API transport does not implement that browser-to-server application.

Supply a deployment-owned JSON file with exactly these fields. There are no deployment URI/address defaults:

| Field | Required value |
|---|---|
| `publicBind` | Literal public listener IP and port; IPv6 uses brackets |
| `privateAdminBind` | Concrete RFC 1918, IPv6 ULA, loopback or shared VPN (100.64.0.0/10) IP and port, without overlap with the public listener |
| `webHost`, `apiHost`, `adminHost` | Three distinct DNS host names matching application TLS certificates and allowed-host configuration |
| `upstreamCaFile` | Existing absolute path to the CA bundle validating application TLS certificates |
| `webUpstream` | One TLS endpoint as `host:port` |
| `coreApiUpstreams` | Array of distinct TLS API endpoints as `host:port` |
| `adminApiUpstreams` | Array of distinct private TLS Admin endpoints, separate from public upstreams |

Do not publish the upstream Admin port publicly. Firewall/container publication and VPN authorization must enforce the private path. Use host names resolvable in the proxy's runtime network. The CA-file path must also exist inside that runtime. HAProxy does not hold the applications' server private keys or administrator client keys.

Render with an installed HAProxy 2.8-compatible binary:

```sh
python3 eng/render-haproxy-config.py \
  --config "$APPLICATION_INGRESS_CONFIG" \
  --output "$APPLICATION_HAPROXY_CONFIG"
```

The renderer rejects missing/duplicate/unknown fields, URI or configuration injection, public/wildcard Admin bindings, overlapping listeners and unsupported Web replicas. It validates with `haproxy -c` before atomically replacing the output. The output directory must already exist. Run HAProxy in the deployment's existing service/container manager, with the generated configuration and CA bundle mounted read-only. Rendering does not start/reload services or change firewall rules.

TLS health checks verify certificate trust, host name and SNI. CoreApi/AdminApi require HTTP 200 from `/health/ready`; Web currently exposes `/health/live`. Two failed checks remove a node; two successful checks restore it, at five-second intervals. Long-lived TLS streams use a one-hour idle timeout and the proxy caps active connections at 1024. Review these operational bounds against measured load. Existing streams are not moved when a node becomes unready; interrupted commands retry using their existing semantic idempotency contract. Missing/unknown public SNI is rejected. Encrypted Client Hello routing is not supported by this profile.

`python3 eng/test-haproxy.py` runs the real binary against local TLS fixtures: API balancing/readiness removal and recovery, private certificate preservation, forged-header denial, public Admin rejection and atomic configuration failure. `HAPROXY_BIN` can select an already available binary; no downloads or services are started outside the test process. The CI ingress job runs this recurring check. These fixtures qualify the proxy behavior, not a deployed rack, production certificates, VPN/firewall rules or live external providers.

The separate `AdminTlsTransportTests` exercises actual AdminApi Kestrel certificate admission and device revocation against PostgreSQL/OpenFGA, without substituting the connection certificate provider.

References: [HAProxy 2.8 configuration manual](https://docs.haproxy.org/2.8/configuration.html), [current signed-in entry scope](../../docs/implementation/SIGNED_IN_APPLICATION_ENTRY.md), [private Admin network owner](../../docs/operations/PRIVATE_ADMIN_NETWORK_AND_PODMAN.md).
