# Phase 5C — Dynamic Forms and Safe Publication

## Scope

Implement one bounded versioned form lifecycle for a real tenant journey.

Allowed variability may include reviewed field definitions, required/optional rules, labels/options, bounded validation and mapping to capability-owned application contracts.

The existence of this foundation does not require every capability to adopt dynamic forms; fixed strongly typed forms remain valid when variation is not a real product requirement.

## Security

Do not permit arbitrary HTML/script execution. Treat labels/help text/templates/options as untrusted content and preserve normal output encoding/sanitization rules.

## Publication lifecycle

```text
draft
→ validate
→ preview/diff
→ authorized publish
→ immutable published revision
→ activate for new work according to policy
```

Failed publication must not replace the last valid revision.

## Persistence/history

If submitted form data becomes historical business truth, retain the definition/version needed to interpret it. Do not rely on current form shape to reinterpret old records.

## Exit gate

One real form can evolve without corrupting active/historical instances and without becoming an arbitrary application builder.