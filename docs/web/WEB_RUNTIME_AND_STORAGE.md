# Web Runtime and Browser Storage Policy

**Version:** v0.1.0

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

The selected topology also defines `Secure`/`SameSite`/scope behavior, CSRF/antiforgery for state-changing requests, session rotation/expiry, and Tenant Web versus Platform Admin/custom-domain separation. Owner: `docs/security/APPLICATION_SECURITY_BASELINE.md` and `docs/security/IDENTITY_AND_SESSIONS.md`.

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

Web output is encoded by default. Any implemented rich content requires a narrow sanitizer and an explicit Content Security Policy/security-header design; browser storage/caching must not turn untrusted content or stale permission/tenant state into authority.

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

## 6. Blazor state placement: `stateless` does not mean no state

SquiFlow's goal of replaceable/stateless **server compute** must be interpreted carefully with Blazor Web App.

If the chosen render mode uses Interactive Server, Blazor can maintain a per-user circuit in server memory. Component/scoped-service state in that circuit is process/node state, not durable business truth.

Therefore:
- do not store authoritative order/payment/permission/business state only in a Blazor circuit;
- valuable user-authored work that must survive process/network loss uses an explicit server-side business draft/resource where justified;
- losing a circuit may lose disposable UI state, but must not erase already committed business state;
- process-local circuit/cache/session state cannot be treated as shared multi-node state unless the chosen hosting/session design actually provides that behavior;
- do not claim transparent Web-node failover merely because the Core API/application layer is otherwise stateless.

The exact Blazor render-mode/session/circuit topology is a Phase-1 implementation decision. If Interactive Server is selected for relevant surfaces, evaluate actual memory use, reconnect behavior, circuit persistence, deployment draining, and multi-node/session-affinity implications before promising seamless failover.

This does **not** add Redis as a baseline. A distributed state/cache provider is selected only if the chosen Web topology actually requires one.

## 7. Future offline evaluation

Revisit browser offline behavior only after Workstation local-first/sync is production-proven and a real Web customer journey requires it.

Any later proposal must justify the exact offline reads/commands plus quota/eviction, XSS exposure, multi-tab ownership, schema migration, long-offline compatibility, attachments and permission-revocation behavior.

## Source note

Current .NET 10 server-side Blazor guidance documents that Interactive Server is stateful, keeps user state in server-memory circuits, and may require deliberate persistence/session-affinity/distributed-state choices for multi-server scenarios. That framework behavior is why SquiFlow distinguishes stateless business/application correctness from transient Blazor circuit state.
