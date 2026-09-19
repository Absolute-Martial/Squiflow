# Encryption, Key Management, and Zero-Trust Platform Administration

**Status:** Accepted architecture direction  
**Version:** v0.1.0
**Base architecture revision:** MR !49

## 1. Decision

SquiFlow will **not implement its own cryptographic key vault or root-key lifecycle**. SquiFlow owns policy, authorization, resource/key mapping, recovery workflow, audit semantics, and provider integration. A mature key-management system owns cryptographic root/key storage and cryptographic operations.

The initial self-hosted candidate is **OpenBao**, running in the current Podman/rack deployment profile. HashiCorp Vault remains a compatible future provider choice if licensing, operations, support, or deployment requirements justify it.

```text
SquiFlow Platform Admin
        │
        │ policy / authorization / approval / audit
        ▼
SquiFlow key-management adapter
        │
        ▼
OpenBao / Vault
        │
        ├── key generation
        ├── wrap / unwrap
        ├── Transit encryption where justified
        ├── key versions / rotation / rewrap
        ├── revoke / retire / destroy under policy
        └── provider audit/evidence
```

SquiFlow application code must not contain a home-grown replacement for OpenBao/Vault cryptography, seal/unseal, root-key splitting, key rotation, or recovery.

Detailed current operating profile: `docs/security/OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE.md`.

## 2. Encryption is centrally governed, not centrally decrypted by the UI

The Platform Admin control plane governs:

- encryption policy;
- key lifecycle policy;
- rotation/revocation/destruction requests;
- device-key recovery/reprovisioning;
- backup/object encryption policy;
- protected-field classification;
- recovery/break-glass workflow;
- approval/JIT requirements;
- authoritative security audit.

Platform Admin Web/Admin API do **not** normally receive or display raw master keys, KEKs, DEKs, OpenBao root/seal material, recovery shares, or tenant plaintext merely because an operator administers encryption.

```text
Platform Admin Web
      │
      ▼
Platform Admin API
      │
      │ policy/lifecycle command only
      ▼
Key-management adapter
      │
      ▼
OpenBao/Vault
```

Raw key export is not an ordinary administration feature.

## 3. Key hierarchy

Do not use one symmetric master key directly for every database, backup, object, and tenant.

Use an envelope-style hierarchy:

```text
OpenBao/Vault root/seal protection
              │
              ▼
       Key Encryption Keys (KEKs)
              │
       ┌──────┼─────────┐
       ▼      ▼         ▼
  Device DEKs Server DEKs Backup/Object DEKs
       │      │         │
       ▼      ▼         ▼
   SQLite   selected   encrypted archives/
            fields     sensitive objects
```

A compromise of one Workstation DEK must not automatically disclose every Workstation or every server/backup object.

## 4. OpenBao initial role and current rack profile

OpenBao is the initial provider candidate for:

- Transit/key wrapping;
- high-entropy data-key generation;
- KEK/key-version lifecycle;
- rewrap/rotation support;
- policy-controlled service identities;
- auditable key use;
- Integrated Storage/Raft;
- supported seal/unseal mechanisms.

The **accepted initial rack profile** is:

```text
OpenBao
+ Integrated Storage / Raft
+ Transit engine
+ Shamir 3-of-5 unseal/recovery
+ PGP-encrypted shares
+ separate secure holders/locations
+ regular tested Raft snapshots
+ periodic recovery/unseal drills
```

This `3-of-5` profile supersedes earlier illustrative threshold examples. A later threshold change requires an explicit superseding decision and recovery proof.

When SquiFlow has a trustworthy external root, a PKCS#11/HSM or external KMS/HSM auto-unseal strategy may supersede the manual Shamir profile after a tested migration/recovery process.

SquiFlow uses a narrow provider boundary because this is a real external-provider/replacement boundary. It must not reproduce the entire OpenBao/Vault API.

Conceptually useful operations include:

```text
GenerateDataKey
WrapKey
UnwrapKey
RewrapKey
GetKeyVersion/status
RotateKey
Revoke/retire/destroy according to policy
```

Provider SDK types do not leak into business Capability Cores.

## 5. Seal/unseal and bootstrap trust

