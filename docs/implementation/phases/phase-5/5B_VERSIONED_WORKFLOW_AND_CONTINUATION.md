# Phase 5B — Versioned Workflow and Continuation

## Required first workflow

Implement one real configurable state/transition workflow where tenant variation is justified.

The workflow foundation is optional per capability. Simple fixed state machines may remain strongly typed when configurable workflow would only add ceremony.

## Workflow contract

A material non-terminal state needs:

- state identity;
- allowed transitions;
- current permission/domain checks;
- continuation owner/discovery path;
- timeout/expiry/retry semantics where applicable;
- recovery/correction path;
- definition version that created/controls the instance.

## Version changes

Active old instances remain explainable against their original definition unless an explicit migration maps them safely to a newer version.

## Authorization separation

OpenFGA may answer who has permission to attempt a transition. SquiFlow workflow/domain state decides whether the transition is currently valid.

## Small-team operability

Avoid approval designs that strand Owner + one Staff when a required approver becomes unavailable. Any four-eyes/high-risk rule must have a defined recovery/escalation model appropriate to its risk.

## Exit gate

Workflow instances survive definition changes and process restarts without becoming uninterpretable or silently invalid.