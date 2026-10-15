---
name: ux-ui-governance
description: "Maintain a requested design-system contribution, deprecation or versioning workflow under the consuming project release policy. Do not bump a locked product version."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Skill: Governance


Keep the system consistent as it grows. Apply versioning, contribution, and deprecation rules.

## Steps
1. Read `${UX_UI_KIT}/workflows/governance.md` (SemVer table, contribution workflow, deprecation policy, change comms).
2. Classify the change: **major** (breaking — renamed/removed token or prop, changed anatomy/default), **minor** (additive — new token/component/variant/optional prop), **patch** (fix — contrast/bug/doc/value tweak in tolerance).
3. For a **new** component/token: confirm it serves a real, repeated need (≥ 2 places) before promoting product → candidate → core. Design it to the full quality bar (`${UX_UI_KIT}/rules/components.md` → Component Quality Bar).
4. For a **deprecation**: mark with reason + replacement + removal version; keep working ≥ 1 minor cycle; provide a migration map (`${UX_UI_KIT}/design-systems/crosswalk.md` style); remove only in a major.
5. Wire any new file into `the Codex UI doctrine` (File Reference Map + relevant table/router) and add a changelog entry.

## Verification (definition of done)
- Change has a SemVer level **and** a changelog entry.
- Removals have a deprecation window, a replacement, and a migration table.
- New spec meets the 8-state + a11y + token-mapping bar and is reachable via the router.
- `python3 ${UX_UI_KIT}/scripts/validate_tokens.py <application-token-file-or-directory>` passes; contrast re-checked if colors changed.
