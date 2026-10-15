---
name: ux-ui-critique
description: "Review the rendered UI and actual interactions for usability, hierarchy, accessibility and craft, with specific evidence per finding. Independent review requires a real authorized separate reviewer."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Review the rendered work

Resolve the target from the user's request. If missing, ask for the target while
continuing any independent inspection. Read the shared doctrine and
`${UX_UI_KIT}/review/design-critic.md` for evidence and verdict structure.

Render the actual output, inspect meaningful desktop/mobile or native scaling
states, and exercise its controls. Review hierarchy, useful empty/error states,
layout, copy and feedback against actual state changes. Cite an element, file,
screenshot or measured result for each finding. Distinguish confirmed defects
from open questions and evidence gaps; a passing automated gate does not prove
aesthetic quality.

Use a separate reviewer only when delegation is available and authorized. Pass
the target and resolved kit location, not the maker's intended verdict. Otherwise
perform and label a current-context review. This skill does not create a fork or
background agent. Review-only requests remain read-only. For an authorized build,
fix material in-scope findings and recheck the affected behavior.
