# ByteByteGo Exhaustive Sequential Study — URL Entry 027

# URL 027 — Must-Know Deployment Strategies: From Big-Bang to Progressive Delivery

## A. Identification

- **URL entry:** `027`
- **PDF page:** `271`
- **Source URL:** `https://blog.bytebytego.com/p/must-know-deployment-strategies-from`
- **Public source access:** paid post; public preview inspected. Subscription controls were not bypassed and hidden strategy details are not reconstructed.
- **Related visual:** archive page `184`, general production-shipping/deployment visual. It is supporting context, not proof of the inaccessible article's full strategy taxonomy.
- **Visual inspection:** PDF page `271` rendered and inspected in full.

## B. Core concept

### SOURCE

The accessible source frames deployment strategy as **risk management**. Production exposes code to real users, traffic, infrastructure, and unknown interactions, so deployment approaches evolved to control the damage when a release is bad. The preview says some strategies reduce blast radius through gradual exposure, while others separate deployment from release so software may already be present before users are allowed to exercise it. It describes an evolution from big-bang deployment toward progressive delivery and emphasizes cost and situational fit rather than one universally correct strategy.

### INFERENCE

The architectural question is not “Which deployment strategy is best?” It is: **what release risk exists in SquiFlow's actual topology, what failure can the strategy contain, and does the deployment have the spare capacity, routing, compatibility, telemetry, and recovery controls required for the strategy to be real?**

### EXTERNAL KNOWLEDGE / CAVEAT

Common deployment terms such as rolling, blue-green, canary, feature flags, dark launch, and progressive exposure have overlapping but distinct mechanics; this study does not attribute a hidden exact list to the inaccessible ByteByteGo content.

A deployment strategy cannot make an incompatible database migration safe by itself. Likewise, a canary on one physical node may reduce user exposure to application behavior but does not create hardware redundancy or eliminate shared-database blast radius. Feature flags separate code deployment from feature exposure but add flag lifecycle, configuration, test-matrix, and stale-branch complexity.

## C. Important concepts

- immutable release artifact;
- deployment versus user exposure;
- blast radius;
- maintenance window;
- progressive exposure;
- health versus business smoke tests;
- compatible schema/data evolution;
- rollback versus roll-forward;
- spare capacity and routing;
- feature/configuration gates;
- observability and stop conditions;
- release ownership and operator recovery;
- single-node physical failure reality;
- skipped Workstation/client compatibility.

## D. Diagram / visual explanation

The related archive visual shows a general path from source control through build/test/deployment toward production. It is useful for separating build, verification, deployment, and runtime observation, but it does not establish that SquiFlow should use a particular progressive-delivery product or topology.

For SquiFlow, the useful decision path is:

```text
verified immutable artifact
    -> migration/config/capacity/dependency preflight
    -> choose release mechanism supported by actual node/routing capacity
    -> deploy
    -> process health + authorized business smoke
    -> observe defined failure signals
    -> continue, stop, roll forward, roll back, or enter maintenance recovery
```

## E. How it works — step by step

1. Build one immutable, versioned artifact and retain checksum/provenance evidence.
2. Promote the same bytes rather than rebuilding separately per environment.
3. Keep environment configuration and secrets outside the artifact.
4. Before deployment, check free space/capacity, required dependencies, configuration, and database migration compatibility.
5. Verify old/new API, durable work, database schema, and skipped Workstation compatibility where applicable.
6. Select a deployment/exposure strategy that the **real topology** can support.
7. Drain or bound in-flight work where the change requires it.
8. Deploy and check liveness/readiness.
9. Run a small authorized business smoke journey so “process is alive” is not confused with “release works.”
10. Observe predefined error/latency/resource/business signals and stop conditions.
11. Recover using the tested path: binary rollback, data-compatible roll-forward, maintenance restore, or another explicit procedure.
12. Remove temporary rollout/feature controls after the transition so they do not become permanent hidden complexity.

## F. Why it matters

SquiFlow's initial production environment is a constrained owned rack, not an elastic fleet. That makes deployment realism especially important. A sophisticated strategy can be **less safe** if it assumes spare nodes, duplicate databases, routing automation, or operator capacity that does not exist. A short honest maintenance window may provide lower total risk than pretending to have zero-downtime progressive delivery on one active node.

## G. Trade-offs / limitations

