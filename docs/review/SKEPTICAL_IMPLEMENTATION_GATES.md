# Skeptical Implementation Gates

**Version:** v0.0.15

For each capability ask the questions below before adding another project, service, queue, cache, offline layer or configurable state.

## Product/UX

- Is this an actual business action or merely a screen/state label?
- Can a small Owner + Staff business understand it without ERP expertise?
- What happens on first use, empty data, loading, dirty form, submit, partial completion, conflict, session expiry, permission change, dependency outage and retry?
- Can the user tell `LocalCommitted` from `Authoritative`?
- If something waits for another person, how do they discover it and continue?
- Is cancellation actually possible, or is compensation/reversal required?

## Permissions/control plane

- Why is this permission needed?
- Is it a stable business action or a UI implementation detail?
- Can the tenant Owner safely delegate it?
- Could granting it expose money, stock, cost/margin, security or another tenant?
- Is the assignment performed only through Web administration?
- Is the Desktop merely consuming the effective permission rather than changing it?
- If this changes server/runtime/provider behavior, why is it not restricted to Platform Admin Web?
- What happens to queued/offline work after permission revocation?

## Local-first Workstation

- Does the user truly need this command offline?
- Is it local-capable, local-provisional or server-required?
- What is safely committed locally before network use?
- What does server authority still validate?
- What happens if the device is offline for months?
- What happens if local data is tampered with?
- Do we really need CRDT/multi-master semantics, or does aggregate-specific conflict policy solve the problem more safely?
- Can the user understand history/conflict/pending state after remote changes arrive?

## Web

- Does this feature genuinely need offline behavior now?
- If not, are we accidentally adding IndexedDB/service-worker/multi-tab complexity with no user value?
- Can ordinary HTTP/CDN caching make it fast enough without an offline business replica?
- What sensitive data would browser JavaScript be able to read?

## Data/transactions

- Who owns the data and what is authoritative locally/remotely?
- What is the transaction boundary?
- What prevents duplicate execution?
- What happens under concurrent edits/multiple devices?
- Is a generic last-write-wins policy unsafe for this aggregate?
- Does the read path need authoritative, fresh, eventually-consistent or cached data?

## Worker/external effects

- What async work is created?
- What if it crashes, stalls, loses its lease or resumes after another worker took over?
- What if an external effect succeeded but acknowledgement was lost (`OutcomeUnknown`)?
- Can the job be paused/drained safely?
- Can an operator retry it without creating duplicate business effects?
- Is the critical control available only from Platform Admin Web?

## Architecture/complexity

- Why does this need a separate project/process/service?
- Would an in-process module with a clean contract be simpler?
- Are we adding infrastructure merely because it may be useful someday?
- Does this abstraction have more than one real implementation/use case?
- Is a provider-specific reference project being mistaken for a product decision?
- Can we postpone this until measurements/user demand justify it?

## Operations/security

- What gets audited/traced without leaking sensitive data?
- What are resource bounds?
- What happens during version skew/upgrade/long offline?
- How is the data backed up, restored, retained and deleted?
- Can a support/admin action be explained and rolled back/corrected?

An implementation is not complete merely because its happy path works, and an architecture is not better merely because it has more components.
