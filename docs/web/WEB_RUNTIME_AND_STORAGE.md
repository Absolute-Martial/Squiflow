# Web Runtime and Browser Storage Policy

**Version:** v0.0.15

## 1. Current decision

SquiFlow Web is **online-only for business operations** in v0.0.15.

Do not implement partial offline business behavior now.

That means:
- no IndexedDB business replica;
- no queued offline business mutations;
- no service-worker synchronization layer;
- no browser conflict engine;
- no browser long-offline schema/version migration work;
- no attempt to make Web equivalent to the local-first Windows Workstation.

This is deliberately deferred because it adds a second complex offline client before the Desktop/sync model has been proven.

## 2. What browser storage is allowed now

### HttpOnly secure cookie / server-backed session
Preferred for browser authentication/session where the selected Web architecture permits it.

### Memory
Use for active page/query/form state.

### `sessionStorage`
Use only for low-risk, tab-local transient hints such as return route or temporary wizard navigation state.

### `localStorage`
Use only for harmless UI preferences such as theme/sidebar preferences.

Do not store:
- access or refresh tokens;
- passwords;
- permission grants as authority;
- customer/order/payment records;
- provider/platform secrets.

### IndexedDB
Not part of the current business architecture.

A later version may evaluate it for specific offline scenarios, but no current module should depend on IndexedDB.

### Cache Storage / service worker
No business-data service-worker architecture in v0.0.15.

Normal browser/HTTP/CDN caching of versioned static assets is allowed without introducing offline semantics.

## 3. Network loss UX

If Web loses connectivity:
- show a clear offline/connectivity state;
- do not display false success;
- keep safe in-memory form state where practical;
- disable/hold submit until connectivity returns;
- let the user copy/export important unsent text where the form is large/valuable;
- on reconnect, revalidate session, authorization, version and current server state before submitting.

## 4. Performance without offline architecture

Web can still feel fast using:
- immutable hashed static assets;
- CDN/HTTP cache;
- route/code splitting;
- server/query caching where freshness allows;
- pagination/virtualization;
- ETags/conditional requests;
- selective prefetch of the next likely online route when measured useful.

Do not preload the tenant database.

## 5. Future evaluation gate

Browser offline support is reconsidered only after the native Workstation local-first/sync architecture is production-proven and there is a real Web user need.

Any future proposal must separately justify:
- which exact commands/read models need offline behavior;
- browser storage quota/eviction behavior;
- XSS exposure of local business data;
- multi-tab queue ownership;
- logout/user-switch cleanup;
- schema migration;
- long-offline compatibility;
- conflict UX;
- attachments;
- permission revocation while offline.