Progressive delivery can reduce user blast radius and improve evidence before full exposure, but it adds routing, version coexistence, telemetry, compatibility, cleanup, and operator decision complexity. Blue-green-style duplication consumes spare infrastructure. Rolling deployment exposes mixed versions and therefore depends on compatibility. Feature flags increase behavioral combinations and need ownership/expiry. Big-bang deployment is operationally simple but has a larger immediate blast radius. A maintenance-window release creates explicit downtime but can simplify recovery when topology is small.

## H. Alternatives / comparisons — fit, not winner/loser

```text
single-node maintenance deployment
    -> strong fit when one active node exists and honest bounded downtime is acceptable

rolling/progressive exposure
    -> fit when multiple compatible instances and routing/observability exist

blue-green-style duplicate environment
    -> fit when spare capacity and data compatibility make parallel stacks real

feature/configuration exposure control
    -> fit when deploy and user release need to be separated
```

These mechanisms can be combined, but only when each solves a concrete release-risk problem.

## I. Real implementation considerations / SquiFlow implications

- **KEEP:** immutable artifact promotion, externalized configuration/secrets, and deployment preflight.
- **KEEP:** database/schema compatibility and roll-forward-aware migration design are independent of rollout strategy.
- **KEEP:** process health plus an authorized business smoke journey.
- **KEEP:** explicit rollback/roll-forward/maintenance recovery rather than assuming database rollback.
- **KEEP:** on one active rack node, an honest maintenance window may be safer than fake zero downtime.
- **LATER / SCALE TRIGGER:** canary/rolling/blue-green/progressive exposure only when actual topology, spare capacity, routing, compatible state, telemetry, and operator recovery support it.
- **NEEDS MEASUREMENT:** deployment/restart/drain/smoke/recovery time on the real rack and representative workload.
- **AVOID:** selecting a strategy from maturity diagrams or assuming “progressive” automatically means safer.
- **AVOID:** claiming HA because two application versions are deployed on the same physical failure domain.

**What are we doing and why?** We currently require reproducible immutable releases with preflight, compatible migrations, health/smoke verification, and explicit recovery because those controls reduce the actual failure modes of SquiFlow's initial constrained deployment. We are not adding elaborate progressive-delivery topology yet because spare nodes/routing and the operational need are not proven.

**What would change this?** A multi-node production topology, a meaningful user base needing reduced exposure blast radius, frequent release incidents, or measured downtime/SLO requirements that justify the extra routing/version-coexistence/observability burden.

### Critical interrogation — answers intentionally withheld

**Foundation questions**
1. What does the accessible source say deployment strategy is primarily trying to control?
2. What distinction does the preview make between deployment and exposure/release?
3. Why is the related archive visual not evidence of the hidden full strategy list?

**Critical reasoning questions**
1. What exact release failure is SquiFlow's current maintenance-window approach protecting against?
2. Which failure would canary exposure reduce, and which shared DB/hardware failures would it not reduce?
3. Does the first rack have enough spare capacity to run two full versions safely under peak load?
4. What compatibility obligations appear when old and new processes coexist?
5. What evidence would make progressive delivery worth its operational complexity for the small team?

**Trade-off questions**
1. When is planned downtime safer than rolling deployment?
2. When is a feature flag preferable to a separate deployment environment?
3. What additional resource cost does blue-green-style duplication impose?
4. When can a rollback be more dangerous than a roll-forward?

**Failure / edge-case questions**
1. New binary is healthy but migration changed semantics for an old Workstation. What fails safely?
2. Canary traffic looks healthy but the shared DB is filling disk. What did the canary fail to isolate?
3. Rollback binary cannot understand the new schema. What recovery path remains?
4. Feature flag is disabled but background jobs created under the new behavior remain. How is that handled?

**Implementation questions**
1. What exact preflight checks run before release?
2. What business smoke journey proves Core/Admin/auth/database behavior after deployment?
3. What metrics and stop conditions govern a future progressive rollout?
4. How are old artifact, migration state, and runbook kept available for the supported recovery window?

**System design interview questions**
1. Design a safe first-paying-customer deployment on one rack node without pretending to have zero downtime.
2. Evolve that release process to two nodes while preserving database and Workstation compatibility.

**Challenge**
A team proposes Kubernetes canaries because “progressive delivery is safer.” The current production profile has one physical node, one central database, and limited RAM. Determine which actual release risks would improve, which would remain unchanged, and what evidence/topology would need to exist before adopting the proposal.

---
