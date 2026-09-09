# URL 035 — Schema Evolution: Changing the Contract Without Breaking What Runs

## Review method

This occurrence is reviewed independently from earlier archive/URL material. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: no comparison, pattern catalog, popularity claim, or source diagram selects architecture by itself.

## A. Identification

- **URL occurrence:** `035`
- **PDF page:** `279`
- **Source URL:** `https://blog.bytebytego.com/p/schema-evolution-changing-the-contract`
- **Source access:** paid article with public preview; no paywall bypass.
- **Related supplied visual:** archive page `293`, common versioning strategies.
- **Visual inspected:** PDF page `279` at full size.

## B. Core concept

### SOURCE

The preview explains that small schema changes can break production when old and new application versions coexist. Historical rows, queued messages, and old mobile clients can outlive the code that created them. The outline covers backward/forward compatibility, safe versus breaking changes, expand-and-contract migrations, schema registries, differences across databases/APIs/event streams, versioning, and deprecation timelines.

### INFERENCE

Compatibility has a larger time horizon than a deployment. SquiFlow's skipped Workstations, pending local sync, durable jobs, stored idempotency results and snapshots make this especially important even on a single active server node.

### EXTERNAL KNOWLEDGE / CAVEAT

“Additive” is not automatically safe: a new required field, new enum value, changed default, changed meaning, stricter validation or newly produced event variant can break old readers/writers. Version labels also do not solve compatibility by themselves; old/new behavior must be defined and tested.

## C. Important concepts

- old/new reader and writer overlap;
- backward and forward compatibility;
- expand → deploy overlap → backfill → switch → contract;
- durable messages/events surviving code versions;
- old Workstations/mobile-like clients;
- stored snapshots/rule/config versions;
- schema registries where a real event ecosystem warrants one;
- deprecation telemetry and retirement;
- rollback versus roll-forward;
- destructive migration recovery.

## D. Diagram / visual explanation

The related visual compares semantic, calendar, sequential and API versioning labels. This is a naming/communication map, not a compatibility proof. A database schema can be incompatible even if the product version increments “correctly,” and two API versions can still share incompatible durable state if migration design is poor.

## E. How it works — step by step

1. inventory all readers/writers of the contract, including old clients and queued work;
2. define compatibility direction and support window;
3. expand schema/contract additively where practical;
4. deploy code that can operate during overlap;
5. backfill/migrate with bounded observable work;
6. switch authoritative reads/writes;
7. verify obsolete readers/writers/messages are drained, migrated, rejected explicitly, or outside support;
8. contract/remove old shape only after evidence;
9. document rollback/roll-forward if contraction fails.

## F. Why it matters

A Workstation may return after months offline carrying pending intent produced under an older local/API/sync schema. SquiFlow cannot make destructive server changes based only on “the current deployment is upgraded.”

## G. Trade-offs / limitations

Compatibility overlap creates temporary duplicated fields/code, migration logic and longer deprecation periods. Immediate destructive migration is simpler locally but risky in distributed/version-skew reality. Schema registries can help event ecosystems but are needless operational surface if SquiFlow has no large independent event-stream contract set.

## H. Alternatives / comparisons — fit, not winner/loser

```text
additive compatible evolution
    -> preferred for normal DB/API/message changes

new API/protocol version
    -> breaking external/client semantics requiring coexistence

explicit migration/upgrade-required
    -> old client cannot safely continue

schema registry
    -> candidate when independent producers/consumers need machine-enforced schema governance

maintenance migration
    -> acceptable for genuinely incompatible changes when overlap cannot be supported and downtime/recovery is explicit
```

## I. Real implementation considerations

Compatibility must cover central DB schema, Workstation local schema, API/sync protocol, Guard IPC, durable jobs/messages, OpenFGA model IDs/tuple migration, rule/workflow/config snapshots and stable observability registries where external consumers depend on them.

### Implications for the Current Implementation

- **KEEP:** expand-migrate-switch-contract direction already documented for central persistence.
- **KEEP:** old Workstations/pending sync/durable work are part of the compatibility matrix, not afterthoughts.
- **KEEP:** destructive contraction requires reader/writer/data inventory and explicit roll-forward/maintenance recovery.
- **IMPROVE NOW (implementation gate):** when schemas/protocols exist, execute old-reader/new-writer and new-reader/old-writer tests against non-empty historical data and pending local work.
- **LATER / SCALE TRIGGER:** schema registry only if SquiFlow develops independently versioned event producers/consumers where registry governance materially reduces risk.
- **AVOID:** assuming semantic/product version numbers prove compatibility.
- **AVOID:** removing fields/columns because current server code no longer references them without proving old clients/jobs are drained.

**What are we actually doing and why?** We design schema evolution around overlapping versions because Workstations and durable state can outlive a server release. We do not add a registry/platform until there is a real producer/consumer ecosystem that needs it.

**What would falsify/change this?** If an internal contract is provably single-process/single-version with no persisted old data or durable messages, a simpler migration may be safe. If independent producers/consumers grow, stronger registry/compatibility tooling becomes justified.

### Critical interrogation — answers intentionally withheld

**Foundation**
1. What are backward and forward compatibility?
2. Why does schema overlap outlive the deployment window?
3. What is expand-and-contract migration?

**Critical reasoning**
1. Which SquiFlow contracts can survive for months outside the current server binary?
2. Why can adding a required field be breaking?
3. When should an old Workstation receive `UpgradeRequired` instead of compatibility support?
4. What evidence is required before removing an obsolete column/message field?
5. Why is backup not a substitute for compatible migration?

**Trade-off**
1. When is temporary dual-read/dual-write complexity justified?
2. When would a schema registry actually pay for itself?
3. When is a maintenance migration safer than rolling overlap?

**Failure / edge**
1. Old Workstation syncs a message missing a newly required field.
2. New writer emits an enum value old consumer does not understand.
3. Backfill is interrupted halfway.
4. Server binary rolls back after destructive schema contraction.

**Implementation**
1. Which compatibility matrix belongs in CI versus release qualification?
2. How is migration progress resumable/observable?
3. How are old durable jobs/messages inventoried before contraction?
4. How are rule/workflow snapshots versioned independently of product release?

**System design interview**
1. Design a no-data-loss column rename across old/new API instances and offline Workstations.
2. Explain why API versioning and DB schema evolution are related but separate contracts.

**Challenge**
A field must change meaning, not just name, while old Workstations may remain offline for six months. Design the overlap, migration, rejection/upgrade rule and eventual contraction evidence.
