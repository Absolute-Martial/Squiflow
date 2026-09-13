# Device and Synchronization Management

**Status:** Accepted architecture direction

## 1. Device management is a SquiFlow capability

Device/workstation identity and synchronization status are not only local utility concerns. They form a tenant-scoped SquiFlow capability with host-specific presentation.

The Web and Workstation may both expose the capability, but they show different scopes according to current authorization and local information availability.

## 2. Authoritative device record

The server owns the authoritative device/workstation record and lifecycle state.

Conceptually it includes:

```text
DeviceId
TenantId
FriendlyName
DeviceType / HostKind
Enrollment/authorization state
Application/protocol version
LastSeenAt
LastSuccessfulSemanticSyncAt
LastKnownSyncRevision/cursor
Revoked/Suspended state
created/updated/audit metadata
```

Do not treat a locally editable machine name or Workstation SQLite row as authority for enrollment or revocation.

## 3. Workstation local view

The Workstation can always show local information for the current device that it can know safely, including:

```text
This device
├── DeviceId / friendly name
├── enrollment/session state
├── current application version
├── connectivity
├── local database status/size where safe
├── last successful semantic sync
├── pending upload count/bytes/oldest age
├── pending remote apply/download state
├── conflict/rejected/retryable items requiring action
├── attachment-transfer backlog
└── protocol/upgrade state
```

These values distinguish local evidence from server-confirmed state.

If the current actor has the required tenant permission and the Workstation is online, it may also show organization device records through the authoritative backend. This is not an offline replicated platform-control database.

## 4. Web view

Authorized Web users may see tenant-scoped device/workstation inventory and synchronization health such as:

```text
Front Desk PC
  Authorized
  Online/recently seen
  App version: ...
  Last semantic sync: ...
  Pending/health summary where safely available

Office Laptop
  Offline
  Last seen: ...
  Last semantic sync: ...

Counter 2
  Revoked
```

The Web does not need the Workstation's private local-only diagnostics or filesystem details unless a specific redacted diagnostic workflow explicitly uploads them.

## 5. Permission examples

Use stable capability permissions rather than broad `is owner` UI conditions. Exact names close with the first implementation, but expected semantics include:

```text
Devices.ViewCurrent
Devices.ViewTenant
Devices.Rename
Devices.Enroll
Devices.Suspend
Devices.Revoke
Devices.ViewSyncHealth
```

UI visibility is not enforcement. Every authoritative device mutation re-checks current server authorization.

## 6. Revocation and offline behavior

A revoked/suspended Workstation may already contain offline bytes. Revocation means it can no longer obtain server-authoritative acceptance/synchronization under the revoked device authority; it does not magically erase local storage.

On reconnect:

```text
Workstation request
→ device/session validation
→ revoked/suspended?
   ├─ yes → fail closed with explicit device state/recovery instruction
   └─ no  → continue normal sync admission
```

Pending local intent is preserved according to support/recovery policy rather than silently discarded.

## 7. Sync state is not just connectivity

`Online` is not equivalent to `Synchronized`.

Support/UI should distinguish at least:

- connectivity/network state;
- authentication/device state;
- last successful semantic sync;
- pending backlog size/age;
- server throttling/backpressure;
- current cursor/checkpoint compatibility;
- conflict/rejection/review state;
- attachment transfer state;
- upgrade/resnapshot requirement.

This prevents a green network indicator from hiding a stuck business backlog.

## 8. Host and backend boundaries

Local Workstation status comes from SQLite/local runtime/Guard-safe evidence.

Tenant-wide device/sync state comes from the authoritative server path.

Workstation semantic synchronization enters through the Sync ingress workload when that split is implemented; ordinary Web device administration enters through the interactive Web/API ingress. Both use the same device capability/server authority rather than separate device models.

## 9. Observability/privacy

Do not use unbounded DeviceId/UserId/TenantId values as ordinary metric labels. Device-specific investigation belongs in controlled logs/traces/support views backed by explicit application state where needed.

Only upload local diagnostic details through the defined redaction/diagnostic pipeline. Device inventory must not become an unrestricted remote filesystem/process-inspection surface.

## 10. Related owners

- `docs/sync/SYNC_AND_AUTHORITY.md`
- `docs/admin/ADMIN_SURFACES.md`
- `docs/architecture/WEB_AND_SYNC_INGRESS.md`
- `docs/architecture/CAPABILITY_CORE_AND_HOST_EXECUTION.md`
- `docs/workstation/LOCAL_FIRST_DESKTOP.md`