OpenBao/Vault protects SquiFlow keys, but the key service itself must be recoverably sealed/unsealed. This is a private infrastructure security problem and must not be hidden behind Platform Admin.

Do not create a circular dependency:

```text
Platform Admin needs OpenBao to start
        AND
OpenBao requires Platform Admin to unseal
```

Use a separate, narrowly scoped bootstrap/unseal ceremony:

```text
approved operator/recovery machine
+ private infrastructure access
+ required physical factor / 3-of-5 recovery quorum
        │
        ▼
OpenBao bootstrap/unseal
        │
        ▼
normal Platform Admin service identities become usable
```

The bootstrap/recovery path is an infrastructure plane, not a second business API.

## 6. Static unseal is not the production security target

Static Key Auto Unseal does not eliminate key custody; it moves the problem to the location holding the static seal key.

Therefore production must not rely on an ordinary plaintext `master.key`/`unseal.key` stored beside the OpenBao container or its Raft data.

Static unseal may be used only as an explicitly documented temporary bootstrap/development mechanism under an accepted threat model. It is not the paying-customer target.

Preferred stronger directions are:

- hardware-backed PKCS#11/HSM seal when practical;
- external KMS/HSM auto-unseal when a trustworthy external root exists;
- or the accepted 3-of-5 Shamir profile until such a root exists.

## 7. Physical security factor and operator custody

Cryptographically sensitive Platform Admin/recovery actions require a physical security/recovery factor according to policy.

```text
approved authenticated administrator
        AND
registered active Admin device
        AND
operation-specific platform authorization
        AND
required physical security/recovery factor or quorum
        AND
step-up/JIT/approval requirements
        =
protected cryptographic administration allowed
```

Possession of only one factor is insufficient.

An ordinary removable drive containing a raw unencrypted master-key file is **not** the production target. Prefer a non-exportable hardware-backed token/HSM where practical. Transitional removable recovery material must itself be encrypted/protected and normally offline.

The physical factor may be inserted/used for bootstrap or high-risk operations and then removed. It must not remain permanently attached merely to keep ordinary tenant traffic running.

## 8. Recovery must survive loss of one physical device/share

Security must preserve confidentiality and authorized recoverability.

The current initial profile uses **5 Shamir shares with a threshold of 3**. Shares are PGP-protected where supported and kept in genuinely separate secure holders/locations.

```text
Primary token/share unavailable
        │
        ▼
other independently protected shares/factors
        │
        ▼
3-of-5 recovery quorum
        │
        ▼
restore/unseal OpenBao
        │
        ▼
provision replacement factor
```

Do not keep the shares together in the same rack, bag, removable device, machine, password vault, or physical location.

Recovery is tested through real snapshot + quorum + replacement-environment drills. Possessing files/shares that have never successfully recovered the environment is not evidence.

## 9. Zero-trust Platform Admin access

Platform Admin is a distinct private security/control plane. Network location alone never grants authority.

Every Platform Admin action is, as applicable:

```text
explicitly authenticated
+ explicitly authorized
+ context/device/session evaluated
+ target/resource scoped
+ current state/version checked
+ risk classified
+ step-up/recent authentication
+ physical factor where required
+ JIT/time-bounded elevation where appropriate
+ independent approval where required
+ authoritatively audited
```

Being inside Tailscale/private networking or already signed in does not grant every capability.

Normal protected Admin access requires layered evidence:

```text
approved private-network path
        │
        ▼
ZITADEL authenticated administrator
        │
        ▼
registered non-revoked SquiFlow Admin device
        │
        ▼
PlatformAdmin.Access
        │
        ▼
operation-specific capability/resource permission
        │
        ▼
stronger factors/elevation/approval when required
```

There is no universal `SuperAdmin = everything` assumption.

Illustrative capability separation:

```text
Platform.Security.View
Platform.Encryption.Policy.View
Platform.Encryption.Policy.Change
Platform.Keys.Rotate
Platform.Keys.Revoke
Platform.Keys.Destroy
Platform.Device.Recovery
Platform.Backup.Restore
Platform.Identity.Configure
Platform.Support.AccessTenant
```

