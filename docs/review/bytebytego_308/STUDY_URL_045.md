# URL 045 — What Do Version Numbers Mean?

## Review method

This occurrence is reviewed independently. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: a comparison, checklist, pattern catalog, popularity claim, maturity ladder, or source diagram never selects SquiFlow architecture by itself. The review must first identify what SquiFlow is actually doing at the corresponding boundary, why that mechanism exists, what authority it owns, what it costs, and what evidence would justify changing it.

## A. Identification

- **URL occurrence:** `045`
- **PDF page:** `289`
- **Source URL:** `https://blog.bytebytego.com/p/ep120-what-do-version-numbers-mean`
- **Source access:** public newsletter section accessible; exact archive-title overlap with archive 030 reviewed independently.
- **Related supplied visual:** archive page 128, Semantic Versioning.
- **Visual inspected:** PDF page `289` at full size.

- **Exact overlap:** archive entry `030` has the same title; this URL occurrence is independently reviewed.

## B. Core concept

### SOURCE

The accessible section explains Semantic Versioning as `MAJOR.MINOR.PATCH`: major for incompatible API changes, minor for backward-compatible functionality, patch for backward-compatible fixes. It shows early `0.1.0`, first stable `1.0.0`, later patch/minor/major releases, pre-release identifiers such as alpha/beta/rc, and build metadata.

### INFERENCE

SemVer is useful for communicating compatibility expectations of one versioned software/public-API unit. It does not by itself define SquiFlow's cross-version behavior between independently evolving product artifacts, Workstations, HTTP/sync protocols, database schemas, durable messages or snapshots.

### EXTERNAL KNOWLEDGE / CAVEAT

SemVer has precise precedence rules and special major-zero semantics: before 1.0.0 the public API is considered unstable and anything may change. More importantly, a software package's SemVer does not guarantee that database rollback, old Workstation compatibility or wire-message compatibility is safe. A major/minor/patch label is communication metadata; compatibility must still be designed and tested for each contract.

## C. Important concepts

- product/artifact version;
- public API compatibility;
- Workstation/server compatibility window;
- sync protocol/schema version;
- DB/local schema migration;
- durable message/job version;
- Guard/Workstation IPC version;
- rule/workflow/form snapshot version;
- pre-release/build metadata;
- retirement and migration evidence;

## D. Diagram / visual explanation

The visual shows major/minor/patch and pre-release/build suffixes plus an alpha -> beta -> RC -> release flow. SquiFlow should use this to communicate release identity, while keeping separate compatibility domains explicit. A `2.0.0` product release does not automatically mean the sync protocol is v2, and `1.2.3` does not prove a database migration can roll back.

## E. How it works — step by step

1. Assign one immutable application/release artifact version and provenance.
2. For each external/durable compatibility domain, define its own version or compatibility rules.
3. Prefer additive-compatible evolution where possible.
4. When a breaking contract is required, provide explicit new compatibility path and retirement window.
5. Before contraction/removal, inventory supported old readers/writers, skipped Workstations, pending sync and durable work.
6. Use telemetry/inventory and tests to prove old versions are drained or explicitly unsupported.
7. Treat SemVer as release communication, not as the mechanism that performs migration/reconciliation.

## F. Why it matters

SquiFlow's Workstation can skip releases and remain offline for long periods. Backend executables, DB schema, durable jobs and rule snapshots can also overlap. Compatibility therefore matters operationally even if every artifact has a neat SemVer.

## G. Trade-offs / limitations

Long compatibility windows reduce forced upgrades but increase testing and migration burden. Aggressive breaking changes simplify code sooner but can strand offline clients or durable work. Independent contract versions create more metadata but avoid falsely coupling every subsystem to one product number.

## H. Alternatives / comparisons — fit, not winner/loser

```text
product SemVer
    -> release communication / artifact identity

HTTP API version policy
    -> external request/response compatibility

sync protocol/schema capability/version
    -> Workstation/server compatibility

DB schema migration state
    -> persisted data evolution

message/snapshot version
    -> durable asynchronous/history compatibility
```
These can move at different rates while one product release bundles a tested combination.

## I. Real implementation considerations

Every adoption/change is required to state its owner/authority, failure behavior, recovery path, implementation evidence and operating burden. A source list is not implementation evidence.

### Implications for the Current Implementation

- **KEEP:** explicit product release/versioning and immutable artifact provenance.
- **KEEP:** separate API/sync/DB/local-schema/message/IPC/snapshot compatibility domains rather than equating all of them to product SemVer.
- **KEEP:** additive expand/overlap/backfill/switch/contract evolution and explicit retirement evidence.
- **IMPROVE NOW (implementation gate):** define actual supported version windows/capability negotiation as the first Workstation/API/schema code exists and test skipped-release upgrade paths.
- **NEEDS MEASUREMENT:** exact API version transport (URI/header/media type) and support-window length based on client/tooling/operating reality.
- **AVOID:** using `MAJOR` as a substitute for a migration plan or assuming `PATCH` can never require operationally risky data/config changes.

**What are we actually doing and why?** We are using product version numbers to identify immutable releases while treating API, sync, database, durable-message and snapshot compatibility as separate contracts because long-offline Workstations and durable state can outlive one release. We would simplify/version-couple domains only if implementation evidence proves their lifecycles cannot diverge.

**Implementation-evidence status:** the repository is still documentation/planning only at the root (no application source tree committed). These are accepted design requirements and future verification gates, not claims that the controls/behavior already exist in running code.

### Critical interrogation — answers intentionally withheld

**Foundation**

1. What does MAJOR.MINOR.PATCH communicate?
2. What is special about 0.x under SemVer?
3. What is the difference between pre-release and build metadata?

**Critical reasoning**

1. Which SquiFlow compatibility domains can coexist across versions?
2. Why can a PATCH product release still need a careful DB migration?
3. How does an old Workstation know whether it can safely sync?
4. What evidence is required before contracting an old schema/field?
5. Why should one product version not automatically version every internal contract?

**Trade-off**

1. How long should skipped Workstations remain supported?
2. When is forced upgrade safer than indefinite backward compatibility?
3. When should an API version be URI-based versus another transport?

**Failure / edge**

1. A six-month-old Workstation reconnects after two DB schema changes. What happens?
2. A durable job serialized under an older contract runs after deployment. How is it handled?
3. Binary rollback is attempted after a destructive data migration. Why can SemVer not save this?

**Implementation**

1. Where are current protocol/schema versions recorded?
2. What matrix tests old/new Workstation/server combinations?
3. How is version retirement measured before removal?
4. How do pre-release builds avoid contaminating production durable state?

**System design interview**

1. Design SquiFlow's version/compatibility matrix for product, API, sync, DB and durable work.
2. Plan a breaking API + schema change without losing offline Workstation intent.

**Challenge**

1. Release 1.4.2 contains a backward-compatible UI bug fix but also a required DB index rebuild and rule-snapshot migration. Explain why the product can still be a PATCH while deployment risk/compatibility must be handled separately.
