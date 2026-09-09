# URL 048 — Infrastructure as Code Landscape

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: a comparison, checklist, pattern catalog, popularity claim, maturity ladder, or source diagram never selects SquiFlow architecture by itself. The review must first identify what SquiFlow is actually doing at the corresponding boundary, why that mechanism exists, what authority it owns, what it costs, and what evidence would justify changing it.

## A. Identification

- **URL occurrence:** `048`
- **PDF page:** `292`
- **Source URL:** `https://blog.bytebytego.com/p/ep145-infrastructure-as-code-landscape`
- **Source access:** public newsletter section accessible.
- **Related supplied visual:** archive page 20 IaC landscape.
- **Visual inspected:** PDF page `292` at full size.

## B. Core concept

### SOURCE

The accessible section presents containerization, container orchestration, Infrastructure as Code, and GitOps as common infrastructure-automation strategies. It says IaC represents infrastructure/configuration in versionable, testable, reusable files and names Terraform, CloudFormation and Ansible examples; GitOps combines declarative configuration with Git and CI/CD for automated infrastructure updates.

### INFERENCE

The durable requirement is reproducible, reviewable infrastructure and configuration. The source's sequence of Docker -> orchestration -> IaC -> GitOps should not be treated as a maturity ladder that SquiFlow must climb.

### EXTERNAL KNOWLEDGE / CAVEAT

The source says orchestration becomes a necessity when dealing with multiple containers; that is too strong. A small number of containers/processes on one/few hosts can be managed reliably with simpler service/container tooling. IaC can be declarative or imperative and not every environment needs Terraform. GitOps introduces controllers/reconciliation/credential/rollback semantics and is most valuable where the topology and change volume justify them. Configuration code does not mean tenant operators should edit infrastructure files for business settings.

## C. Important concepts

- reproducible environment definition;
- immutable artifacts and external configuration;
- host/service/container definitions;
- provisioning versus configuration management;
- version review and drift evidence;
- secret separation;
- clean-environment rebuild;
- migration/preflight/rollback or roll-forward;
- containerization versus orchestration;
- GitOps reconciliation;
- small-team operating cost;

## D. Diagram / visual explanation

The visual places containerization, Kubernetes orchestration, Terraform/Ansible-style tools, Git and CI/CD in one landscape. For SquiFlow it is a capability map: packaging, provisioning, configuration, orchestration and change reconciliation are separate concerns. The presence of Kubernetes/Terraform/Ansible logos is not a selection.

## E. How it works — step by step

1. Record the exact first-production topology: processes/containers, edge, DB, storage, identity/authz dependencies, observability and private recovery prerequisites.
2. Store deployment/service/configuration definitions and runbooks in version control.
3. Build one immutable versioned artifact/image and keep environment secrets/config outside it.
4. Automate or document preflight, migration, health and authorized smoke checks.
5. Prove a clean/replacement environment can be rebuilt from the definitions/runbook.
6. Select the smallest automation mechanism that makes this repeatable for the real node count/change frequency.
7. Add orchestration or GitOps only when manual placement/reconciliation/drift/rollout becomes a measured recurring operational problem.

## F. Why it matters

SquiFlow's owned-rack deployment cannot depend on one person's memory. Reproducibility and recovery are production requirements even on one node. But a small team also pays heavily for unnecessary cluster controllers and multiple IaC layers.

## G. Trade-offs / limitations

More automation improves repeatability but can hide failures behind complex controllers, increase credential surface and make emergency recovery harder. Manual runbooks are simpler but can drift and be error-prone. Containerization can improve packaging without requiring orchestration. GitOps provides review/reconciliation but adds another control plane.

## H. Alternatives / comparisons — fit, not winner/loser

```text
versioned service/host scripts + runbook
    -> valid first fit for a small one-node topology if reproducible/tested

container compose/service definitions
    -> when packaging/isolation improves deployment

Terraform/Ansible/provider IaC
    -> when provisioning/config drift/rebuild complexity earns it

Kubernetes
    -> when real multi-node placement/reconciliation/rollout/failover complexity earns orchestration

GitOps controller
    -> when declarative cluster/infrastructure reconciliation/change workflow is itself valuable
```

## I. Real implementation considerations

Every adoption/change is required to state its owner/authority, failure behavior, recovery path, implementation evidence and operating burden. A source list is not implementation evidence.

### Implications for the Current Implementation

- **KEEP:** version-controlled reproducible deployment definitions/runbooks, immutable artifact promotion, config/secrets outside artifacts, preflight/migration/health/smoke and tested recovery.
- **KEEP:** exact IaC/automation mechanism remains open; simple host/container/service setup is valid if reproducible/testable.
- **NEEDS MEASUREMENT:** whether containers improve first profile packaging/resource isolation enough to use them and which automation tool best matches the actual rack/provider topology.
- **LATER / SCALE TRIGGER:** Kubernetes when multi-node orchestration pain is real; GitOps when reconciliation/change volume justifies a controller workflow.
- **AVOID:** adopting Terraform/Kubernetes/Flux merely to claim IaC/GitOps maturity or forcing business configuration into infrastructure files.

**What are we actually doing and why?** We are requiring reproducible version-controlled deployment and recovery because the owned rack must be rebuildable without tribal knowledge. We have intentionally not selected Terraform/Kubernetes/GitOps because their specific provisioning/orchestration/reconciliation problems are not yet proven; we will select the smallest mechanism that passes clean-environment rebuild and operational tests.

**Implementation-evidence status:** the repository is still documentation/planning only at the root (no application source tree committed). These are accepted design requirements and future verification gates, not claims that the controls/behavior already exist in running code.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. What is the difference between containerization, orchestration, IaC and GitOps?
2. What property makes infrastructure definitions useful for recovery?
3. Why are immutable artifacts separate from environment configuration?

**Critical reasoning**

1. What exact first-production infrastructure must SquiFlow recreate?
2. Which parts can be simple service definitions/runbooks and which might need a real IaC tool?
3. Why does more than one container not automatically require Kubernetes?
4. What does GitOps add beyond storing files in Git?
5. How does private break-glass recovery work if the normal deployment controller is broken?

**Trade-off**

1. When is Terraform/Ansible worth their state/credential/learning burden?
2. When does containerization improve deployment enough to justify it?
3. What operational pain would justify Kubernetes or GitOps?

**Failure / edge**

1. Original rack dies. Can a clean replacement be rebuilt without undocumented state?
2. IaC applies partially and DB migration succeeds but app health fails. What is the recovery path?
3. GitOps controller applies a bad config repeatedly. How is reconciliation stopped/recovered?
4. A secret is accidentally committed into IaC. What rotation/evidence process follows?

**Implementation**

1. Which deployment files/runbooks must exist before first paying customer?
2. How are checksums/provenance and previous known-good artifacts retained?
3. What clean-environment rebuild test proves reproducibility?
4. How are infrastructure credentials separated and rotated?

**System design interview**

1. Design the simplest reproducible SquiFlow one-rack deployment and its upgrade/recovery flow.
2. Explain the trigger path from one-host scripts to orchestration/GitOps without assuming a maturity ladder.

**Challenge**

1. A proposal adds Kubernetes, Terraform and Flux to manage Core API, Admin API, Worker and one DB on one physical server. Evaluate each tool by the exact problem it solves and the new failure/recovery burden rather than by industry popularity.
