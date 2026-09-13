# Encryption Policy, Key Lifecycle, and Privileged Access

**Status:** Accepted architecture direction  
**Version:** v0.0.19  
**Parent security owner:** `docs/security/ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md`

This document makes explicit the policy/lifecycle details accepted in the 2026-09-13 encryption and Platform Admin discussion. It complements the broader key-management/zero-trust document; it does not create a second cryptographic authority.

## 1. Platform-managed encryption policy

Platform Admin governs a versioned `EncryptionPolicy` through Admin API. The UI controls policy and lifecycle requests; it does not receive raw KEKs, DEKs, OpenBao root/seal material, or tenant plaintext merely because the operator administers encryption.

The policy model is conceptually:

```text
EncryptionPolicy
│
├── WorkstationDatabase
│   ├── Required
│   ├── EncryptionProfile / algorithm profile
│   ├── KeyRotationPolicy
│   ├── RecoveryPolicy
│   └── MinimumCompatibleClientVersion
│
├── ServerStorage
│   ├── VolumeEncryptionRequired
│   └── ActiveKey/ProfileVersion where applicable
│
├── SensitiveFields
│   ├── DataClassifications
│   └── EncryptionProfile per protected class
│
├── ObjectStorage
│   ├── ProviderAtRestEncryptionRequired
│   └── ApplicationEncryptionProfile where justified
│
├── Backup
│   ├── EncryptionRequired
│   ├── RecoveryKeyPolicy
│   └── RotationPolicy
│
└── Diagnostics
    ├── RedactionPolicy
    └── SensitiveDataProhibition
```

Exact field names are implementation details. The architectural requirements are the policy dimensions and separation of concerns.

## 2. Policy publication is a security-sensitive configuration change

An encryption policy is not an ordinary UI preference. Published policy is:

- versioned;
- authoritatively audited;
- integrity protected/signed where the selected distribution mechanism supports it;
- rollback/roll-forward aware;
- compatible with supported older Workstations during the supported upgrade window;
- associated with the minimum client/schema/provider version needed to apply it safely;
- validated before publication so a policy cannot intentionally or accidentally make still-supported data undecryptable.

A client or service records the policy/key/profile version relevant to durable encrypted state so recovery can identify how the state must be opened without recording secret key bytes.

## 3. Platform key records expose metadata, not raw key material

Platform Admin may display and authorize actions against safe key metadata such as:

```text
KeyId
Purpose
Scope / owner reference
KeyVersion
Status
CreatedAt
ActivatedAt
RotationDueAt
RetiredAt
ProviderReference
```

The ordinary Admin API/UI does not expose a `ShowMasterKey` or generic raw-key export operation.

## 4. Staged key rotation

Rotation must preserve the ability to decrypt existing ciphertext while new writes transition to a new active key/version.

Conceptual flow:

```text
request rotation
      │
      ▼
authorize + step-up + approval when required
      │
      ▼
create K2 / next key version
      │
      ▼
make K2 active for new encryption
      │
      ▼
keep K1 decrypt-capable
      │
      ▼
rewrap / re-encrypt old material progressively where required
      │
      ▼
verify coverage/recovery
      │
      ▼
retire K1
```

Do not switch keys in a way that instantly makes old data unreadable.

Useful lifecycle states are conceptually:

```text
Pending
Active
DecryptOnly
Retiring
Revoked
Destroyed
```

Exact provider state names may differ. SquiFlow must distinguish **revocation** from **destruction**. Revocation can deny new use while preserving controlled recovery/decrypt capability according to policy; destruction is intentionally irreversible and therefore receives the strongest authorization/approval/recovery checks.

## 5. Server-key lifecycle is centrally governed

For server-side KEKs/DEKs and protected-field/object/backup keys, Platform Admin can request narrowly defined lifecycle operations such as:

```text
generate / provision
activate
rotate
rewrap
revoke/disable
schedule retirement
verify migration/coverage
retire
destroy only under explicit destructive policy
```

The actual cryptographic operation remains behind OpenBao/Vault or another selected key-management provider. SquiFlow records the business/security meaning, authorization and audit around the provider operation.

## 6. Four-eyes approval for the highest-risk destructive operations

The architecture supports independent approval for actions whose mistake or compromise could produce platform-wide security impact or permanent loss.

Example flow:

