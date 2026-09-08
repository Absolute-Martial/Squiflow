# Workflow Design and Continuation

**Version:** v0.0.15

Design workflows from the user's continuation journey, not from a diagram alone.

## 1. Continuation-first questions

For every non-terminal state answer:

1. Who can continue it?
2. Where do they discover it?
3. What action continues it?
4. What data is required?
5. What if nobody acts?
6. Is there a deadline/escalation?
7. Can it be reassigned/delegated?
8. Can it be cancelled, or must a compensating action be created?
9. What if two actors act simultaneously?
10. What if permission, underlying entity, rule version or workflow version changes?
11. How can the user/support understand why the item is here?

A state without a meaningful continuation owner/reason may be a field/status rather than a workflow state.

## 2. Small-team usability

A tenant with only Owner + Staff must not need a complicated BPM suite.

Start with simple stage/transition concepts and allow the Owner to configure only what they need.

Example:

```text
Order accepted
→ WaitingForArtwork
→ DesignInProgress
→ WaitingForCustomerApproval
→ ReadyForPrint
→ Completed
```

The Owner may keep the default workflow or add/remove tenant stages through Web Settings.

## 3. Canonical state versus tenant workflow stage

Use configurable tenant workflow stages alongside protected canonical business states.

Do not let a custom stage redefine:
- payment success/refund/reversal;
- posted/issued financial truth;
- stock movement truth;
- tenant/security/session state.

## 4. Web-only authoring/publication

Workflow definitions, transitions, stage creation, role/permission mapping and publication are changed through Web administration only.

The Workstation:
- receives a published compatible effective workflow snapshot;
- can execute allowed local transitions while offline if the transition is explicitly local-capable;
- cannot create or publish workflow definitions.

## 5. Permission integration

A transition combines:

```text
permission
+ current canonical state
+ current workflow stage/version
+ transition guard/rule
+ concurrency version
```

Example:

```text
permission: quotes.approve
stage/state: Submitted
transition: Submitted → Approved
```

## 6. Human work discovery

Human work should be discoverable through an appropriate work inbox rather than requiring users to rediscover the original record.

Examples:
- Needs my approval
- Needs information
- Assigned to me
- Due soon
- Overdue

For a two-person tenant this can be a compact list inside Web/Workstation rather than a separate enterprise task application.

## 7. Waiting and asynchronous continuation

A workflow may wait for:
- human action;
- deadline/time;
- external callback;
- Worker completion.

Waiting is durable state. It is not represented only by an in-memory timer.

## 8. Cancellation/correction

Each state must classify whether cancellation is:
- immediately safe;
- safe only before an irreversible effect;
- compensation/reversal required;
- not allowed.

Do not show `Cancel` when the real business action is a refund, reversal or corrective document.

## 9. Versioning

Active workflow instances remain pinned to their definition version unless an explicit migration exists.

Do not silently apply a new transition graph to old in-progress work.

## 10. Concurrent actors

Two actors may act simultaneously. A transition must use expected state/version and be idempotent so only a valid transition commits.

If one user approves while another cancels/rejects, the loser receives a stale/conflict result and reloads the current workflow state.
