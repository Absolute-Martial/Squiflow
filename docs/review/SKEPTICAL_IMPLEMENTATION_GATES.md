# Skeptical Implementation Gates

**Version:** v0.0.15

For each capability ask:

- Is this an actual business action or merely a screen/state label?
- Can a small Owner understand the permission/configuration?
- What happens on first use, empty data, loading, dirty form, submit, partial completion, conflict, session expiry, permission change, dependency outage and retry?
- Who owns the data and what is authoritative locally/remotely?
- What prevents duplicate execution?
- What happens under concurrent edits/multiple tabs/devices?
- What async work is created, and what if it crashes, stalls or loses its lease?
- What if an external effect succeeded but acknowledgement was lost (`OutcomeUnknown`)?
- How does the user discover and continue workflow work?
- Is cancellation actually possible, or is compensation/reversal required?
- What gets audited/traced without leaking sensitive data?
- What are resource bounds?
- What happens during version skew/upgrade/long offline?
- How is the data backed up, restored, retained and deleted?

An implementation is not complete merely because its happy path works.
