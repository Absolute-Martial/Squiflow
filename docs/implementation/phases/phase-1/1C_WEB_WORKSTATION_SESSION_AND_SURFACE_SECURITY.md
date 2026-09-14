# Phase 1C — Web/Workstation Session and Surface Security

**Gate-quality owner:** `docs/implementation/PHASE_GATE_PRODUCTION_HONESTY.md`  
**Evidence/permanence owner:** `docs/implementation/PHASE_GATE_EVIDENCE_REGRESSION_AND_TRANSITIONS.md`

## Production intent

After 1C passes, each declared Web/Workstation authenticated surface has tested session/lifecycle/browser/client failure semantics: mutations cannot bypass the required browser/session controls, transient presentation/session loss does not masquerade as durable business loss/success, and users receive an honest state when identity/session/network conditions change mid-operation.

## Web

Close the current Blazor session/render topology enough to define where valuable state lives and how reconnect/restart behaves.

For cookie/server-backed Web sessions establish as applicable:

- Secure/HttpOnly/SameSite policy;
- antiforgery/CSRF for mutations;
- output encoding by default;
- first CSP/security-header baseline for the implemented surface;
- explicit DTO/field allow-lists;
- stable Problem Details without secret/provider leakage;
- logout/session-expiry behavior that invalidates or limits the selected application session as designed;
- durable drafts outside transient circuit/component memory whenever losing them would violate the declared product behavior.

Do not call a mutation surface production-honest before the applicable CSRF/session/output/input controls are active.

## Workstation

Workstation session/account state must remain separate from durable local business state.

If Phase-2 local durability has not yet been introduced, local durable-operation cases are `NOT_INTRODUCED`; do not invent fake persistence just to test them.

Once local durable/provisional operations exist, define and test as applicable:

- sign-out while a local operation is being entered but not committed;
- sign-out after local durable commit but before remote admission;
- expired provider/application session discovered before a protected local proposal;
- expired session discovered while pending local work exists;
- network loss after local acknowledgement but before any server confirmation;
- account switch/relogin with pending local state;
- revoked device/session detected on next authoritative interaction.

The test must state what committed, what did not, what remains pending, what authority is absent, and what the user is shown.

## Continuing development

Web/Workstation capability pages may continue to grow. A screen can be added before every backend feature exists, but it must not fake authoritative success or hide an unavailable dependency.

## Permanent security evidence

For implemented surfaces, regression checks cover:

- stored/reflected/client-side XSS paths relevant to accepted fields/rendering;
- CSRF on cookie-authenticated mutation;
- excessive-field/mass-assignment attempts;
- unsafe error/detail disclosure;
- bad/expired session and callback state;
- secrets/tokens/cookies in logs/client artifacts;
- circuit/server restart during valuable state where applicable;
- sign-out/expiry/network interruption outcomes for Workstation operations that actually exist.

Cheap browser/API/session tests run `PER_MR`. Full browser/provider/session-restart paths run recurring/pre-release integration tests when the property cannot be proven in-process.

## Transitional security contract

Between 1A/1B completion and full 1C qualification, a surface may expose only operations whose session/surface controls are already individually production-honest.

Examples:

```text
cookie mutation without qualified CSRF protection
→ FORBIDDEN / disabled

authenticated read with qualified authorization/output encoding
→ may be allowed if its own session semantics are already proven

valuable draft stored only in transient Blazor circuit memory
→ may not be represented as durable/autosaved
```

This prevents `we will finish session security later` from becoming a permanent reachable state.

## Exit gate

1C passes only when the declared session/surface guarantees are demonstrated by named behavioral tests and protected by permanent/recurring regression checks. Documentation of semantics without executable/operational evidence is insufficient. `BLOCKED = none`.
