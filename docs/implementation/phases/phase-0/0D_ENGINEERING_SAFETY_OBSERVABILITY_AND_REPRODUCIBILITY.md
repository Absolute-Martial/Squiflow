# Phase 0D — Engineering Safety, Observability, and Reproducibility

**Detailed phase index:** `docs/implementation/phases/README.md`

**Purpose:** Establish the safety rails that all subsequent feature development inherits instead of postponing them to a late hardening phase.

## 1. Principle

Phase 0 does not need production-scale telemetry, security tooling, deployment automation, or failure catalogs.

It **does** need the mechanisms that prevent future work from becoming unobservable, unreproducible, or architecturally unconstrained.

The rule is:

> Introduce the safety mechanism at the same time as the class of behavior that needs it; do not create every possible instance in advance.

This rule continues through every later phase. Phase 8 qualification is therefore a cross-system proof of controls already introduced, not the first time observability/security/recovery appear.

## 2. CI/build foundation

CI should prove at least:

```text
restore
build Release
deterministic kernel/spec tests
architecture dependency tests
basic secret detection appropriate to current repo
basic dependency/vulnerability review/triage path
artifact/version identity
```

Where the pipeline already does more, preserve it.

CI success must not be described as proof of future provider, rack, restore, or security exercises that did not actually run.

## 3. Architecture tests

Continue expanding `SquiFlow.Architecture.Specs` as real boundaries appear.

Phase-0 checks should protect things such as:

- shared capability/kernel code does not reference host/provider frameworks;
- Guard does not depend on business modules/ORM/provider authorization code;
- presentation projects do not obtain central DB credentials/persistence dependencies;
- provider contracts use SquiFlow-owned types rather than mirroring third-party SDKs;
- ordinary module composition remains in-process;
- future Worker/Admin projects are not created as empty architecture decoration.

Do not build a huge custom architecture-test framework if simple project/source checks prove the rule.

## 4. Failure vocabulary

Introduce stable source-controlled vocabulary as real failures appear:

```text
FailureCode
EventId / EventName
OperationId / CorrelationId / CausationId
```

Do not rely on exception message text as the stable support/API identity.

Do not preallocate hundreds of unused event codes.

## 5. Observability foundation

`SquiFlow.Observability` already exists and should remain the product-owned boundary over Serilog/OpenTelemetry concepts.

During Phase 0 prove the basics on current hosts:

- structured logs;
- service/component/version identity;
- correlation foundation;
- stable lifecycle/failure events;
- safe redaction rules for secrets/credentials;
- bounded exporter/buffering behavior appropriate to current implementation;
- telemetry failure never becomes business-authority failure.

As later phases add DB/sync/Worker/provider paths, those paths extend this same foundation.

## 6. Authoritative audit versus telemetry

Phase 0 should already preserve the conceptual distinction:

```text
operational telemetry
= diagnosable but potentially sampled/lossy

business/security audit
= durable authoritative evidence when a later operation requires it
```

Do not store future security/business audit only in OpenSearch/New Relic because telemetry exists first.

## 7. Secrets and build safety

Current repositories/artifacts must not contain:

- production passwords/tokens;
- reusable Workstation client secrets;
- central DB credentials embedded in desktop/Web artifacts;
- OpenBao root/unseal/recovery material;
- provider management credentials;
- unredacted secrets in normal logs/build output.

Environment-specific configuration remains external to immutable application artifacts.

## 8. Reproducible deployment foundation

`deploy/` should describe how the components that **actually exist** are started/stopped/configured.

It may evolve toward the current Podman server profile as server components are containerized.

Phase 0 does not require Kubernetes/Terraform/Flux merely to claim infrastructure-as-code.

A simple version-controlled Podman/service/runbook representation is acceptable when it is rebuildable and testable.

## 9. Developer/repository reproducibility

A second developer/machine should be able to discover:

```text
required .NET version
restore/build commands
test/spec commands
launch commands
required non-secret configuration
where each host begins
where architecture rules live
```

without relying on undocumented chat/history.

## 10. Other components continue evolving during 0D

0D is not an infrastructure-only phase.

Developers may continue:

- Customers/other capability work;
- Workstation/Web UI composition;
- Guard behavior;
- CoreApi endpoints;
- provider-contract work;
- tests;
- deployment definitions.

New real paths should add the minimum observability/failure/security evidence required by the path rather than waiting for a future observability phase.

## 11. Failure tests

Exercise current reality:

- secret accidentally added to a test config/source file is detected by the selected check where feasible;
- architecture dependency violation fails CI;
- invalid required host configuration fails explicitly;
- telemetry exporter unavailable does not crash a healthy host merely because telemetry cannot export;
- Guard crash/restart evidence is bounded;
- a crash loop does not create an unbounded logging/alert/restart storm;
- deployment/start instructions can be reproduced from repository/runbook state;
- build artifacts do not require embedded production secrets.

## 12. Resource bounds

Any Phase-0 queue/buffer/retry/restart mechanism must be bounded.

Examples:

```text
Guard restart attempts
telemetry buffers
local lifecycle log retention
HTTP request/body limits where exposed
```

Exact production numbers can remain provisional, but no mechanism is allowed to default to unbounded growth because a later phase will supposedly tune it.

## 13. Exit gate

0D is complete when:

- CI protects the most important current architecture rules;
- current hosts emit usable, safe structured lifecycle/failure evidence;
- correlation/failure vocabulary has a stable extension pattern;
- secrets/configuration are not embedded in source/artifacts;
- current deployment/startup can be reconstructed from repository/runbook information;
- telemetry failure is non-authoritative and bounded;
- new feature development has a clear path to add tests/observability rather than inventing a parallel mechanism.