---
name: ux-ui-design-qa
description: "Set up or run UI quality checks against actual project screens and components, including state coverage, accessibility and visual regression. Select checks compatible with the host."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Design QA


Stand up the automated + manual gates that stop quality from regressing.

## Steps
1. Read `${UX_UI_KIT}/workflows/design-qa.md` (the QA pyramid: token/lint gates → automated a11y → visual regression → manual a11y).
2. Wire the **fast gates** first (every commit/PR): `python3 ${UX_UI_KIT}/scripts/validate_tokens.py <application-token-file-or-directory>`, `python3 ${UX_UI_KIT}/scripts/validate_contrast.py <application-colors-file>` (batch WCAG over the token pairs), and `python3 ${UX_UI_KIT}/scripts/lint_hardcodes.py <src>` (no raw hex/px/timing in component code). The repo's `.github/workflows/ci.yml` runs these.
3. Add **automated a11y** (axe-core / Pa11y) over each component's states (error/loading/disabled/expanded/selected), zero serious/critical to merge. Run the **real-render contrast gate** `node ${UX_UI_KIT}/scripts/measure_render.mjs <file.html>` (and `--dark`) — it opens the page in headless Chromium, disables transitions, and measures the true computed-style + alpha-composited contrast of every text element (catches what static token checks miss).
4. Add **visual regression** snapshots across variants × sizes × states × light/dark + key breakpoints + RTL; freeze animations + deterministic data.
5. Sign off the **manual a11y** checklist per release (keyboard, screen reader, 400% reflow, 200% text-spacing, reduced-motion, forced-colors — `${UX_UI_KIT}/accessibility/*`).

## Verification (definition of done)
- An unreviewed PR cannot introduce an unresolved token alias, a contrast failure, a raw hex/px, or an axe violation — a gate blocks each.
- Snapshots cover dark mode + RTL + the semantic-changing states, not just the happy path.
- Manual a11y checklist signed off for the release.
