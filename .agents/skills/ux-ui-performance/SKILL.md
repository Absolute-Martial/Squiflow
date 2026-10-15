---
name: ux-ui-performance
description: "Investigate and improve measured UI responsiveness, layout shift, loading or animation performance in the actual host. Use web performance metrics only for web targets."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Performance


Make the UI fast and stable. Treat performance as an accessibility concern — slow/janky UIs fail low-end devices first.

## Steps
1. Read `${UX_UI_KIT}/workflows/performance.md` (Core Web Vitals targets, loading strategy, layout-shift, animation perf, design-system runtime cost).
2. Diagnose against budgets: **LCP ≤ 2.5s**, **INP ≤ 200ms**, **CLS ≤ 0.1**. Measure on a mid-tier mobile profile (throttle slow 4G + 4× CPU).
3. Loading: render above-fold first (SSR/Server Components), code-split by route + heavy widget, lazy-load below-fold/behind-interaction (Astro islands / Qwik), preload one critical font weight, modern responsive images.
4. Kill layout shift: size skeletons to final dimensions, reserve media space via `aspect-ratio` (`${UX_UI_KIT}/tokens/sizing.json`), never inject content above existing.
5. Animation: only `transform`/`opacity`, `will-change` sparingly, 100–300ms (`${UX_UI_KIT}/tokens/motion.json`), honor `prefers-reduced-motion`. Prefer CSS state styling over JS for low INP; tree-shake/per-component imports.

## Verification (definition of done)
- Lighthouse / field data meets LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1 on mid-tier mobile.
- First interaction stays responsive under slow-4G + 4× CPU throttle.
- Skeletons + media reserve space (zero CLS on load); no non-compositor animation in loops.
