# Browser Storage, PWA, and Partial Offline

**Version:** v0.0.15

## Storage policy

| Store | Intended use | Never treat as |
|---|---|---|
| HttpOnly secure cookie/server session | authentication/session | UI/business data bucket |
| memory | active UI/query state | durable store |
| sessionStorage | tab-local transient hints | secrets/business truth |
| localStorage | harmless UI preferences | auth tokens/sensitive records |
| IndexedDB | explicit drafts/bounded offline data | invisible full database replica |
| Cache Storage | app shell/hashed assets | unrestricted private API cache |

For any IndexedDB business data define schema version, tenant/user partition, limits, expiry, logout/user-switch behavior, upgrade migration, corruption handling and conflict semantics.

## Offline tiers

Tier 0: cached shell/assets + useful offline/degraded UI.

Tier 1: selected IndexedDB drafts + bounded low-risk read cache + freshness display.

Tier 2: selected offline commands only after idempotency, multi-tab coordination, quota/eviction, version migration, long-offline recovery and conflict/security tests.

Tier 3: full Desktop-equivalent browser offline is not baseline.

The Workstation remains the strongest offline/local-first client.

## Preloading

Precache shell/static assets and critical routes. Do not preload all customers, transactions, attachments, rules or privileged data.
