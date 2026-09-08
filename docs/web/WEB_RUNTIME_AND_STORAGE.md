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

## 3. Online server-side drafts are allowed

`Online-only` does not require valuable work to exist only in browser RAM until final submit.

For long/valuable forms where browser refresh, crash or session expiry would create unacceptable loss, a module may implement an explicit **server-side draft** while the network is available.

A server-side draft:
- is a real versioned server resource, not hidden browser offline state;
- is tenant/permission scoped;
- has explicit ownership/share rules;
- uses idempotent save/autosave semantics;
- exposes `Saved at ...` / save-failed state;
- has retention/abandon/delete policy;
- reauthorizes on later edit/submit;
- uses expected-version conflict handling for multiple tabs/devices.

Do not introduce server-side drafts for every tiny form. Use them where the cost of losing user input justifies the lifecycle complexity.

## 4. Network loss UX

If Web loses connectivity:
- show a clear offline/connectivity state;
- do not display false success;
- keep safe in-memory form state where practical;
- disable/hold submit until connectivity returns;
- let the user copy/export important unsent text where the form is large/valuable;
- if the form has an online server-side draft, show whether the latest edits were actually saved;
- on reconnect, revalidate session, authorization, version and current server state before submitting.

## 5. Performance without offline architecture

Web can still feel fast using:
- immutable hashed static assets;
- CDN/HTTP cache;
- route/code splitting;
- server/query caching where freshness allows;
- pagination/virtualization;
- ETags/conditional requests;
- selective prefetch of the next likely online route when measured useful.

Do not preload the tenant database.

## 6. Accessibility

All Web surfaces follow `docs/ux/ACCESSIBILITY_AND_INTERACTION_QUALITY.md`.

Connectivity, save state, validation, authorization failure and long-running operation status must be conveyed as accessible text/state, not color/icon alone.

Tenant branding/custom domains cannot bypass the accessibility baseline through arbitrary CSS/script.

## 7. Future evaluation gate

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
