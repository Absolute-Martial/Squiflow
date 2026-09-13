# Architecture Decisions Consolidated from the 2026-09-13 Shared/Authoritative Modules Discussion

**Status:** Accepted direction for the new architecture revision  
**Version:** v0.0.19  
**Base commit:** `d6df80a2613a330b7abd3adce32ca19ab20f5d1b`

This document records the decisions made after the base commit so that the merge request does not silently lose or rewrite accepted architecture detail.

## Decisions

1. **Keep `Foundation`; do not introduce a universal `Shared`, `Common`, or `Utils` bucket.** Foundation owns narrow product-wide primitives/abstractions, not Customers/Orders/Inventory business meaning.

2. **Business reuse is capability-owned.** Customers, Orders, Inventory, Payments, Devices, etc. each have one SquiFlow business meaning. Host-specific projects/adapters must not fork that meaning.

3. **Distinguish shared deterministic capability logic from server authority.** A Capability Core can contain deterministic domain/business processing reusable by Workstation/server/tests. Server-authoritative application/use-case code supplies current authority/current facts and commits authoritative state.

4. **WebApi and SyncApi are separate workload hosts, not separate business backends.** WebApi is optimized for interactive Web/API traffic. SyncApi is optimized for device/sync traffic, batching, cursors, backpressure, idempotency, reconnect bursts and optional gRPC/streaming when proven.

5. **Both backend hosts converge on the same authoritative capability modules.**

```text
WebApi -----┐
            ├──> same authoritative Orders/Customers/Inventory/... modules
SyncApi ----┘                    │
                                ▼
                         PostgreSQL + Outbox
```

6. **Different entry use cases are allowed.** Web can call `Orders.CreateOrder`; Sync can call `Orders.AdmitProvisionalOrder`. Those are different workflow/trust entry paths into the same Orders capability, not separate implementations.

7. **The Workstation model is provisional local execution → authoritative admission/commit.** Do not describe it as blindly running/storing the complete transaction twice. Sync sends semantic operation intent plus revision/evidence needed for admission.

8. **Revision/evidence-based selective re-evaluation remains accepted.** If relevant authoritative dependencies have not changed, admission can use the fast path; if they changed, re-evaluate only the necessary authoritative decisions. Security/current-authority checks are never skipped merely because a revision token matches.

9. **The Worker uses the same authoritative modules.** Worker-specific entry use cases are allowed, but Worker must not become `WorkerOrderService`/`WorkerCustomerService` business forks.

10. **Scheduler owns timing, Worker owns durable execution, module owns business mutation.** Preferred chain: `Scheduler -> durable occurrence/job -> Worker -> authoritative module -> persistence`.

11. **Reads remain module-owned first-class operations.** WebApi/SyncApi do not bypass modules with arbitrary controller-level SQL. Optimized module-owned read projections are allowed and do not create a second source of truth.

12. **Persistence is behind the module/application boundary, not owned by WebApi/SyncApi.** PostgreSQL remains authoritative server state; SQLite/WAL remains Workstation-local/provisional state; object storage holds large durable objects with authoritative metadata; caches are disposable; inbox/outbox/jobs/idempotency are explicit durable processing state.

13. **Do not introduce a mandatory `CoreApi` network hop between WebApi/SyncApi and business modules.** In-process module execution remains the modular-monolith default; shipping the same module assembly in multiple hosts is acceptable deployment duplication.

14. **Module physical project count is earned.** Logical boundaries such as Domain/Contracts/Application/Workstation/Postgres may be documented, but empty projects must not be scaffolded merely for diagram symmetry. Add compile-time splits when cross-host reuse, provider isolation, dependency enforcement, packaging or module complexity makes them valuable.

15. **Presentation remains host-specific.** Share business contracts and deterministic semantics, not Avalonia and Web ViewModels/navigation state.

16. **Capability-owned definitions stay with the capability.** Orders owns `Orders.Create`, Orders feature definitions and Orders settings; the central feature/authorization systems evaluate them but do not become giant global buckets that own their business meaning.

17. **Cross-module communication must respect ownership.** A module should consume another capability's public contract/application surface or published integration event rather than reaching into its persistence adapter/private tables.

