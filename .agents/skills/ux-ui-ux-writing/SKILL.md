---
name: ux-ui-ux-writing
description: "Write or review interface copy, including actions, labels, empty/error/loading states and confirmations. Keep messages clear, useful, truthful and suitable for localization."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: UX Writing


Produce or critique interface copy in the project's voice.

## Steps
1. Read `${UX_UI_KIT}/content/voice-tone.md` (voice principles, tone spectrum, formulas, microcopy patterns, inclusive language, pre-ship checklist).
2. Match tone to the user's emotional state (onboarding/success/routine/error/destructive). Higher stress → plainer language.
3. Apply the formulas:
   - Buttons: frontload the verb, name the outcome.
   - **Confirmation dialogs:** the confirm button **restates the action and object** — "Delete account", not "Delete"/"OK"/"Yes"/"Confirm". The title asks ("Delete account?"), the button answers in matching words. Cancel stays "Cancel". For irreversible/high-stakes actions, require a **type-to-confirm** step (WCAG 3.3.4/3.3.6).
   - Errors: what happened → why → how to fix (no dead ends, no codes/stack traces).
   - Empty states: value → first action.
4. Enforce mechanics: sentence case, no ALL CAPS, numerals, no blame on the user, labels (not placeholders), no directional/color-only instructions.
5. Run the 10-item pre-ship copy checklist.

## Output
Final copy (or a redline review) that reads naturally aloud and passes the checklist. Keep within any character limits for tight UI.

## Verification (mandatory before declaring done)
Run every line through the 10-item pre-ship checklist in `${UX_UI_KIT}/content/voice-tone.md` — do not skip it:
- Reads naturally **aloud**; frontloaded verb on actions; no jargon/blame/dead-ends.
- Errors follow what→why→how; empty states give value→action; no bare "No data"/"Error".
- Mechanics: sentence case, numerals, labels (not placeholders), no color/direction-only cues, inclusive language.
- Within character limits; translatable (no concatenation — see `${UX_UI_KIT}/accessibility/i18n-rtl.md`).
