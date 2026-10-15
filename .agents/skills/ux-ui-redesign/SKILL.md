---
name: ux-ui-redesign
description: "Improve a requested existing UI while preserving working journeys, business authority and the project stack. Audit first, make focused changes and verify the affected behavior."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Redesign & Audit


Audit-first redesign that preserves behavior.

## Steps
1. Read `${UX_UI_KIT}/workflows/redesign-audit.md` (the full Scan → Diagnose → Direct → Apply → Verify sequence + Output Completeness).
2. **Scan** the codebase: framework, styling method (→ route to the matching `frameworks/` adapter), token reality, components, what must be preserved.
3. **Diagnose** with the Banned Defaults checklist (`${UX_UI_KIT}/taste/design-taste.md`) + the review rubric (`$ux-ui-design-review` skill). Produce a prioritized findings table.
4. **Direct**: choose an archetype/system (`$ux-ui-apply-aesthetic` skill) that fits the brand.
5. **Apply** in order — tokens first, then typography/spacing, then component states, then motion — without changing routes/data/markup semantics (except a11y fixes).
6. **Verify**: re-run `design-review` + `a11y-audit`; smoke-test every previously working flow; dark mode + responsive spot-check. Run `${UX_UI_KIT}/scripts/validate_contrast.py` on the token source and `${UX_UI_KIT}/scripts/lint_hardcodes.py` over the changed code.

## Guardrails
Never sacrifice a working feature for aesthetics. Never ship a brand color that fails contrast. Never remove existing accessibility affordances. Deliver complete files — no placeholders.

## Single-theme consistency (critical for multi-page apps)
Consolidate to ONE shared token theme and make **every page** consume it — a redesign that leaves different pages on different palettes has failed. Replace per-page/ad-hoc colors with semantic tokens; verify with `${UX_UI_KIT}/scripts/lint_hardcodes.py` that no page reintroduces off-theme values. Theme switches must come from the single token source, not page edits (`${UX_UI_KIT}/rules/tokens-and-color.md` → Single-Theme Consistency).
