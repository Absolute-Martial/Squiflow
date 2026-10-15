---
name: ux-ui-a11y-audit
description: "Audit a UI for accessibility with criterion-referenced findings and specific fixes. Use for contrast, keyboard, labels, focus, screen-reader or WCAG checks; distinguish measured evidence from unrun checks."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Accessibility Audit


Evaluate against WCAG 2.2 and the project's ARIA patterns.

## Steps
1. Read `${UX_UI_KIT}/accessibility/wcag-checklist.md` (POUR-organized, P0/P1/P2) and `${UX_UI_KIT}/accessibility/aria-patterns.md`.
2. Check the mandatory P0 set per component: keyboard navigable, focus visible (≥3:1), screen-reader name/role/state, contrast (4.5:1 text / 3:1 UI), target size ≥24×24, no color-only signaling.
3. Verify WCAG 2.2 additions: Focus Not Obscured (2.4.11), Target Size (2.5.8), Accessible Authentication (3.3.8).
4. **Contrast — measure, don't eyeball.** For rendered HTML, RUN the real-render gates and report their actual output (the Codex UI doctrine → Verification Protocol): `node ${UX_UI_KIT}/scripts/measure_render.mjs <file> [--dark]` (every text element) AND `node ${UX_UI_KIT}/scripts/verify_states.mjs <file> [--dark]` (every interactive element in default/hover/focus — catches hover-state failures). For loose color pairs, `python3 ${UX_UI_KIT}/scripts/contrast.py "<fg>" "<bg>"`. Never state a ratio you did not measure.
5. Check reduced-motion handling (`${UX_UI_KIT}/taste/motion-choreography.md`).

## Output
A findings table: WCAG criterion (e.g. 1.4.3) · severity (P0/P1/P2) · what fails · specific fix. Confirm passes explicitly. Accessibility may never be traded for aesthetics.