Exact names are capability-catalog details; separation is mandatory.

## 10. Access to Admin Platform is separate from access inside it

Two decisions are independent:

1. may this identity/device reach and establish a Platform Admin session?;
2. which exact platform operation may it perform on the target resource now?

Being admitted to the Admin UI does not grant encryption rotation, backup restore, support/data access, identity-provider reconfiguration, key recovery, or key destruction.

## 11. Registered Admin devices

A Platform Admin device is more than an IP address or browser cookie.

Target model includes an administrator-associated device credential/public key and lifecycle state:

```text
AdminDeviceId
AdministratorId
PublicKey / credential reference
RegisteredAt
LastSeenAt where retained
Status = Active | Revoked
CredentialVersion
```

A password/session copied to an unregistered machine must not satisfy protected Platform Admin access. Exact attestation/hardware binding remains implementation-specific.

## 12. High-risk operations, JIT and four-eyes approval

Higher-risk operations require stronger evidence after the Admin Platform is open.

Examples include:

- root/KEK rotation;
- key revocation/destruction;
- device-key recovery;
- backup restore;
- recovery-policy changes;
- highest-level platform authorization changes;
- identity/key-provider reconfiguration.

Conceptual flow:

```text
current authorized Admin session
        │
        ▼
recent/step-up authentication
        │
        ▼
registered device proof
        │
        ▼
physical security/recovery factor where required
        │
        ▼
short-lived/JIT elevation where required
        │
        ▼
independent approval where required
        │
        ▼
execute through owning provider/capability
        │
        ▼
verify
        │
        ▼
authoritative security audit
```

The highest-risk destructive actions support four-eyes approval. JIT elevation is action/scope/time bounded and expires automatically rather than becoming standing broad authority.

## 13. Encryption administration does not imply tenant-data browsing

`Encryption administration != tenant customer-data access`.

An operator allowed to inspect encryption health or rotate keys does not automatically receive permission to browse tenants' Customers, Orders, Payments, files, or reports.

Cross-tenant support access requires its own explicit permission/workflow, reason/ticket context, tenant/resource scope, time-bounded/JIT session, automatic expiry/revocation, and authoritative audit. Approval or tenant acknowledgement may be required by policy.

## 14. Data-plane availability versus Admin availability

Platform Admin UI/API is a control plane and must not become a continuous dependency for every ordinary request or Workstation startup.

```text
Admin API unavailable
    │
    ├── new admin changes/rotations/recovery may be unavailable
    └── already provisioned ordinary business paths continue where their own dependencies are healthy
```

A server operation that genuinely requires an online OpenBao Transit operation fails closed if the key service is unavailable. Application-level Transit encryption is therefore selective based on classification rather than imposed on every ordinary business field.

Workstation local DB opening does not require continuous Admin/OpenBao connectivity after legitimate provisioning.

## 15. Encryption in transit

Required protection includes:

- Browser -> edge/Admin/Web API over TLS;
- Workstation -> Sync API over authenticated TLS regardless of HTTP/gRPC transport;
- internal API/provider/database links over TLS where required/supported by the selected boundary;
- certificate/server validation is not disabled as a recovery shortcut.

Serialization, `OperationEnvelope`, private IP addressing, and Tailscale membership are not substitutes for transport protection/authentication semantics.

## 16. Server data at rest

Server protection is layered:

1. encrypted block/volume storage for PostgreSQL/WAL/temp/database files according to deployment capability;
2. PostgreSQL authorization/RLS/least privilege for logical access;
3. selective application/key-service encryption for exceptionally sensitive values when the threat model justifies it;
4. encrypted backups and object-store protection.

Do not application-encrypt every DB column by default. Field encryption has query/indexing/runtime-dependency/recovery costs and is driven by classification/threat model.

## 17. Workstation local encryption relationship

Workstation uses device-specific DEKs rather than one platform master key.

```text
OpenBao/Vault recovery KEK
        │ wraps
        ▼
device-specific DB DEK
        │
        ├── local usable copy protected by Windows/device mechanism
        └── central recovery-wrapped copy
```

