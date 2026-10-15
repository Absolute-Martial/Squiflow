---
name: ux-ui-figma-integration
description: "Map Figma variables and components to project tokens and verify design/code parity. Use connected Figma tools only when available and within the authorized read/write scope."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Figma Integration


Bridge design (Figma) and code (this repo) in both directions. The token JSON stays the source of truth; Figma Variables mirror it.

## Steps
1. Read `${UX_UI_KIT}/workflows/figma-integration.md` (token↔Variable mapping, sync directions, MCP usage, parity).
2. Map the 3-tier hierarchy → three Figma collections (Primitives → Semantic → Component) with aliasing; dark/brand/density → Figma **Modes** (`${UX_UI_KIT}/tokens/theming.json`).
3. Choose ONE authoritative sync direction (code→Figma publish, or Figma→code extract via Tokens Studio / Variables REST API). The other side is generated — never hand-edit both.
4. **If a Figma MCP server is connected:** prefer its tools/skills (load its mandatory prerequisite skill first). Use it to read frames/variables/screenshots into code, build Variables/components from our tokens, or wire Code Connect to `${UX_UI_KIT}/components/*`.
5. Check component parity: Figma variants/properties must cover our variants + sizes + the 8 states; flag gaps.

## Verification (definition of done)
- Every Figma Variable resolves to a token in `${UX_UI_KIT}/tokens/*.json` (no orphan hex in designs).
- One authoritative direction; the generated side has zero hand edits.
- Variant sets cover all 8 states; Code Connect points to the right `${UX_UI_KIT}/components/*` file.
- After any token import: `python3 ${UX_UI_KIT}/scripts/validate_tokens.py <application-token-file-or-directory>` passes.
