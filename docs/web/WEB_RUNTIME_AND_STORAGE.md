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

This avoids a second complex offline client before Desktop/sync is proven.

## 2. Browser storage

### HttpOnly secure cookie / server-backed session
Preferred for browser authentication/session where the selected Web architecture permits it.

### Memory
Active page/query/form state.

### `sessionStorage`
Only low-risk tab-local transient hints such as return route/wizard navigation.

### `localStorage`
Only harmless UI preferences such as theme/sidebar preference.

Do not store access/refresh tokens, passwords, permission authority, customer/order/payment records or provider secrets there.

### IndexedDB / service-worker business storage
Not part of the current business architecture.

Normal HTTP/CDN caching of versioned static assets is fine.

## 3. Online server-side drafts

`Online-only` does not require valuable work to exist only in browser RAM until final submit.

For a genuinely long/valuable form, a module may implement an explicit **server-side draft**:
- real versioned server resource;
- tenant/permission scoped;
- explicit owner/share rules;
- idempotent save/autosave;
- visible save-success/failure state;
- retention/abandon policy;
- reauthorization on later edit/submit;
- expected-version conflict handling.

Do not create drafts for every tiny form.

## 4. Network loss

If Web loses connectivity:
- show a clear connectivity state;
- never display false success;
- keep safe in-memory form state where practical;
- require reconnect before business commit;
- if a server-side draft exists, show whether the latest edit actually reached the server;
- on reconnect, revalidate session, authorization and current resource version/state.

## 5. Performance without offline architecture

Use ordinary techniques only when useful:
- immutable hashed static assets;
- CDN/HTTP cache;
- route/code splitting;
- bounded server/query cache where freshness permits;
- pagination/virtualization;
- ETags/conditional requests;
- measured prefetch.

Do not preload the tenant database.

## 6. Future offline evaluation

Revisit browser offline behavior only after Workstation local-first/sync is production-proven and a real Web customer journey requires it.

Any later proposal must justify the exact offline reads/commands plus quota/eviction, XSS exposure, multi-tab ownership, schema migration, long-offline compatibility, attachments and permission-revocation behavior.
