---
name: ux-ui-migrate-design-system
description: "Map a project design system to or from a requested external system using semantic roles, preserving accessible states and the current host architecture."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Migrate / Interop Design System


Bridge to or from external design systems via a role-based crosswalk.

## Steps
1. Read `${UX_UI_KIT}/design-systems/interop-protocol.md` (Crosswalk Method, the three directions, headless-vs-styled guidance, verification).
2. Use the curated tables in `${UX_UI_KIT}/design-systems/crosswalk.md` for Material 3 / Apple HIG / Fluent 2 / Carbon / shadcn/ui / Radix. For others, derive a mapping with the Crosswalk Method (map by role/intent across 6 axes: color roles, type scale, spacing unit, radius, elevation, motion).
3. Choose the direction:
   - **FROM** external → our tokens (adopt their look): re-point `semantic.*`.
   - **TO** external stack (our components on their foundation): theme their primitives with our tokens.
   - **Migrate**: Audit → Map → Bridge (alias layer) → Verify, screen by screen.
4. **Verify** every mapped color pair for contrast (`${UX_UI_KIT}/scripts/contrast.py` / `a11y-audit`); confirm all 8 states + dark mode survive the mapping.

## Output
A crosswalk table (our token → their token → value note), a bridge plan if migrating, and verified token overrides. Render via `design-code`.
