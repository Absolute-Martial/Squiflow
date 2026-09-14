# Phase 1C — Web/Workstation Session and Surface Security

## Web

Close the current Blazor session/render topology enough to define where valuable state lives and how reconnect/restart behaves.

For cookie/server-backed Web sessions establish as applicable:

- Secure/HttpOnly/SameSite policy;
- antiforgery/CSRF for mutations;
- output encoding by default;
- first CSP/security-header baseline;
- explicit DTO/field allow-lists;
- stable Problem Details without secret/provider leakage.

Durable drafts cannot exist only in circuit/component memory when losing them would violate a real product requirement.

## Workstation

Workstation session/account state must remain separate from durable local business state. Sign-out, expired identity-provider session, or network outage must not silently erase already committed local data when later phases introduce it.

## Continuing development

Web/Workstation capability pages may continue to grow. A screen can be added before every backend feature exists, but it must not fake authoritative success or hide an unavailable dependency.

## Security tests

Exercise stored/reflected/client-side XSS paths relevant to implemented fields, CSRF on cookie-authenticated mutation, excessive-field/mass-assignment attempts, bad session/callback state, and accidental secrets in logs/client artifacts.

## Exit gate

The current Web and Workstation identity/session surfaces have explicit security and failure semantics and do not rely on transient presentation memory as business authority.