Normal startup uses local DPAPI/TPM-backed protection direction and can remain offline. Detailed owner: `docs/workstation/WORKSTATION_ENCRYPTION_AND_KEY_RECOVERY.md`.

## 18. Object, backup, log and temporary-file protection

Large object storage and backups are separate from relational DB encryption.

- provider-side encryption at rest is used where available/appropriate;
- highly sensitive objects may use application-side encryption when justified;
- backup archives containing recoverable business/security state are encrypted before off-site upload;
- the backup destination does not hold the only usable decryption key;
- key/recovery material is independently recoverable;
- restore drills verify decrypt + integrity + reconstruction, not merely download.

Encryption at rest does not permit plaintext secrets/credentials/customer content to leak into logs/diagnostics.

Never log raw:

- access/refresh tokens or cookies;
- OpenBao/Vault tokens;
- KEKs/DEKs/root/recovery material;
- provider credentials;
- unrestricted customer payloads.

Sensitive temporary files, migration copies, exports, document staging, and support bundles have bounded location/lifetime/cleanup and protection appropriate to classification.

## 19. Authoritative audit

Material cryptographic/Platform Admin operations create durable authoritative security-audit records separate from lossy telemetry.

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

Conceptual safe audit fields include:

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

Additional safe references may include AdminDeviceId, KeyReference, PolicyVersion, RecoveryOperationId, or FailureCode. Raw key bytes, recovery shares, bearer tokens, cookies, OpenBao tokens, and provider secrets are never audit fields.

OpenTelemetry/Serilog may receive a safe operational copy; they are not the security-audit authority.

## 20. Break-glass

If normal Platform Admin is unavailable, a private infrastructure recovery path may bootstrap/unseal/redeploy the control plane. It is:

- not exposed to tenant users;
- not a hidden Web/Sync business endpoint;
- least privilege;
- runbook-driven;
- private-network/physical-factor restricted;
- high-severity evidenced/audited where technically possible;
- followed by credential/factor review/rotation as appropriate.

## 21. Verification

Before production prove, at minimum:

- OpenBao Integrated Storage/Raft survives/recoverably restores outside the disposable container layer;
- supported Raft snapshots are actually restorable;
- 3-of-5 Shamir unseal/recovery works with separately held PGP-protected shares;
- loss of one share/token remains recoverable and fewer than threshold shares cannot unseal;
- periodic unseal/recovery drills are repeatable;
- public/LAN/unapproved private-network paths cannot directly reach Platform Admin/OpenBao;
- forged proxy/network identity headers cannot bypass trusted ingress;
- a copied Admin session on an unregistered device cannot satisfy protected access;
- JIT elevation expires and cannot be reused outside scope/time;
- four-eyes operations cannot self-approve where independence is required;
- rotation leaves old ciphertext recoverable during migration;
- revocation does not accidentally destroy keys;
- encryption admins cannot gain tenant support/data access without the separate permission;
- authoritative audit records actor/target/device/tenant/correlation/version/outcome evidence without secret material;
- Admin API outage does not stop ordinary already-provisioned Web/Sync/Workstation operation merely because the control UI is unavailable.

## 22. Architectural invariant

> SquiFlow centrally governs encryption through a private zero-trust Platform Admin control plane, while cryptographic root/key custody belongs to a proven key-management system. The current rack profile uses OpenBao Transit + Raft with tested 3-of-5 Shamir recovery, PGP-protected shares and recovery snapshots/drills. Network location, an Admin login, or possession of one removable factor alone is never sufficient authority. Workstation offline operation remains possible through device-local protected DEKs, and every material security operation leaves authoritative audit evidence without exposing secret material.

Related owners:

- `docs/admin/ADMIN_SURFACES.md`
- `docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`
- `docs/security/ENCRYPTION_POLICY_KEY_LIFECYCLE_AND_PRIVILEGED_ACCESS.md`
- `docs/security/OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE.md`
- `docs/workstation/WORKSTATION_ENCRYPTION_AND_KEY_RECOVERY.md`
- `docs/operations/PRIVATE_ADMIN_NETWORK_AND_PODMAN.md`
- `docs/operations/DEPLOYMENT_CAPACITY_AND_RECOVERY.md`
