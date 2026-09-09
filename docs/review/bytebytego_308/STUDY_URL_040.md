# URL 040 — A Crash Course in API Versioning Strategies

## Review method

This occurrence is reviewed independently from earlier archive/URL material. `SOURCE`, `INFERENCE`, and `EXTERNAL KNOWLEDGE / CAVEAT` are separated. The SquiFlow analysis follows `TECHNOLOGY_FIT_AND_USAGE_REVIEW_RULE.md` and `CRITICAL_INTERROGATION_RULE.md`: no comparison, pattern catalog, popularity claim, or source diagram selects architecture by itself.

## A. Identification

- **URL occurrence:** `040`
- **PDF page:** `284`
- **Source URL:** `https://blog.bytebytego.com/p/a-crash-course-in-api-versioning`
- **Source access:** paid article with public preview; no paywall bypass.
- **Related supplied visual:** archive page `293`, common versioning strategies.
- **Visual inspected:** PDF page `284` at full size.

## B. Core concept

### SOURCE

The visible article says versioning lets an API evolve without forcing all clients to update immediately. Breaking field/contract/behavior changes can disrupt consumers; a new version gives clients a controlled migration path. The article is organized around when versioning is necessary, versioning strategies/labels, and graceful retirement of old versions.

### INFERENCE

Versioning is a client-compatibility contract and retirement process, not merely putting `v2` in a URL. Choosing URI, query parameter, header or media-type versioning should follow SquiFlow client/tooling/observability/cache needs rather than a generic comparison.

### EXTERNAL KNOWLEDGE / CAVEAT

Not every additive change requires a new API version, and a new version does not make a breaking persistence/message change safe. Behavioral compatibility, enum evolution, optionality/defaults and old-client semantics matter. Product semantic version, API version, sync protocol version and DB schema version are separate domains.

## C. Important concepts

- breaking versus compatible API changes;
- old/new clients coexisting;
- version identifier placement;
- version discovery/documentation;
- deprecation and sunset;
- usage telemetry before retirement;
- skipped Workstation releases;
- API/sync/schema/message version separation;
- migration window and `UpgradeRequired`.

## D. Diagram / visual explanation

The visual shows semantic, calendar, sequential and API versioning, with API examples using request parameters, URI components and HTTP headers. These are labeling/transport techniques. The visual does not establish which is best for SquiFlow, and semantic versioning of the product does not replace an explicit long-lived API/sync compatibility policy.

## E. How it works — step by step

1. identify whether the change actually breaks a supported client expectation;
2. prefer additive-compatible evolution where practical;
3. if breaking, define new contract/version and old-client behavior;
4. select version transport/label from the real client/tooling/cache/observability constraints;
5. support overlap for a documented window;
6. collect inventory/telemetry of remaining old clients;
7. warn/deprecate predictably;
8. retire only after support-window/evidence or return explicit `UpgradeRequired` rather than silently changing meaning.

## F. Why it matters

Workstations can skip releases and remain offline. Web/Admin and future partners may deploy independently. SquiFlow must not silently reinterpret an old command as a new business meaning.

## G. Trade-offs / limitations

URI versioning is explicit/tool-friendly but duplicates route surface. Header/media-type approaches keep resource URLs stable but can be less visible and complicate caches/debugging/tooling. Query parameters are easy but can be awkward for routing/cache semantics. Supporting multiple versions increases tests/security/maintenance. Frequent versions can hide poor compatibility discipline.

## H. Alternatives / comparisons — fit, not winner/loser

```text
additive-compatible same version
    -> preferred when client expectations remain valid

URI version
    -> candidate when explicit routing/tooling/discoverability is most valuable

header/media-type version
    -> candidate when representation/contract negotiation fits clients/tooling

query-parameter version
    -> possible when client/routing constraints make it practical

protocol capability negotiation / UpgradeRequired
    -> useful for Workstation/sync compatibility beyond simple REST route labels
```

Exact selection remains implementation evidence-driven.

## I. Real implementation considerations

Version inventory must include owning backend/audience, compatibility matrix, retirement date/window, old-client telemetry, docs/OpenAPI, security policy and durable-state compatibility. Do not let an old version become a security bypass or indefinite unmaintained surface.

### Implications for the Current Implementation

- **KEEP:** every public/long-lived SquiFlow API/sync surface has an explicit compatibility/retirement contract.
- **KEEP:** additive-compatible change preferred; breaking semantics use explicit version/compatibility path.
- **KEEP:** Workstation sync checks compatibility before durable changes and can return `UpgradeRequired`.
- **NEEDS MEASUREMENT:** exact HTTP version transport (URI/header/media type/narrow combination) remains open until real clients/tooling/edge/cache needs are implemented.
- **IMPROVE NOW (implementation gate):** generated endpoint/version inventory plus old-client usage telemetry before retirement.
- **AVOID:** choosing a versioning style because the comparison visual makes it look standard.
- **AVOID:** using product SemVer as a substitute for API/sync/schema/message compatibility.
- **AVOID:** silent semantic reinterpretation of an old request.

**What are we actually doing and why?** We keep compatibility rules explicit because SquiFlow clients can be independently old, especially Workstation. We have intentionally not pre-selected URI versus header/media-type versioning because the real client/tooling constraints are not yet implemented.

**What would falsify/change this?** Once concrete endpoints/clients exist, if one version transport materially simplifies OpenAPI generation, routing, caching, diagnostics and old-client support without harmful tradeoffs, it can be selected for that surface.

### Critical interrogation — answers intentionally withheld

**Foundation**
1. When does an API change need a new version?
2. Why is retirement part of versioning design?
3. How is API version different from product SemVer?

**Critical reasoning**
1. Which SquiFlow clients can legitimately remain old and for how long?
2. Why should version transport remain open until clients/tooling exist?
3. Which additive changes can still break clients?
4. How does versioning interact with authorization/security fixes?
5. When should the server require an upgrade rather than support another version?

**Trade-off**
1. URI versus header/media type for SquiFlow Web/Workstation/partners?
2. How many simultaneous versions can a small team safely maintain?
3. When is one backward-compatible version better than introducing `v2`?

**Failure / edge**
1. Old client sends a command whose field meaning changed.
2. Deprecated version has one remaining offline Workstation.
3. A security fix cannot safely preserve old behavior.
4. Edge cache ignores the version dimension.

**Implementation**
1. How is API version included in generated endpoint inventory?
2. How is old-client usage measured without leaking sensitive identifiers?
3. What retirement tests prove no unsupported writer remains?
4. How is `UpgradeRequired` represented consistently across HTTP/sync?

**System design interview**
1. Design version migration for a quotation API used by Web, Workstation and future partner integrations.
2. Explain why API versioning does not solve DB/message schema evolution alone.

**Challenge**
A breaking command semantic change is required for a security reason, but 4% of Workstations have not connected for three months. Decide whether to preserve, version, reject, or force upgrade and define the evidence/risk tradeoff.
