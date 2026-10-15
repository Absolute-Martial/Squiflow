---
name: ux-ui-token-build
description: "Build requested platform theme artifacts from project tokens, preserving semantic roles and validating generated CSS or native resource mappings."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Token Build


Turn the DTCG token source of truth into platform-ready outputs. Tokens are authored once; every platform output is generated.

## Steps
1. Read `${UX_UI_KIT}/workflows/token-build.md` (architecture, tool options, resolution rules, output targets, CI).
2. Pick the tool: **Style Dictionary** (multi-platform, the default), **Tokens Studio** (Figma-owned tokens — pairs with `figma-integration`), W3C DTCG export, or a small custom script (model on `${UX_UI_KIT}/scripts/validate_tokens.py`).
3. Honor the resolution rules: resolve aliases to final values per platform; expose semantic + component tokens (primitives stay internal); emit base + dark/brand/density overrides (`${UX_UI_KIT}/tokens/theming.json`) as deltas only; format by `$type`.
4. Generate the requested target(s): CSS `:root` vars, Tailwind v4 `@theme`, typed JS/TS, iOS Asset Catalog + `Color.DS`/`Spacing`, Android `colors.xml`/Compose theme.
5. Wire CI: on `${UX_UI_KIT}/tokens/*.json` change, run `${UX_UI_KIT}/scripts/validate_tokens.py`, regenerate, fail if committed artifacts are stale; gate colors with `${UX_UI_KIT}/scripts/contrast.py`.

## Verification (definition of done)
- `python3 ${UX_UI_KIT}/scripts/validate_tokens.py <application-token-file-or-directory>` passes (no unresolved aliases).
- Regenerating produces no diff vs. committed artifacts.
- Dark/brand/density outputs contain only deltas, not full duplicates.
