---
name: ux-ui-design-review
description: "Review an existing screen or component for task completion, usability, consistency, accessibility and visual hierarchy with prioritized evidence-backed findings."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Design Review


Run a structured, scored review.

## Steps
1. Read `${UX_UI_KIT}/workflows/design-review.md` (rubric, scoring guide, Nielsen heuristics, process).
2. Gather context: the screen(s)/flow, target users, platform, constraints.
3. Score the 6 dimensions (Visual Hierarchy 20%, Consistency 20%, Accessibility 20%, Usability 20%, Responsiveness 10%, Performance 10%); compute the weighted overall.
4. Run the accessibility lens with `${UX_UI_KIT}/accessibility/wcag-checklist.md`; use `${UX_UI_KIT}/scripts/contrast.py` for any color-pair doubts.
5. Check against the anti-slop tells in `${UX_UI_KIT}/taste/design-taste.md` (Banned Defaults checklist).
6. Apply Nielsen's 10 heuristics; flag violations by number.

## Output
- The 6-dimension scored table + overall score.
- A prioritized findings table: # · Severity (Critical → Major → Minor → Enhancement) · Finding · Recommendation.
- Concrete, token-referenced fixes.