```text
Administrator A
      │
      ▼
requests destructive/high-risk operation
      │
      ▼
PendingApproval
      │
      ▼
Administrator B with required independent authority
      │
      ▼
approves
      │
      ▼
step-up / physical factor requirements revalidated
      │
      ▼
execute
      │
      ▼
verify + authoritative audit
```

Candidate operations include:

- destroying root/KEK/key material;
- restoring a full production backup when it can overwrite current authoritative state;
- disabling a mandatory encryption control;
- changing root identity/key-management provider configuration;
- granting the highest platform-security privileges;
- changing recovery policy in a way that reduces recoverability or required factors.

This does not require two-person approval for ordinary read-only administration or low-risk settings. The rule is risk based.

## 7. Break-glass lifecycle

Break-glass is a separate emergency recovery path, not a standing all-powerful admin account.

Conceptual lifecycle:

```text
normal Admin/control plane unavailable
        │
        ▼
invoke documented break-glass path
        │
        ▼
strong identity + registered/recovery device evidence
        │
        ▼
required physical/recovery factor/quorum
        │
        ▼
very narrow emergency authority
        │
        ▼
high-severity authoritative audit/evidence
        │
        ▼
operator/security notification where available
        │
        ▼
restore normal control plane
        │
        ▼
review + rotate/revoke exposed emergency credentials/factors as appropriate
```

There is no plaintext `admin/admin123`, hidden tenant route, or permanent emergency bypass in application configuration.

## 8. Platform support access is separate from encryption administration

Encryption/key administration does not grant cross-tenant customer-data browsing.

If cross-tenant support access is implemented, it uses an explicit support workflow such as:

```text
support/admin requests tenant-support session
        │
        ▼
reason / ticket / incident reference
        │
        ▼
operation-specific platform authorization
        │
        ▼
additional approval or tenant acknowledgement where policy requires
        │
        ▼
time-bounded + tenant/resource-scoped support authority
        │
        ▼
automatic expiry/revocation
        │
        ▼
authoritative audit
```

Support access and encryption lifecycle permissions are different capabilities.

## 9. Registered Admin-device revocation

An Admin device has its own lifecycle independent of the administrator account and private network membership.

Revocation should be able to invalidate future protected Admin use by that device, including as applicable:

- Admin-device credential/session use;
- Platform Admin access requiring that device proof;
- future recovery/key-management ceremonies requiring the device;
- future issuance/rotation participation.

Revocation does not claim to erase secrets/plaintext already available on a fully compromised running machine.

## 10. No universal `SuperAdmin = everything` assumption

Platform Admin entry and individual capabilities remain separate checks. A highly privileged emergency identity may exist for narrowly defined recovery, but ordinary administration should be capability-scoped.

Illustrative separation:

```text
AdminPlatform.Access
Platform.Security.View
Platform.Encryption.Policy.View
Platform.Encryption.Policy.Change
Platform.Keys.Rotate
Platform.Keys.Revoke
Platform.Keys.Destroy
Platform.Device.Recovery
Platform.Backup.Restore
Platform.Tenants.Suspend
Platform.Identity.Configure
Platform.Support.AccessTenant
```

Exact names remain capability-catalog details. A grant to one does not imply the others.

## 11. Verification requirements

Before production, verify at minimum:

- policy publication creates a new immutable/versioned authoritative record;
- incompatible policy cannot silently strand supported Workstations/data;
- rotation keeps old ciphertext decryptable during the migration window;
- rewrap/re-encryption can resume safely after interruption;
- retiring/revoking a key does not accidentally equal destruction;
- destructive key operation cannot execute without required independent approval/factors;
- ordinary encryption administrator cannot browse tenant business data without separate support permission;
- support sessions expire and remain tenant/resource scoped;
- break-glass use produces high-severity evidence and triggers post-use credential/factor review;
- Admin-device revocation prevents future protected Admin use from that device;
- no key byte appears in Admin UI responses, logs, traces, audit payloads or policy documents.

## 12. Architectural invariant

> Platform Admin governs encryption policy and key lifecycle, but never treats raw key possession or broad tenant-data access as an ordinary administrative capability. Policy is versioned and recovery-aware; rotation is staged; destruction is distinct from revocation; and the highest-risk actions support independent approval, strong physical/recovery evidence and authoritative audit.
