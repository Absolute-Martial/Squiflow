---
name: ux-ui-apply-aesthetic
description: "Apply a requested visual direction or reference design system through semantic tokens while preserving accessibility, the existing brand and project stack."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Apply Aesthetic


Choose and apply a design direction without breaking accessibility.

## Steps
1. **Brief Inference first (mandatory)** — before any tokens, name it: industry/domain, audience & tone, the one mood adjective the result must earn, motion depth, and the layout-family sequence (`${UX_UI_KIT}/taste/design-taste.md` → Brief Inference + Variance Mandate). Generating before deciding = slop.
2. Pick a direction in `${UX_UI_KIT}/taste/aesthetic-systems.md`:
   - An **archetype** (recipe mapped to our tokens), or
   - A **named library system** — browse with `python3 ${UX_UI_KIT}/scripts/design_systems.py list` (or `search <term>` / `show <name>`); specs live in `${UX_UI_KIT}/design-systems/library/<name>/DESIGN.md`.
3. Apply the **Library Contract** (in `aesthetic-systems.md`): re-point `semantic.*` tokens to the chosen system's color roles; map typography/spacing/radius/shadow/motion to `${UX_UI_KIT}/tokens/*.json`.
4. **Verify contrast** of every mapped color pair (`${UX_UI_KIT}/scripts/contrast.py` / `a11y-audit`). A brand value that fails must be adjusted — taste never overrides POUR.
5. Add motion per `${UX_UI_KIT}/taste/motion-choreography.md`; run the pre-flight aesthetic check in `design-taste.md`.

## Output
Updated/overridden semantic tokens + notes on type/space/motion, then render via `design-code`. Confirm the result passes both the aesthetic check and accessibility.
