# OpenBao and Zero-Trust Platform Admin Operating Profile

**Status:** Accepted initial operating direction  
**Version:** v0.0.19  
**Applies to:** current self-hosted Podman/rack profile

This document records the concrete operating details accepted in the 2026-09-13 encryption, key-management, Platform Admin, Podman, and private-network discussion. It complements `ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md`, `ENCRYPTION_POLICY_KEY_LIFECYCLE_AND_PRIVILEGED_ACCESS.md`, and `PRIVATE_ADMIN_NETWORK_AND_PODMAN.md`.

Where an earlier example used a different Shamir threshold, this document defines the **initial rack operating profile**: `3-of-5` shares. A later threshold change requires an explicit superseding decision and recovery drill.

## 1. Initial OpenBao profile

The initial self-hosted key-management profile is:

```text
OpenBao
+ Integrated Storage / Raft
+ Transit engine
+ Shamir 3-of-5 unseal/recovery
+ PGP-encrypted generated shares
+ separate secure holders/locations
+ regular tested Raft snapshots
+ periodic unseal/recovery drills
```

OpenBao is the initial candidate because SquiFlow must not implement its own cryptographic vault/root-key lifecycle. HashiCorp Vault remains a provider alternative behind a narrow SquiFlow key-management adapter.

The initial profile does **not** mean OpenBao is permanently tied to manual Shamir operation. When a trustworthy external KMS/HSM exists, hardware-backed or external auto-unseal can supersede the manual bootstrap profile after an explicit migration/recovery proof.

## 2. Root of trust and seal strategy

The root/seal problem is separate from SquiFlow application encryption.

```text
operator/recovery factors
        │
        ▼
OpenBao seal/unseal
        │
        ▼
OpenBao key hierarchy / Transit
        │
        ▼
SquiFlow KEKs / wrapped DEKs
        │
        ├── Workstation DB keys
        ├── selected server protected-data keys
        ├── backup keys
        └── object-encryption keys where justified
```

Static Unseal with a plaintext key file stored beside the OpenBao container/Raft storage is not the production target. It may only be a deliberately documented temporary bootstrap/development mechanism.

Preferred future seal directions are:

- PKCS#11/HSM-backed seal;
- trustworthy external KMS/HSM auto-unseal;
- or the accepted manual Shamir profile until an external root is available.

## 3. Shamir recovery custody

For the initial rack profile:

```text
Share count = 5
Threshold   = 3
```

The generated shares are PGP-encrypted to their intended recovery holders/keys when supported by the initialization procedure. The shares must not all live in the same rack, bag, ordinary USB drive, home/office location, password vault, or machine.

A conceptual distribution is:

```text
Share A -> primary secure recovery location
Share B -> separate secure location
Share C -> separate secure location
Share D -> additional disaster-recovery location/holder
Share E -> additional disaster-recovery location/holder
```

The exact people/locations are operational secrets and do not belong in source control.

Possession of fewer than three shares must not be enough to recover/unseal. Loss of one ordinary physical security token must not permanently destroy customer data.

## 4. Raft durability and recovery

OpenBao's key-management availability/recoverability is not proven merely because a container restarts.

The initial profile requires:

- durable Integrated Storage/Raft data outside the disposable container layer;
- regular supported Raft snapshots;
- snapshot integrity/availability checks;
- encrypted/protected off-host backup according to the deployment policy;
- restore onto a replacement environment as a tested procedure;
- periodic drill of `snapshot + required recovery shares/factors -> usable OpenBao`;
- periodic manual unseal/recovery drill while Shamir remains the active seal model.

Container recreation must not initialize a new empty vault over the old deployment by accident.

## 5. Physical security factor

The operator-controlled physical factor is a **second anchor** for protected cryptographic administration. It is not one AES key reused directly to decrypt every database.

Protected operation condition:

```text
approved private-network path
        AND
ZITADEL-authenticated platform administrator
        AND
registered active Admin device
        AND
operation-specific platform authorization
        AND
required physical security factor / recovery quorum
        AND
step-up/JIT/approval requirements for the operation
```

An ordinary removable drive containing a raw plaintext `master.key` is not the production root-of-trust design. Prefer a non-exportable hardware-backed token/HSM when practical. Transitional removable recovery material must itself be encrypted/protected and normally offline.

The physical factor is used for bootstrap, recovery, or specifically protected high-risk administration and can then be removed. Ordinary tenant business transactions must not require the operator to leave a key attached.

## 6. Registered Admin device requirement

Private-network reachability is not sufficient. A protected Platform Admin session is limited to registered, non-revoked Admin devices.

Conceptual record:

```text
AdminDeviceId
AdministratorId
PublicKey / credential reference
RegisteredAt
LastSeenAt where retained
Status = Active | Revoked
CredentialVersion
```

The exact device-attestation mechanism is implementation-specific, but the design target is a hardware-backed/local private key with only the public credential registered centrally where practical.

