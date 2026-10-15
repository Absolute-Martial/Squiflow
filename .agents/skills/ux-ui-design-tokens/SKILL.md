---
name: ux-ui-design-tokens
description: "Create, extend or audit design tokens, semantic color roles, typography, spacing and theme mappings. Reuse the project token system and validate the actual changed token files."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Design Tokens


Produce and maintain DTCG (`$type`/`$value`) tokens following the project's 3-tier system.

## Steps
1. Read `${UX_UI_KIT}/rules/tokens-and-color.md` → "Token System" + "Color Guidelines" and `${UX_UI_KIT}/rules/typography-and-spacing.md` for the rules (4px base, Major Third scale, OKLCH palette generation, dark-mode-at-semantic-layer).
2. Read the relevant existing files in `tokens/` to match structure: `colors.json`, `typography.json`, `spacing.json`, `shadows.json`, `borders.json`, `breakpoints.json`, `motion.json`, `gradients.json`, `opacity.json`, `blur.json`, `sizing.json`, `states.json`, `theming.json`.
3. Generate/extend tokens:
   - Primitives = raw values (never used directly). Semantic = purpose aliases. Component = component-scoped.
   - New palettes: generate 11 OKLCH shades; verify 500 ≥ 4.5:1 on white (text), 600 ≥ 3:1 (UI) using the `$ux-ui-a11y-audit` skill / `${UX_UI_KIT}/scripts/contrast.py`.
   - Multi-brand/density → `theming.json`.
4. **Validate**: run `python3 ${UX_UI_KIT}/scripts/validate_tokens.py <application-token-file-or-directory>` (JSON validity + alias resolution).

## Output
DTCG JSON. Preserve `$description` on every token. Reference, never hardcode.
