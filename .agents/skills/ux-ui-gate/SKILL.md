---
name: ux-ui-gate
description: "Run the consuming project checks and applicable UI gates against actual changed targets; report measured outcomes and unrun checks."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Verify the actual UI change

Identify the target and its repository verification contract. Run those checks
and select compatible UI checks against the changed screen/component/harness.
Never run the upstream kit's self-test as application qualification.

For compatible local HTML, useful bundled checks include:

```sh
DS_REQUIRE_BROWSER=1 node "${UX_UI_KIT}/scripts/verify_states.mjs" <actual-harness>
DS_REQUIRE_BROWSER=1 node "${UX_UI_KIT}/scripts/axe_audit.mjs" <actual-harness>
DS_REQUIRE_BROWSER=1 node "${UX_UI_KIT}/scripts/verify_responsive.mjs" <actual-harness> --scale=1.25
DS_REQUIRE_BROWSER=1 node "${UX_UI_KIT}/scripts/verify_keyboard.mjs" <actual-harness>
```

Use explicit application token paths with token validators. Inspect script usage
and dependencies before execution. HTML checks do not qualify native controls.
Render and inspect the real output, exercise controls and report actual outcomes,
failures, skips and unrun checks. Fix in-scope failures without suppressing checks.
