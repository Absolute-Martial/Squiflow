# Phase 8B — API/Browser Security and Admission Limits

## Qualify implemented surfaces

Exercise real paths for:

- authentication/session boundary;
- TenantContext/resource/field authorization;
- CSRF/antiforgery where cookie-backed;
- stored/reflected/client-side XSS/output encoding;
- mass assignment/excessive field exposure;
- SQL/filter/sort/identifier injection;
- SSRF/redirect/DNS/private-network restrictions;
- malicious file/path/type/size/decompression/template input;
- stable safe error disclosure;
- custom-host/forwarded-header confusion where used.

These controls should already have been introduced with the relevant surface. Phase 8 proves them together under realistic attack/failure combinations; it does not excuse `security later` in earlier phases.

## Rate/admission controls

Use layered, workload-specific limits rather than one arbitrary global number. Consider login/recovery, account/device, tenant, route/work class, expensive provider work and Admin operations separately.

`429`/`Retry-After` and client backoff must be explicit where throttling exists.

## Hard resource/usage limits

Application-level durable limits remain separate from edge/rate limiting and telemetry. Current sensitive authority cannot be delegated to gateway/cache state.

## Exit gate

Security controls are proven through actual application paths, not declared complete because a scanner ran.