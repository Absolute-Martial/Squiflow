---
name: ux-ui-image-to-code
description: "Implement a UI from a supplied screenshot or mockup in the requested framework, deriving semantic tokens and layout, then comparing the real rendered result with the reference."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Image to Code


Reconstruct a design from a visual reference as a real design system, not a one-off copy. Match the *system* (color/type/spacing language), never lift copyrighted imagery or brand assets.

## Steps
1. **Read the reference like a designer.** Infer and write down:
   - **Palette** — 1 dominant surface family, text colors, 1 primary action + at most 1 accent (sample the hues; don't guess random hex).
   - **Type** — family feel (geometric/grotesk/serif), the scale jumps, display vs. body contrast, weights.
   - **Spacing & density** — base unit, section rhythm, card padding; airy vs. compact.
   - **Radius & depth** — radius language (sharp/soft/pill), shadow vs. hairline separation.
   - **Layout archetype + sequence** — bounded hero / asymmetric split / dense bento / editorial stack (`${UX_UI_KIT}/taste/design-taste.md` → Variance Mandate).
2. **Anchor to a known system** if it's close — browse `${UX_UI_KIT}/taste/aesthetic-systems.md` / `python3 ${UX_UI_KIT}/scripts/design_systems.py search <term>` and adopt that recipe to stabilize decisions.
3. **Build the token theme** from the inferred values → 3-tier DTCG (`$ux-ui-design-tokens` skill); generate a single `theme.css`. Verify every color pair with `${UX_UI_KIT}/scripts/contrast.py` / `${UX_UI_KIT}/scripts/validate_contrast.py` (light + dark) — a sampled brand color that fails AA gets adjusted; taste never overrides POUR.
4. **Rebuild layout + components** token-driven via `${UX_UI_KIT}/frameworks/adapter-protocol.md` + `${UX_UI_KIT}/components/*`: one shared primitive layer, all 8 states, a11y wired, no emoji (lucide), single theme. Apply taste (`design-taste.md`) so it doesn't regress to generic.
5. **Verify against the reference** — render and screenshot it, compare side-by-side to the reference; run `node ${UX_UI_KIT}/scripts/measure_render.mjs`, `lint_hardcodes.py`, `taste_audit.mjs`, and the consuming project checks.

## Verification (definition of done)
- the consuming project checks pass for the implemented scope, with actual token and rendered-state evidence.
- The rebuilt UI uses ONE inferred token theme — no per-section palettes.
- A screenshot of the result visibly matches the reference's design language.

> Honest limit: this matches the design **system**, not a pixel-perfect copy. Do not reproduce the reference's photographs, logos, or copyrighted copy — substitute your own or generic placeholders.
