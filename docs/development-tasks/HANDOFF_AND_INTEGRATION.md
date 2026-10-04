# Handoff and integration

A task is assigned with its entire file, shared rules and a bounded source baseline. Sending only the short prompt loses the contracts and is insufficient.

## Copyable single-task wrapper

```text
Execute only <TASK-ID> using the attached complete task file.
Read the attached AGENT_RULES.md and HANDOFF_AND_INTEGRATION.md.
Baseline Git SHA: <full SHA>; baseline source ZIP SHA-256: <digest>.
The attached dirty-file manifest records incoming changes to preserve.
Use the task's declared model, dependencies and exact write scope.
Inspect dependency handoffs and current canonical owners before editing.
Close only explicitly assigned decisions with owner acceptance.
Implement/verify the outcome with focused tests and static security review.
Run the exact task checks and normal repository gate when required.
List exact checks unavailable in this environment as evidence pending.
Return changed-file list, source-only ZIP, hashes, evidence and blockers.
Do not commit/push, overwrite incoming work or broaden scope.
```

## Baseline package

The integrator records full `git rev-parse HEAD`, toolchain/dependency versions and a dirty-file manifest containing path, tracked/untracked state and SHA-256 of every included incoming file. The manifest includes modified tracked files and deliberate untracked source. A Git SHA alone does not identify a dirty source tree. Record agreed task dependencies and their accepted evidence separately.

Create a source ZIP from an explicit reviewed allowlist; exclude `.git`, `bin`, `obj`, caches, build outputs, runtime volumes, credentials, keys, tokens, certificates with private material, secret configuration and unrelated files. Include necessary public source/configuration/templates and safe evidence only. Preserve incoming Admin request-budget changes and unrelated `package-lock.json`; exclude an unrelated file from an agent package when unneeded, never remove it from the original workspace. Verify extraction and hashes before assignment. Do not supply production credentials to task agents.

## Returned package and review

Return the baseline SHA/dirty-manifest identifier, task ID, exact changed-file list (including deletions), source-only ZIP and SHA-256, summary of contract changes, static security findings, commands with inspected outcomes, unrun checks, and known blockers. Evidence includes actual tool versions and relevant provider/browser/deployment configuration without secret values. A remote CI result is claimed only from its actual inspected run.

The integrator extracts into an isolated review copy and compares against the supplied dirty baseline. Accept only assigned paths, review migrations/authorization/host composition and focused owners, verify compatibility and integration dependencies, then apply reviewed edits without overwriting incoming work. Shared-file conflicts require coordinated review; no agent may resolve them by replacing the other agent's file wholesale. No one-wide commit combines unreviewed parallel branches. Commit/push belongs only to the integrator when the user explicitly authorizes it.

## Verification ownership

Task evidence can be partial while environment checks remain unavailable. The receipt says `evidence pending` and names exact missing SDK/Docker/provider/browser/target checks; it never says production-ready. Do not uninstall validation, skip suites, force serialized tests or substitute unit mocks for real-boundary qualification. Run one normal full gate after integration with no competing shared-tree build, then independently build/publish each introduced host under [independent host rules](../implementation/INDEPENDENT_HOST_BUILDS.md). Gate qualification needs owner acceptance of the selected release cases and every introduced material responsibility trustworthy. Passing the catalog validator qualifies the assignment package only.