18. **Domain/internal events and public integration events are distinct.** Internal domain events need not become permanent cross-module contracts; publish explicit integration events when another module/process must consume a stable contract.

19. **Web and Workstation remain peer product hosts over shared SquiFlow capabilities.** Capability availability and execution mode are explicit: `DeviceLocal`, `LocalProvisional`, or `ServerAuthoritative`. Web does not gain the Workstation SQLite/Guard/device-runtime model implicitly.

20. **No architectural claim in this revision requires immediately creating empty `web-api`, `sync-api`, Worker, persistence, or per-module adapter projects.** The current compact host/module implementation remains valid until the real boundary is implemented.

21. **Workstation SQLite business persistence is encrypted at rest.** Qualification covers the actual SQLite/WAL/journal/shared-memory/temp/backup durability surface, not only the main `.db` file. Exact SQLite encryption provider remains a Phase-2 qualification choice.

22. **Workstation encryption uses device-specific DEKs rather than one global master key.** The usable local DEK is protected through a Windows/device mechanism (DPAPI/TPM-backed direction to qualify) so legitimate offline startup does not depend on Platform Admin/OpenBao availability.

23. **Device-key recovery is centrally governed but not continuously central.** A recovery-wrapped representation of the Workstation DEK is protected by the selected key-management system. Recovery/reprovisioning requires zero-trust Platform Admin authorization/audit; normal offline Workstation use does not contact the Admin plane.

24. **Guard is not a key vault.** Guard coordinates update/recovery safety but does not permanently own SQLite DEKs, KEKs, OpenBao/Vault root material, or central recovery credentials.

25. **Migration/update/rollback checkpoints remain encrypted.** An encrypted live SQLite database must not silently become a plaintext backup/migration copy. Recovery manifests may store key references/versions but never raw key material.

26. **BitLocker/device-volume encryption is defense in depth, not the sole SquiFlow local-data guarantee.** Application/database encryption and volume encryption solve different theft/copy threats; neither makes a fully compromised running device safe.

27. **SquiFlow will not build its own cryptographic key-management service.** A mature system such as OpenBao/HashiCorp Vault owns cryptographic root/key storage and operations. SquiFlow owns policy, authorization, resource mapping, provider adapter, recovery workflow and audit.

28. **OpenBao is the initial self-hosted key-management candidate for the current Podman profile.** HashiCorp Vault remains a replacement candidate through a deliberately narrow provider boundary. OpenBao Transit/data-key/wrap/rewrap/rotation capabilities are the intended class of functionality; SquiFlow does not reproduce the whole provider API.

29. **Static unseal with a plaintext key file beside the service is not the production target.** Static unseal may be temporary/bootstrap only under an explicit threat model. Production direction is hardware-backed PKCS#11/HSM, trustworthy external KMS/HSM auto-unseal, or the accepted Shamir recovery profile until a stronger external root exists.

30. **Platform encryption is centrally governed but raw keys are not exposed through Platform Admin.** Admin Web/API controls policy, rotation/revocation/recovery requests and audit; KEKs/DEKs/root/seal material stay behind the key-management boundary.

31. **Platform Admin follows a two-anchor/zero-trust access model.** Protected access requires an authenticated authorized administrator on a registered active Admin device; cryptographically sensitive operations additionally require the configured physical security/recovery factor and step-up/approval according to operation risk.

32. **One lost physical token or recovery share must not permanently destroy customer data.** The accepted initial rack recovery profile is **Shamir 3-of-5**, with PGP-protected shares held in genuinely separate secure locations/holders and tested recovery. Earlier illustrative threshold examples are superseded by this operating profile.

33. **An ordinary USB drive containing a raw unencrypted master key is not the production root-of-trust target.** Prefer a non-exportable hardware-backed token/HSM. If removable media temporarily carries bootstrap/recovery material during the early private phase, the material itself is protected/offline and treated as transitional.

34. **Platform Admin encryption authority does not imply cross-tenant customer-data browsing.** Key/policy administration and tenant-data support access are separate permissions/workflows.

