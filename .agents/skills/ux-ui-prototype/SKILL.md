---
name: ux-ui-prototype
description: "Create a requested wireframe, prototype, user flow or usability-test plan at the fidelity the task needs, with explicit assumptions and a validation plan."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Prototype & Research


Guide work through the right fidelity level with validation.

## Steps
1. Read `${UX_UI_KIT}/workflows/prototyping.md` (5-level fidelity ladder, journey mapping template, usability-testing script, sample data).
2. Identify the current need and pick the **lowest** fidelity that answers it — never skip levels:
   - Content-first (info needs) → Wireframe (layout/nav) → Low-fi (task completion) → High-fi (visual/a11y) → Code (feasibility/perf).
3. For flows: produce a user-journey map with decision points, error paths, and edge cases.
4. For validation: define the usability test (tasks, success criteria, 5-user rule) using the script.
5. High-fi/code steps pull tokens (`${UX_UI_KIT}/tokens/*`), components (`${UX_UI_KIT}/components/*`), taste (`${UX_UI_KIT}/taste/*`), and a11y (`${UX_UI_KIT}/accessibility/*`).

## Output
The artifact at the chosen fidelity + an explicit "what we validate next" plan.

## Verification (before declaring done)
- The fidelity matches the question being answered — no level skipped.
- Flows include decision points, **error paths, and edge cases** (empty/loading/overflow), not just the happy path.
- A concrete validation step is named (tasks + success criteria), not "test later".
- High-fi/code artifacts pass the same token + a11y bar as `design-code` (no hardcoded values, contrast, states).
