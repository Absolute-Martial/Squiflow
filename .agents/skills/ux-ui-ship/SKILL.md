---
name: ux-ui-ship
description: "Prepare requested UI release evidence and release notes using the consuming project version and verification rules. Publishing and tagging require existing user authorization."
---

## Codex integration

Read [the shared UI doctrine](../ux-ui-design-doctrine/SKILL.md) before using this
workflow for scope, resource resolution, invocation policy and verification.
Resolve the task-local `UX_UI_KIT` variable as described there; Codex does not
create it. Keep the bundled kit read-only and generate changes in the application.
Use explicit application inputs for checks, never library defaults as product
evidence. Apply the actual host adapter and project rules before framework examples.

# Prepare UI release evidence

Use the actual repository's release/version policy and verification contract.
Verify the implemented journey and collect actual UI evidence. Summarize the
source revision, changes, checks, pending target/provider evidence and known scope
limits for review. Draft requested release notes using the project's convention.
Do not change a locked product version to match the design kit's version.

Publishing, tagging and external messages require authorization from the user or
an applicable explicitly invoked workflow. Existing authorization persists; do
not request it again. Use the repository's version-control tools, including
GitButler here. This workflow does not imply a new release, tag or deployment.