A copied browser session/password on an unregistered device does not satisfy the device requirement.

## 7. Zero-trust action rule

Every Platform Admin action is evaluated independently. Being on Tailscale, inside the rack network, already signed in, or holding an administrator role does not create universal authority.

For every action, evaluate as applicable:

```text
authenticated identity
+ current session / recent-auth context
+ registered Admin-device status
+ private-network/trusted-ingress evidence
+ platform capability permission
+ target/resource scope
+ current state/version
+ risk classification
+ step-up authentication
+ physical factor where required
+ JIT/time-bounded elevation where appropriate
+ independent approval where required
+ authoritative audit
```

Privileges are narrowly scoped and time-bounded/JIT where appropriate. A high-risk temporary elevation automatically expires; it is not converted into a standing `SuperAdmin = everything` grant.

## 8. Access to the Admin plane versus authority inside it

Two decisions are separate:

```text
May this identity/device establish a Platform Admin session?
                     │
                     ▼
            Admin-plane access
                     │
                     ▼
What exact action may it perform on this resource now?
                     │
                     ▼
          capability/resource authorization
```

For example, `Platform.Encryption.Policy.View` does not imply `Platform.Keys.Rotate`, `Platform.Backup.Restore`, `Platform.Support.AccessTenant`, or `Platform.Identity.Configure`.

## 9. Private-network enforcement

The current Podman profile uses Tailscale as the preferred private Admin ingress candidate.

Target path:

```text
approved Admin device
      │
      │ Tailscale/private network
      ▼
deny-by-default Grants/ACL-equivalent policy
      │
      ▼
Tailscale Serve / trusted private reverse proxy
      │
      ▼
loopback/private Podman host binding
      │
      ▼
Platform Admin Web/API
```

Do not authorize Admin access by `RemoteIpAddress`, RFC1918 address, `100.x` address, or tailnet membership alone. Trusted Tailscale/proxy identity headers are accepted only when direct access to the backend is structurally prevented and the header source is the trusted ingress.

Another VPN/private-network provider may replace Tailscale later if it preserves or improves these properties.

## 10. OpenBao network exposure

OpenBao is more restricted than Platform Admin.

Normal path:

```text
Admin browser
    -> Platform Admin Web/API
    -> narrow SquiFlow key-management adapter
    -> private OpenBao endpoint
```

Normal browsers, tenant users, Workstations, and the public Internet do not call the OpenBao API directly. Direct operator access exists only for documented bootstrap/unseal/recovery on the private infrastructure path.

## 11. Bootstrap independence

OpenBao startup/recovery must not depend on the very Platform Admin application that needs OpenBao.

Accepted bootstrap path:

```text
approved operator/recovery machine
+ private infrastructure access
+ required physical/recovery factor or 3-of-5 Shamir quorum
        │
        ▼
OpenBao bootstrap/unseal CLI/private procedure
        │
        ▼
OpenBao usable
        │
        ▼
normal Admin API service identity/key-management integration
```

This bootstrap path is not a hidden business API and does not provide general tenant operations.

## 12. SquiFlow key hierarchy

Do not use one global symmetric master key directly for every protected object.

```text
OpenBao/Vault protected key hierarchy
           │
           ├── Workstation recovery KEK
           │       ├── Device A DB DEK
           │       ├── Device B DB DEK
           │       └── Device C DB DEK
           │
           ├── server protected-data KEK/keys
           │       └── selected classified-field/object DEKs
           │
           └── backup/object KEKs
                   └── archive/object DEKs
```

The Platform Admin UI displays safe key metadata/reference/version/state, not raw key bytes.

## 13. Workstation offline/decryption behavior

Normal Workstation database startup remains local-first:

```text
Workstation
    -> Windows protected key material (DPAPI/TPM-backed direction)
    -> device-specific DB DEK
    -> encrypted SQLite/WAL
```

The Workstation does not call Platform Admin/OpenBao every time it opens its database.

Provisioning/recovery maintains a centrally wrapped representation of the same device DEK so an authorized recovery workflow can reprovision a legitimate replacement device context.

## 14. Encryption policy

Platform Admin governs a versioned security policy, not an arbitrary settings blob. The policy covers at least:

```text
Workstation database encryption
server storage encryption requirements
sensitive-field classifications/profiles
object-storage protection
backup encryption/recovery policy
diagnostics/log redaction and sensitive-data prohibition
minimum compatible client/provider versions
key rotation/recovery rules
```

Publication is versioned, audited, integrity-protected where supported, compatible with supported clients/data, and rollback/roll-forward aware.

## 15. Staged rotation and destruction

Key rotation preserves old-data decryptability while new writes transition to the new active version.

Conceptual states:

```text
Pending
Active
DecryptOnly
Retiring
Revoked
Destroyed
```

Revocation and destruction are not synonyms. Irreversible destruction requires the strongest operation-specific authorization, physical/recovery factors, and independent approval policy.

