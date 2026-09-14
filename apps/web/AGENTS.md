# Tenant Web Instructions

These rules apply below `apps/web/` in addition to the root instructions.

## Role

Tenant Web is a presentation/interactive host over authoritative server-side capability behavior. It is not a second business implementation and not a browser-authoritative offline database.

## Structure

- Keep Blazor components focused on presentation, interaction state, request initiation, and understandable error/pending UX.
- Put business invariants and central authorization below the presentation boundary.
- Use explicit request/response contracts; do not bind arbitrary persistence/domain objects directly to client input.
- Valuable drafts that must survive reconnect/restart belong in an explicit durable/server-owned draft design when required, not only transient component/circuit memory.

## Security

- Client validation is UX, never authoritative validation.
- State-changing cookie/session-backed requests must use the chosen anti-forgery/CSRF protections.
- Encode output by default; do not render untrusted content as raw HTML merely for formatting convenience.
- Do not put reusable provider secrets, DB credentials, or long-lived bearer credentials in browser storage.
- UI hiding/route hiding is not authorization.
- TenantContext must be established/verified server-side.

## API/composition

- REST/task-oriented HTTP remains the pragmatic baseline for interactive business API usage unless a focused decision says otherwise.
- Keep read composition at the narrowest correct boundary; if capabilities are in the same server process, do not create network fan-out merely for composition.
- GraphQL/BFF/edge composition require a demonstrated client/query/latency need and their own security/query-cost/freshness controls.

## DO NOT

- Do not implement offline/PWA business synchronization unless that architecture is explicitly adopted.
- Do not copy business rules into view components for convenience.
- Do not treat Blazor circuit/process memory as durable business truth.
- Do not trust user-supplied TenantId/resource identifiers without authoritative scoping.
- Do not add a second Web-specific persistence model as business authority.

## Validation

For Web behavior, add tests/checks appropriate to the implemented surface for malformed input, authorization denial, cross-tenant access, anti-forgery where applicable, output encoding/XSS-sensitive rendering, reconnect/restart behavior for valuable state, and stable error/outcome mapping.