35. **The Admin control plane is not a continuous dependency for ordinary tenant operation.** Admin API outage blocks new admin operations but must not by itself stop already provisioned normal Web/Sync/Workstation business paths. Operations that genuinely require an online key-service Transit operation fail closed if that key service is unavailable.

36. **The current server deployment uses Podman, and Platform Admin/OpenBao are private control-plane services.** The Admin backend should bind only to loopback/private service networking and be reached through a trusted private-network ingress rather than an all-interface/public port.

37. **Tailscale is the preferred current private Admin ingress candidate.** Use deny-by-default Grants/ACL-equivalent policy and a trusted Tailscale Serve/private reverse-proxy path to the loopback-bound Admin service. Tailnet membership alone does not imply SquiFlow Platform Admin authority.

38. **Do not authorize Platform Admin by apparent `RemoteIpAddress` or private CIDR.** Private IP/Tailscale identity is only network evidence. ASP.NET authorization still requires ZITADEL identity, registered Admin-device proof, platform permission, and stronger factors for sensitive operations.

39. **Trusted proxy/Tailscale identity headers are accepted only from the trusted private ingress path.** The Admin backend must not be directly reachable in a way that lets a caller spoof equivalent forwarded/identity headers.

40. **OpenBao is more restricted than Platform Admin.** Normal browsers/users do not call OpenBao directly. Admin API uses a narrow key-management adapter; direct operator OpenBao access exists only for documented bootstrap/unseal/recovery on the private infrastructure path.

41. **OpenBao bootstrap/unseal is not circularly dependent on Platform Admin.** A separate private runbook/ceremony can unseal/recover the key service using the approved operator machine plus required physical/recovery factor/quorum, after which normal Admin services use least-privilege service identities.

42. **The physical security factor is not intended to stay attached continuously.** It is used for bootstrap/recovery/high-risk administration and can be removed; ordinary tenant operations do not require the operator to insert a key for each request.

43. **Server encryption is layered.** Use encrypted server/block storage for PostgreSQL/WAL/temp according to deployment capability, TLS for relevant network/provider boundaries, PostgreSQL/RLS/least privilege for logical controls, and selective application/key-service field encryption only when data classification justifies the query/runtime/recovery cost.

44. **Object storage, backups, logs, diagnostics and temporary files are part of the encryption threat model.** Encrypted DBs do not permit plaintext off-site backups, raw key material in manifests/logs, or unrestricted sensitive content in Serilog/OTel/crash/support artifacts.

45. **Key rotations/recovery/break-glass actions produce authoritative security audit state separate from lossy observability.** Audit records identify actor/target/reason/version/outcome without recording secret key bytes.

46. **Encryption policy is a versioned security artifact, not an ordinary setting.** The centrally governed policy covers Workstation database encryption, server storage, sensitive-field classifications, object storage, backups and diagnostics/redaction. Publication is audited, integrity-protected where supported, rollback/roll-forward aware, and compatible with supported older Workstations/data formats.

47. **Key rotation is staged and revocation is distinct from destruction.** New key/version activation must preserve decrypt access to old ciphertext while rewrap/re-encryption progresses and is verified. Conceptual lifecycle states include `Pending`, `Active`, `DecryptOnly`, `Retiring`, `Revoked`, and `Destroyed`; exact provider names may differ, but irreversible destruction requires the strongest controls.

48. **The highest-risk destructive platform operations support four-eyes approval.** Actions such as key destruction, destructive full restore, disabling required encryption, changing root identity/key-provider configuration, or granting highest platform-security authority can require an independent second approver in addition to step-up/device/physical-factor checks. This is risk-based and is not required for ordinary low-risk administration.

49. **Break-glass and cross-tenant support access are explicit, bounded workflows.** Break-glass is narrow, private, strongly verified, high-severity audited/evidenced, followed by post-use credential/factor review/rotation, and never a standing hidden bypass. Cross-tenant support requires its own permission, reason/ticket context, tenant/resource/time scope, optional approval/tenant acknowledgement according to policy, automatic expiry, and audit; encryption administration alone never grants it.

