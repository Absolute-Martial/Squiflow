---
name: ux-ui-brandkit
description: "Create a requested brand token foundation with primitive, semantic and component roles, typography, spacing and validated color pairs. Keep generated files in the consuming project."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Brand Kit


Stand up the *foundation* (one token system everything renders from) before any screen. Get this right and every page stays consistent and themeable from one place.

## Steps
1. **Brief Inference (mandatory)** — name the industry, audience, the one mood adjective, and motion depth (`${UX_UI_KIT}/taste/design-taste.md` → Brief Inference). Pick an anchoring archetype from `${UX_UI_KIT}/taste/aesthetic-systems.md`.
2. **Primitives** — generate the brand color ramp in **OKLCH** (11 shades, consistent chroma) + a neutral ramp; verify the 500 shade ≥ 4.5:1 on white (text) and 600 ≥ 3:1 (UI) per `${UX_UI_KIT}/rules/tokens-and-color.md` → Color Generation.
3. **Semantic layer** — map roles to primitives: `action.primary`/`-hover`/`destructive`, `text.{primary,secondary,on-action,link}`, `surface.{page,card,raised}`, `border.{default,strong}`, `feedback.{success,warning,error,info}` — and the **dark** overrides (designed, not inverted).
4. **Scales** — Major Third type scale + composite text styles, 4px spacing scale, radius tiers, elevation, and `${UX_UI_KIT}/tokens/motion.json`-style durations/easings.
5. **Emit** DTCG files in the application-owned token directory (3-tier) + a single `theme.css` (the one shared source, `[data-theme="dark"]` overrides). Optionally feed the token-build pipeline (`$ux-ui-token-build` skill) for other platforms.

## Verification (definition of done)
- `python3 ${UX_UI_KIT}/scripts/validate_tokens.py <application-token-file-or-directory>` — valid JSON, all aliases resolve.
- `python3 ${UX_UI_KIT}/scripts/validate_contrast.py <application-colors-file>` — required text/action/border pairs pass WCAG AA in **light AND dark**; `border.strong` ≥ 3:1.
- `python3 ${UX_UI_KIT}/scripts/validate_theme_refs.py` — every component `var(--…)` resolves to the theme.
- One theme, no per-page palettes; destructive = danger token (not primary); zero hardcoded values.

> Output is a verified token foundation — the measurable part is provable (run the consuming project checks). Brand "feel" still benefits from a human review against `${UX_UI_KIT}/taste/design-taste.md`.
