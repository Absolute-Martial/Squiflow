# Concept Dependency Map Extension — URL 041-050

This extension preserves the standing rule: source comparisons/checklists/patterns do not choose technology. Every node below is connected to the SquiFlow boundary/problem it actually serves and to the evidence that would justify changing it.

```text
Security authority and recovery (041, 043)
  external TLS / edge exposure
    -> transport/exposure only
  ZITADEL OIDC/session
    -> human identity
  TenantContext
    -> authoritative tenant scope
  OpenFGA/resource handlers
    -> relationship/resource permission
  domain/workflow/current-state rules
    -> business authorization/invariants
  DB constraints/RLS/least privilege
    -> persistence correctness/defense in depth
  scanner/WAF/gateway
    -> detection/coarse edge controls, not business authority
  incident + restore + private break-glass
    -> recovery when normal security dependencies fail

Database access and concurrency (042, 044)
  workload + actual provider plan
    -> query/index decisions
  expected version
    -> ordinary lost-update protection
  DB constraint / atomic update
    -> DB-owned invariants
  provider-specific lock/isolation
    -> only named hot/multi-row invariant
  reconnect/import burst evidence
    -> proves contention/write/WAL costs

Version and compatibility (045)
  product SemVer
    -> immutable release identity/compatibility communication
  API contract version
    -> external client compatibility
  sync protocol/schema
    -> skipped Workstation/server compatibility
  DB/local schema
    -> persisted-state migration
  durable message/snapshot/IPC versions
    -> long-lived asynchronous/local compatibility
  none of these automatically version all the others

Data-layer scaling (046)
  fix query/schema/index/resource issue first
    -> when one-node inefficiency is the bottleneck
  vertical upgrade
    -> one-node capacity if economical
  read replica
    -> measured read load + safe staleness
  dedicated tenant placement
    -> isolation/residency/noisy-neighbor trigger
  shard/distributed DB
    -> measured write/data/placement limit + new transaction/routing/recovery contract

Stateless compute (047)
  process memory non-authoritative
    -> Core/Admin/Worker replaceability/restart safety
  Blazor circuit/session state
    -> permitted transient node state with explicit reconnect/failover contract
  Workstation local DB
    -> intentional local-first durable authority state
  distributed session/cache
    -> only if real multi-node Web topology requires it

Infrastructure automation (048)
  versioned definitions + runbook + immutable artifact
    -> current reproducibility requirement
  containerization
    -> packaging/isolation candidate
  Terraform/Ansible/etc.
    -> provisioning/configuration candidate when complexity earns it
  Kubernetes
    -> real multi-node orchestration trigger
  GitOps
    -> real reconciliation/change-workflow trigger

Latency (049)
  define user journey + completion semantics
    -> p50/p95/p99 + queue/dependency/DB/client decomposition
  query/index
    -> DB execution cause
  cache/precompute
    -> repeated safe-stale work
  CDN
    -> static/geographic cacheable delivery
  connection reuse/compression
    -> setup/bandwidth cause
  Worker async
    -> long-running acceptance-before-completion
  load balancing/more nodes
    -> only real multi-instance capacity problem

Dependency architecture (050)
  domain/application business rules
    <- dependencies point inward
  ASP.NET/Blazor/Avalonia/provider SDK/SQL
    -> outer details contained at composition/infrastructure edges
  IObjectStore / IBackupTarget
    -> real provider-replacement seams
  Guard / Admin API
    -> real process/security/availability boundaries
  generic repository / interface-per-class / project-per-ring
    -> not justified without a real dependency/replacement boundary
```

## Cross-links to earlier study

- URL 041/043 deepen archive 013/039/043/061/067/069/109/110 and URL 002/004/007 security/identity/API-control reasoning.
- URL 042/044 deepen archive 009/012/051/055/058/062 and URL 003/011/012/034 query/index/concurrency reasoning.
- URL 045 independently repeats archive 030's title while preserving the distinction between product SemVer and protocol/schema/message compatibility.
- URL 046 connects to archive scalability/database/Kubernetes/microservice material and URL 029/030 without converting scale techniques into a backlog.
- URL 047 revisits URL 006 and the Web Blazor-state caveat.
- URL 048 revisits archive 003/053/079 and deployment evidence from URL 027.
- URL 049 connects to URL 013/028/029 and archive performance work; latency remains a measured journey outcome.
- URL 050 strengthens the same anti-ceremony dependency rule established by archive 068/071/081 and URL 014/037.
