# Phase 10D — Provider Migration, Limits, and Operator Ownership

## Provider migration readiness

Before/at first paying customer, implement/qualify paid `IObjectStore`/`IBackupTarget` adapters if the current product decision still requires migration from bootstrap providers, or migrate earlier when reliability/privacy/capacity/compliance already demands it.

Exercise copy/migrate/verify/cutover/restore without changing business code or object identity.

If later evidence changes the exact commercial/provider trigger, update that decision explicitly; do not let the roadmap silently hard-code a provider forever.

## Production limits

Close the actual hard limits required by the first production profile: DB connections, upload/batch sizes, Worker concurrency, local staging, provider/storage capacity, telemetry buffers and any tenant/manual contractual limits.

Do not invent subscription tiers/prices merely because limits exist.

## Operator ownership

Document who receives alerts, who can access/recover the rack, who holds private infrastructure credentials/recovery shares, who can administer providers/identity/auth, and what happens if the primary operator is unavailable.

## RPO/RTO/support promises

Set provisional/final values from restore/capacity evidence rather than guesswork.

## Exit gate

The first customer profile has explicit resource/provider/operator/recovery ownership and no critical production limit depends on telemetry-only accounting.