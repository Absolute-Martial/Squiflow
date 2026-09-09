# ByteByteGo Concept Dependency Map — Entries 101-110 Extension

**Coverage:** archive entries `101-110`; together with prior maps, concept coverage is current through `110 — What is a Firewall?`.

This extension preserves the critical rule that comparisons/catalogs do not choose SquiFlow architecture. Each link names the exact surface, current mechanism, reason, alternative fit and adoption/falsification trigger.

## 1. API contract -> interface-specific transport

```text
101 good API
    -> explicit business intent
    -> semantic idempotency
    -> compatibility/versioning
    -> authn + TenantContext + OpenFGA + domain state
    -> concurrency/preconditions
    -> bounded pagination/cost
    -> stable errors

SquiFlow surfaces
    -> REST/task HTTP
        explicit resources/commands
    -> GraphQL candidate
        complex nested/client-selected reads
    -> gRPC candidate
        Workstation sync / high-frequency typed RPC
    -> SignalR/WebSocket
        live wake-up only
    -> Worker/outbox
        durable long-running consequence

critical rule
    -> interface style changes
       != authority/idempotency/domain correctness changes
```

## 2. Deployment isolation layers

```text
102 virtualization forms
    -> bare-metal host OS/processes
    -> hypervisor + VMs
    -> host kernel + containers
    -> VM + container composition

108 duplicate comparison
    -> same conceptual split independently rechecked

SquiFlow decision
    -> packaging mechanism OPEN
    -> choose from real rack resource/recovery/isolation evidence
    -> containerization != Kubernetes
    -> VM/container snapshot != backup
```

## 3. Provider capability -> provider-specific decision

```text
103 Cloudflare / AWS / Azure matrix
    -> edge / DNS / CDN / LB
    -> compute
    -> object storage
    -> databases
    -> workflow / AI / connectivity

SquiFlow current
    -> owned rack compute
    -> ZITADEL + OpenFGA external security dependencies
    -> Hugging Face IObjectStore bootstrap
    -> Kaggle IBackupTarget bootstrap
    -> managed observability

future provider choice
    -> identify one capability
    -> current official docs/limits/pricing/privacy
    -> POC + failure/recovery
    -> migration/export contract
    -> choose per capability, not per brand
```

## 4. Tech-stack popularity -> current foundation evidence

```text
104 stack catalog
    -> languages
    -> databases
    -> frameworks/runtimes
    -> deployment
    -> CI/version control
    -> caching
    -> architecture
    -> API styles/formats
    -> AI

SquiFlow accepted foundation
    -> C# / modern .NET
    -> ASP.NET Core
    -> Blazor Web App
    -> Avalonia Workstation
    -> modular monolith
    -> DB products still POC-gated

rule
    -> popular logo != missing architecture layer
    -> new runtime only for a concrete specialized boundary
```

## 5. Secure transport

```text
105 HTTP / HTTPS
    -> TLS confidentiality
    -> integrity
    -> endpoint authentication
    -> certificate/hostname/time validation

modern caveat
    -> TLS 1.3 commonly ECDHE-derived secrets
    -> HTTP/3 uses QUIC/UDP
    -> archive RSA/TCP-only model is simplified

SquiFlow
    -> external traffic TLS
    -> OIDC/OAuth over HTTPS
    -> transport version != business semantics
    -> no certificate-validation recovery bypass
```

## 6. Proxy direction

```text
106 forward proxy
    -> client-side/egress mediation
    -> optional enterprise egress policy

reverse proxy
    -> inbound server edge
    -> TLS / host routing / coarse admission

SquiFlow current
    -> reverse-proxy edge capability
    -> Core/Admin remain backend authorization authorities

future
    -> forward/egress proxy only if enterprise/egress-control need appears
    -> SSRF application policy still required
```

## 7. Concurrency vs parallelism -> capacity budget

```text
107 concurrency
    -> number of in-flight operations
    -> async I/O / DB / provider waits
    -> backpressure / fairness

parallelism
    -> simultaneous CPU work
    -> document/image processing

SquiFlow budgets
    -> DB pool + locks/WAL
    -> Worker slots
    -> provider quota
    -> CPU/RAM/temp/disk
    -> tenant fairness

critical rule
    -> more in-flight/parallel work is valid only until first constrained resource
```

## 8. Principal -> authentication mechanism

```text
109 authentication
    -> human Web
        ZITADEL OIDC + app session/cookie topology
    -> native Workstation
        Authorization Code + PKCE
    -> provider/service account
        provider-supported least-privilege client credentials
    -> future external machine
        API key / OAuth client credentials / mTLS / signed request by use case

unchanged after authentication
    -> TenantContext
    -> OpenFGA
    -> domain/current state
    -> resource/field limits
```

## 9. Network reachability -> application authority

```text
110 firewall
    -> ingress/egress reachability
    -> network/host filtering
    -> segmentation/private services

complementary controls
    -> reverse proxy/WAF
    -> TLS
    -> ZITADEL/OpenFGA/TenantContext/domain

SquiFlow hardening need
    -> explicit public/private port inventory
    -> direct-backend bypass test
    -> IPv4 + IPv6 exposure check
    -> outbound dependency inventory
    -> private recovery path

critical rule
    -> network allow != application permission
```

## 10. Integrated interrogation after entry 110

```text
source comparison/catalog
    -> exact SquiFlow boundary
    -> current accepted mechanism + reason
    -> actual implementation evidence level
    -> unresolved requirement
    -> candidate's specific benefit
    -> other option's better-fit surface
    -> coexistence possibility
    -> authority unchanged
    -> new failure/recovery/ops burden
    -> adoption evidence
    -> falsification/change trigger
```

## Next pending concepts

`111 — What is a REST API?` begins at PDF page `217`. Archive entries `111-123` and all 64 URL occurrences remain independently pending.
