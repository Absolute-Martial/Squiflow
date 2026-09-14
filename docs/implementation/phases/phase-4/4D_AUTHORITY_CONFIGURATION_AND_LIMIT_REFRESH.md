# Phase 4D — Authority, Configuration, and Limit Refresh

## Current-authority refresh

Long-offline recovery must refresh state that local snapshots cannot make authoritative, including as applicable:

- authentication/session status;
- device enrollment/revocation;
- OpenFGA permissions/relationships;
- feature/settings publication revisions;
- rule/workflow/form compatibility;
- current server-required stock/credit/payment/hard-limit facts;
- resource/consumption limit state when the capability exposes it.

Only dimensions that exist in the current product/capability need to be refreshed; do not invent unused subsystems just to satisfy the list.

## Workstation behavior

Old cached permissions/limits may support local UX or explain why an operation was captured, but they cannot override current server authority during admission.

## Changed authority

A pending operation captured while allowed may be rejected later if current authority has changed. Preserve the local intent/evidence and provide a human-recoverable state rather than claiming the old permission remains valid forever.

## Degraded UX

When current authority cannot be obtained, classify the action as locally provisional, unavailable, defer/retry, or fail-closed according to the capability. Do not use one universal rule.

## Exit gate

Long-offline recovery refreshes all materially authoritative snapshots before they can influence protected server decisions.