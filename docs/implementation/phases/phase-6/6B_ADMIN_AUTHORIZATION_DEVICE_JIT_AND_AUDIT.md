# Phase 6B — Admin Authorization, Device/JIT, and Audit

## Required control stack

Protected Admin operations evaluate as applicable:

```text
private ingress evidence
+ registered non-revoked Admin device
+ ZITADEL admin identity/recent auth
+ operation-specific platform permission
+ JIT/time-bounded elevation
+ physical/recovery factor
+ independent approval for highest-risk destructive action
```

Not every low-risk read needs every factor; the policy is risk-based and follows the operation being introduced.

## Capability separation

Do not implement one universal `SuperAdmin = everything`. Separate platform-access, security view, encryption policy change, key rotate/revoke/destroy, device recovery, backup restore, identity config and support access according to real operations.

Encryption/key administration does not automatically grant cross-tenant customer-data browsing.

## Authoritative audit

Material operations write durable audit evidence containing safe actor/action/target/device/reason/approval/version/correlation/timestamp/outcome metadata without raw secrets/keys/tokens.

## High-risk flow

For destructive operations support pending independent approval/four-eyes where policy requires it. Revocation and irreversible key destruction remain distinct.

## Exit gate

A copied session on an unregistered/revoked device and a private-network-only user cannot perform protected Admin actions; high-risk operations are attributable and reviewable.