# Phase 10B — Replacement-Environment Restore and Security Recovery

## Full rebuild

Recreate SquiFlow on a clean/replacement environment from version-controlled deployment definitions/runbooks rather than original-machine memory.

## Restore

Restore and verify the state that actually exists in the production scope, including as applicable:

- PostgreSQL;
- object state/bytes;
- idempotency/outbox/jobs;
- rules/forms/workflows/configuration;
- consumption/limit state;
- ZITADEL/OpenFGA identifiers/config/reprovision path appropriate to topology;
- OpenBao Raft snapshot/recovery using the approved recovery ceremony/profile;
- Workstation/Guard reconnect with pending local work.

If the accepted OpenBao operating profile remains `3-of-5` Shamir with PGP-protected shares at production time, the drill must prove that exact profile. If a later accepted decision replaces the root/recovery mechanism, qualify the replacement rather than preserving an obsolete example.

## Security recovery

Exercise private break-glass infrastructure recovery, key recovery, Admin/control-plane restart and credential/provider reconnection without an insecure public/tenant bypass.

## Exit gate

Loss of the original server does not mean loss of undocumented configuration, keys or operational knowledge required to restore usable tenant-isolated service.