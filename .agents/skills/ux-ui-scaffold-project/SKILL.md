---
name: ux-ui-scaffold-project
description: "Create a requested new UI project with Codex AGENTS.md and skill conventions in the user-selected framework. Do not use to replace an existing application."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Scaffold a requested Codex UI project

Use this only when the user requests a new project. Work in the named directory,
preserve its existing files and choose the user's actual framework. A design kit
does not justify replacing an established Blazor or Avalonia application.

Create the smallest working project and guidance needed for the requested
journey. Put repository instructions in AGENTS.md and reusable Codex workflows in
.agents/skills/<name>/SKILL.md. Keep detailed references outside AGENTS.md.
Use agents/openai.yaml only for supported UI metadata/invocation policy.

Reuse the bundled token/component references where useful, placing generated
assets in the new project. Do not copy a Claude directory, settings, hooks,
commands, local-preference template or MCP credentials into the target. Configure
external tools only within the user's actual scope. Verify the real new runtime
and requested behavior; report any unrun platform checks.
