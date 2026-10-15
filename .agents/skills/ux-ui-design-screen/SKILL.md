---
name: ux-ui-design-screen
description: "Design or implement a complete requested screen or UI journey using the existing host, brand and component system. Use for new screens; route requested redesigns, reference-image work and dense dashboards to their specific workflows."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Design or build a screen

1. Identify the real user, task, audience, existing brand, host and accepted scope.
   State material assumptions briefly. Ask only for missing decisions that affect
   the result; continue independent work while awaiting optional clarification.
2. Read the shared doctrine, relevant component/accessibility rules and host
   adapter. Reuse established layout and controls before adding new primitives.
3. Choose a clear visual hierarchy, primary action and section sequence. For dense
   data use `$ux-ui-data-dashboard`; for a requested redesign use
   `$ux-ui-redesign`; for a supplied visual use `$ux-ui-image-to-code`.
4. Implement the complete requested journey in the selected framework. Cover
   applicable loading, empty, error, denial, selected and cancellation states.
5. Run the project's relevant checks and compatible UI gates against the actual
   implementation. Render, inspect and interact at appropriate viewport/scaling
   sizes. Use the existing theme; add themes only within the requested scope.
6. Review the output for task completion, hierarchy, consistency, accessibility
   and truthful feedback. Use an independent critic only when available and
   authorized. Report measured results and remaining evidence limits.