## 16. Four-eyes approval and JIT elevation

The highest-risk destructive operations can require independent approval from a second authorized administrator in addition to the other factors.

Examples:

- destroying root/KEK/key material;
- destructive full-production restore;
- disabling mandatory encryption controls;
- changing root identity or key-management provider configuration;
- granting highest platform-security privileges;
- reducing recovery requirements.

Where a high-risk action needs elevated authority, use short-lived/JIT privilege rather than permanently broadening the operator's role. The elevation is bound to action/scope where possible and expires automatically.

## 17. Break-glass

Break-glass is an emergency recovery workflow, not a standing universal administrator credential.

```text
normal control plane unavailable
 -> invoke documented private recovery path
 -> strong identity/device evidence
 -> physical factor / Shamir quorum as required
 -> narrow emergency authority
 -> high-severity authoritative evidence/audit
 -> restore normal control plane
 -> review and rotate/revoke exposed emergency credentials/factors
```

There is no plaintext default administrator password or hidden public/tenant endpoint.

## 18. Cross-tenant support access

Encryption/key administration does not grant tenant customer-data browsing.

If cross-tenant support access exists, it requires its own permission and workflow:

```text
support access request
 -> reason / ticket / incident reference
 -> exact tenant/resource scope
 -> authorization
 -> optional approval/tenant acknowledgement per policy
 -> time-bounded support session
 -> automatic expiry/revocation
 -> authoritative audit
```

## 19. Authoritative security audit schema

Material security/key/admin actions create durable authoritative audit records separate from lossy Serilog/OpenTelemetry telemetry.

A conceptual record includes:

```text
ActorId
Action
Target
TenantId          # when applicable
DeviceId          # when applicable
Reason            # when required
ApprovalId        # when applicable
PreviousVersion
NewVersion
CorrelationId
Timestamp
Outcome
```

Additional safe identifiers may be included, such as AdminDeviceId, KeyReference, PolicyVersion, RecoveryOperationId, or FailureCode. **Raw key bytes, recovery shares, bearer tokens, cookies, OpenBao tokens, and provider secrets are never audit fields.**

Representative events include:

```text
EncryptionPolicyPublished
KeyRotationRequested
KeyRotated
KeyRevoked
KeyDestructionRequested
KeyDestroyed
DeviceKeyRecoveryRequested
DeviceKeyRecoveryCompleted
BackupRestoreRequested
BackupRestoreCompleted
AdminDeviceRegistered
AdminDeviceRevoked
BreakGlassUsed
SupportSessionGranted
SupportSessionExpired
```

## 20. Availability split

Platform Admin UI/API is a control plane, not a continuous dependency for every ordinary business request.

```text
Admin API unavailable
    -> new admin policy/rotation/recovery actions unavailable
    -> already provisioned ordinary business paths continue where their own dependencies are healthy
```

A server operation that genuinely requires an online OpenBao Transit operation fails closed if OpenBao is unavailable. Therefore application-level Transit encryption is selective, based on data classification, rather than imposed on every ordinary business field.

Workstation local database access remains independent of continuous OpenBao/Admin connectivity after legitimate provisioning.

## 21. Production verification

Before paying-customer production, prove at minimum:

- OpenBao Integrated Storage/Raft survives supported container/host restart and can restore on replacement infrastructure;
- current Raft snapshots are actually restorable;
- 3-of-5 Shamir unseal/recovery succeeds using separately held PGP-protected shares;
- loss of one share and loss of one physical security token are recoverable;
- fewer than the configured threshold shares cannot unseal/recover;
- the periodic unseal/recovery drill is documented and repeatable;
- public/LAN/unapproved tailnet paths cannot reach Platform Admin/OpenBao directly;
- forged private-network identity headers cannot bypass the trusted ingress;
- a valid Admin session copied to an unregistered device fails protected access;
- JIT elevation expires and cannot be reused outside its scope/time;
- four-eyes operations cannot be self-approved when independent approval is required;
- key rotation leaves old ciphertext recoverable through the migration window;
- revocation does not accidentally perform irreversible destruction;
- ordinary encryption administrators cannot gain tenant support/data access without the separate support permission;
- authoritative audit contains the expected actor/target/device/tenant/correlation/version/outcome evidence without secret material;
- Admin API outage does not stop normal already-provisioned Workstation/Web/Sync operation merely because the control UI is unavailable.

## 22. Architectural invariant

> The current SquiFlow rack profile uses OpenBao Transit with durable Raft storage and a tested 3-of-5 Shamir recovery/unseal model. Platform Admin is reachable only through approved private ingress and still independently requires ZITADEL identity, a registered Admin device, operation-specific authorization, and stronger physical/JIT/approval evidence for sensitive actions. Raw root keys are not application data, encryption administration is not tenant-data access, and every material security operation leaves authoritative audit evidence without exposing secret material.
