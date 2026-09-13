# Feature Release Channels, Rollout, and Experiments

**Status:** Accepted architecture direction

## 1. Separate concepts

SquiFlow treats these as different systems:

```text
Feature availability = does this deployment/tenant expose the capability?
Release channel      = Stable / Beta / Preview / Internal lifecycle state
Permission           = may this actor attempt it?
Experiment           = which approved presentation/behavior variant is assigned?
Domain rule          = is the operation valid for the current business state?
```

A feature flag is never authorization. Hiding a UI element is never authorization.

## 2. Release channels

Supported lifecycle vocabulary:

```text
Internal
→ Preview
→ Beta
→ Stable
→ Deprecated
→ Removed
```

A tenant may opt into a channel only within the platform/deployment ceiling. A deployment may forbid Preview/Beta entirely. Compatibility and minimum client version remain explicit constraints.

## 3. Targeting and rollout

A feature publication may target a bounded combination of:

- deployment/environment;
- tenant;
- organization/branch/program where the feature definition explicitly permits it;
- host (`Workstation`, `TenantWeb`, `WebApi`, `SyncApi`, etc.);
- role/permission eligibility for UX exposure only;
- user subject;
- device/workstation;
- minimum/maximum compatible application version;
- release channel;
- rollout percentage.

Targeting does not grant permission. The authoritative command still checks current authorization and domain validity.

## 4. Experiments / A-B testing

Experiments are for approved product/presentation alternatives, not for weakening correctness or security.

Good experiment candidates include:

- navigation/layout;
- onboarding flow;
- search/result presentation;
- wizard versus single-page data entry;
- dashboard presentation;
- non-critical workflow ergonomics.

Do not A/B test:

- tenant isolation;
- authentication/authorization correctness;
- financial/accounting invariants;
- inventory integrity;
- data durability/concurrency guarantees;
- security ceilings;
- business calculations where variant differences would change authoritative meaning without an explicitly approved product rule.

## 5. Stable assignment

Experiment assignment must be stable for its declared subject type.

Conceptually:

```text
hash(ExperimentId + SubjectId) → stable bucket → Variant
```

Use the correct subject intentionally:

- user-based experiment: same authenticated user should ordinarily see the same variant across Web/Workstation;
- device experiment: use DeviceId only when device behavior is what is being tested;
- tenant experiment: use TenantId when the whole tenant must remain coherent.

Assignments and exposure events must be versioned enough for analysis and support.

## 6. Workstation offline snapshot

The Workstation does not call the server for every feature check.

It receives a versioned effective feature snapshot containing only information safe and relevant for local use.

```text
EffectiveFeatureSnapshot
├── Revision
├── Feature states
├── Release channel/effective rollout state
├── compatible experiment assignment where allowed
└── expiry/offline policy where needed
```

When connected, a newer validated publication replaces the prior snapshot atomically. While offline, behavior follows the feature's declared offline policy.

Recommended offline policy classes:

```text
SnapshotAllowed
StableOnly
ServerRequired
```

Security/admin operations such as administrator assignment, permission changes, tenant-security policy changes and device revocation remain `ServerRequired` regardless of a cached UI feature state.

## 7. Kill switch and rollback

A feature/experiment publication must support a rapid disable/rollback path that does not delete authoritative data or reinterpret already-committed business facts.

Disabling an experiment ends new exposure but must preserve enough assignment/version evidence to interpret prior telemetry.

## 8. Observability and privacy

Feature/experiment exposure telemetry must be bounded and privacy-aware. Do not emit high-cardinality identifiers as metric labels. Use logs/traces/events or purpose-built bounded analytics when subject-level evidence is required.

The experiment system must not become a reason to log unrestricted customer/business payloads.