50. **The current OpenBao rack operating profile is explicit, not implied.** It is `OpenBao Transit + Integrated Storage/Raft + Shamir 3-of-5 + PGP-encrypted shares + separate secure holders/locations + regular tested Raft snapshots + periodic recovery/unseal drills`. A later switch to PKCS#11/HSM or external KMS/HSM auto-unseal requires an explicit migration and recovery proof.

51. **Every Platform Admin action is zero-trust evaluated, not only login.** Evaluate authenticated identity, current/recent-auth context, registered Admin-device status, trusted private-ingress evidence, operation-specific capability/resource permission, target/current version, risk classification, step-up/physical factor where required, **JIT/time-bounded elevation where appropriate**, independent approval where required, and authoritative audit. A temporary elevation expires automatically rather than becoming a standing broad role.

52. **Material security/key-management audit has an explicit authoritative evidence schema.** Safe conceptual fields include `ActorId`, `Action`, `Target`, optional `TenantId` and `DeviceId`, `Reason`, `ApprovalId`, `PreviousVersion`, `NewVersion`, `CorrelationId`, `Timestamp`, and `Outcome`, plus safe references such as `AdminDeviceId`, `KeyReference`, `PolicyVersion`, `RecoveryOperationId`, or `FailureCode`. Raw key bytes, recovery shares, bearer tokens, cookies, OpenBao tokens and provider secrets are never audit fields. OTel/Serilog may receive a safe copy but are not the audit authority.

53. **MR !49 must preserve accepted target-branch v0.0.19 canonical architecture while adding these refinements.** `REPOSITORY_STRUCTURE`, `WEB_AND_SYNC_INGRESS`, `MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES`, and `MATERIAL_DECISION_HISTORY` must not be deleted/down-revved merely because the source branch started from a revert commit. Focused current owners and `CURRENT_DECISIONS.md` govern current semantics; older illustrative wording is superseded where it conflicts.

## Canonical server picture

```text
                           SQUIFLOW
                              │
              ┌───────────────┴────────────────┐
              │                                │
      interactive workload              sync workload
              │                                │
              ▼                                ▼
         SquiFlow.Web.Api                SquiFlow.Sync.Api
              │                                │
              └───────────────┬────────────────┘
                              ▼
                SAME AUTHORITATIVE MODULES
           Orders / Customers / Inventory / ...
                              │
                     PostgreSQL + Outbox
                              │
                              ▼
                         Worker Host
```

## Canonical security/control-plane picture

```text
Approved Admin Device
      +
ZITADEL admin identity
      +
private network (Tailscale current candidate)
      +
operation-specific platform authority
      +
physical security factor / recovery quorum for protected crypto operations
      +
JIT / independent approval where required
      │
      ▼
Platform Admin Web/API
      │
      ▼
SquiFlow key-management adapter
      │
      ▼
OpenBao Transit + Raft
      │
      ├── server/backup/object key lifecycle
      └── wrapped Workstation recovery keys

Initial OpenBao recovery:
3-of-5 PGP-protected Shamir shares + tested snapshots/recovery drills

Workstation normal offline use:
Windows-protected device DEK -> encrypted SQLite/WAL
```

Detailed owners:

- `docs/architecture/AUTHORITATIVE_CAPABILITY_MODULES.md`
- `docs/architecture/MODULE_OWNERSHIP_PERSISTENCE_AND_PROJECT_BOUNDARIES.md`
- `docs/admin/ADMIN_SURFACES.md`
- `docs/architecture/CONTROL_PLANE_AND_DATA_PLANE.md`
- `docs/security/ENCRYPTION_KEY_MANAGEMENT_AND_ZERO_TRUST_ADMIN.md`
- `docs/security/ENCRYPTION_POLICY_KEY_LIFECYCLE_AND_PRIVILEGED_ACCESS.md`
- `docs/security/OPENBAO_AND_ZERO_TRUST_ADMIN_OPERATING_PROFILE.md`
- `docs/workstation/WORKSTATION_ENCRYPTION_AND_KEY_RECOVERY.md`
- `docs/operations/PRIVATE_ADMIN_NETWORK_AND_PODMAN.md`
- `docs/decisions/CURRENT_DECISIONS.md`
