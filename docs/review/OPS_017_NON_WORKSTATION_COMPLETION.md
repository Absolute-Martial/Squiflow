# OPS-017 bounded non-Workstation completion receipt

Date: 2026-10-15. Disposition: source prepared for integrator review;
qualification `BLOCKED` pending actual runs on the integrated source.

## Assignment and baseline

Work was performed only in the new `/tmp/application-quality-completion` copy
created from `/tmp/application-completion-src`. No Git metadata/commit identity
was supplied in that input tree. The input directory changed during the task in
invoice, authorization and related source paths; those differences are outside
this patch and are not returned as edits or deletions. Integrate only the explicit
allowlist below. Do not replace the integrator's tree with this working copy.

Preimage SHA-256 values for the two modified input files (checked against the
provided source directory during handoff):

```text
964eb77a0972bc18f4e8edc4458daf9b79fcd14e933378a0827259c88e136a4c  .github/workflows/verify.yml
347ec10feebb51fb18471af2cf16c62ebcf85c9bd140797d3b51a2fd1b7b4599  docs/implementation/INDEPENDENT_HOST_BUILDS.md
```

The assignment limits writes to verification/workflow scripts, their focused
tests and focused documentation. It holds actual .NET/build/Docker work until
the integrator releases the existing normal gate. No ready signal was received.
The normal `eng/verify.sh`, Workstation, shared root instructions/README/solution,
app/module sources, locks, coverage configuration and existing security/mutation
tooling were not edited. No dependency package or browser was installed locally.

Swarm delegation was attempted through the available agent dispatcher; it
returned `agent thread limit reached`. Implementation and review continued
locally without remote agents or remote CI.

## Changed paths

| Path | Change |
| --- | --- |
| `eng/verify-host-ci.sh` | New bounded orchestration of existing host verification/publication for the five non-Workstation hosts; fresh output, nonempty runtime files, SDK/log evidence and publication checksums |
| `eng/verify-web-browser.sh` | New preflight and bounded execution of the existing real Web/Chromium signed-in guard, required fresh evidence and checksums |
| `eng/test-quality-verification.py` | New standard-library fixture selfchecks for orchestration, failures, stale/missing/empty artifacts and workflow safeguards |
| `.github/workflows/verify.yml` | Route only non-Workstation matrix entries through the new runner, retain logs and run script selfchecks in the existing architecture job |
| `.github/workflows/web-browser.yml` | New pinned, read-only PR workflow for the existing actual Web browser/accessibility guard |
| `docs/implementation/INDEPENDENT_HOST_BUILDS.md` | Focused owner updates for the bounded runners, current aliases, browser evidence, limits and requalification |
| `docs/review/OPS_017_NON_WORKSTATION_COMPLETION.md` | This receipt |

There are no patch deletions. The source-only ZIP contains exactly these paths
relative to the repository root. Shell executable modes are retained.

## Exact executed checks

All commands ran in `/tmp/application-quality-completion`, apart from reading
the existing external tools. They do not launch real dotnet or a browser.

| Check | Inspected result |
| --- | --- |
| `python3 eng/test-quality-verification.py` | Final run: 9 tests passed, 13.876 seconds, no failures or skips; subcases include all five actual manifest mappings, failures at each dotnet stage, each missing/empty runtime file, browser failure propagation and stale/malformed/missing evidence |
| `bash -n eng/verify-host-ci.sh eng/verify-web-browser.sh eng/build-host.sh eng/verify-host.sh eng/audit-dependencies.sh eng/verify.sh eng/mutate-orders.sh` | Passed syntax checks; no script execution |
| `python3 -m py_compile eng/test-quality-verification.py` | Passed; cache output excluded from ZIP |
| `node --check eng/test-signed-in-entry-browser.cjs` and `node --check eng/test-entry-ui.cjs` | Both passed syntax checks; guards unchanged |
| Python 3.12.3 / PyYAML 6.0.1 `BaseLoader` parse of every `.github/workflows/*.yml` | Parsed all four workflows and checked `on`/jobs structure; this is YAML syntax checking, not GitHub execution or actionlint |
| External Node module/package introspection with the supplied Playwright/axe paths and `PLAYWRIGHT_BROWSERS_PATH=/tmp/application-entry-browsers` | Node 24.15.0; Playwright 1.64.0, Apache-2.0; axe-core 4.11.0, MPL-2.0; Chromium revision 1248 executable exists. No browser launch. The installed Playwright CLI source declares `install --with-deps` |
| Standard-library source comparison and ZIP allowlist/hash/extraction validation | Patch limited to the seven named paths; protected files unchanged; archive integrity and extracted source hashes checked before handoff |

Fixture dotnet/browser commands intentionally stand in for process orchestration
only. They do not prove compilation, project restoration, test counts, publication
loadability, accessibility or browser behavior of the actual product. YAML
parsing and pinned-action checks are not security scanner results.

## Claim, evidence, regression and requalification

| Claim / owner | Falsifiable evidence and recurring guard | Requalification trigger | Current state / non-claim |
| --- | --- | --- | --- |
| Each selected host independently verifies/publishes its declared graph / independent-host owner | New runner plus existing manifest/build/verify scripts; fresh CI matrix job per host; owned tests; required runtime artifacts and checksums; script selfchecks | Manifest, project references, SDK/locks, scripts, actions, runner or test ownership changes | `BLOCKED` until real local and CI runs are inspected; no deployment/signing/bit-identical rebuild claim |
| Actual Web browser journeys have recurring evidence / signed-in-entry owner | Existing real Web/HTTPS fixture/Chromium guard and axe audits invoked by the new workflow; wrapper requires final results/screenshots/audits; retains partial failure evidence | Browser/tool/SDK revisions, Razor/rendering/session/CSP contracts, fixture, runner or workflow changes | `BLOCKED` until actual run/CI inspection; local issuer/API is controlled, not live ZITADEL/OpenFGA/PostgreSQL evidence; axe incomplete rules need review |
| Existing normal/coverage/security/mutation gates remain required / existing focused owners | Existing normal gate with Coverlet/ReportGenerator and material-path review, architecture tests, Gitleaks, all-mode locked NuGet audit, CodeQL and scoped Stryker | Existing owners' dependency/authority/material-path/action/triage triggers | Preserved in source; no passed scanner, dependency audit, coverage, mutation or normal gate claimed by this patch |

No new business responsibility, host, deployment profile or dependency/tool family
is introduced. Workstation qualification is outside this completion.

## Static review

The changed verification workflows use pinned 40-character action revisions and
top-level `contents: read`, ordinary `pull_request` events and no production
secrets. Changed host/browser checkouts disable persisted Git credentials.
No PR title/body/branch text is interpolated into shell commands. Host aliases
are allowlisted before output removal and selected through the existing manifest.
The existing Workstation matrix membership and commands remain intact.

The new browser job uses the same externally declared Playwright/axe versions,
outside the product dependency graph, with npm lifecycle scripts disabled. Its
Linux runner/system browser dependencies are test inputs; their remote setup has
not run here. The dedicated upload directory contains synthetic screenshots,
audit/results JSON and logs, with 14-day retention. The fixture's generated
private key/certificates are in a separate temporary directory and are not
uploaded. No HAR, browser storage or provider secrets are collected.

This review found no additional in-scope source defect. It does not establish
that existing tests/logs or dependencies are free of all security issues.

## Integrator checks and remaining decisions

Review and apply only the allowlist, preserving newer input-tree work and incoming
`eng/verify.sh` changes. After the integrator releases the build/Docker hold, run
on the same integrated source with no competing shared-tree build:

```bash
python3 eng/test-quality-verification.py
COLLECT_COVERAGE=1 ./eng/verify.sh
./eng/verify-host-ci.sh core-api
./eng/verify-host-ci.sh admin-api
./eng/verify-host-ci.sh admin-bootstrap
./eng/verify-host-ci.sh db-migrator
./eng/verify-host-ci.sh web
APPLICATION_ENTRY_PLAYWRIGHT_PATH=/tmp/application-entry-browser/node_modules/playwright \
APPLICATION_ENTRY_AXE_PATH=/tmp/application-entry-browser/node_modules/axe-core/axe.min.js \
PLAYWRIGHT_BROWSERS_PATH=/tmp/application-entry-browsers \
./eng/verify-web-browser.sh
./eng/audit-dependencies.sh
./eng/mutate-orders.sh
```

Inspect complete results/counts/failures/skips, all required host publications,
coverage/mutation/audit artifacts, browser screenshots and incomplete axe rules.
Inspect the corresponding actual `Verify`, `Web browser`, `Security Review` and
`CodeQL` CI runs on that source after the integrator publishes it. This task did
not commit, push, dispatch CI, run Gitleaks/CodeQL/Stryker, run NuGet audit or
execute any .NET/product/browser/Docker command.

The integrator/gate owner still decides required-check/branch-protection adoption,
whether existing host/job time budgets suffice on actual CI, and any findings or
time-bounded dependency exceptions under the existing triage policy. Release
platform/profile support, live provider/business journeys, native Workstation,
distribution signing/provenance and broader accessibility conformance remain
with their existing owners. No OPS-017 acceptance or production release claim is
made by this source-only handoff.

## 2026-10-16 integrator extension

The original seven-file handoff above is retained as dated evidence. The
integrator extended the browser wrapper for the introduced commercial/Billing
journeys: separate entry/business output directories and required final results
for each guard. The fixture selfcheck now includes a successful entry followed by
a failed business guard; all **10** script selfchecks passed (15.982 seconds,
captured exit 0). This proves orchestration only. Real application results and
remaining qualification are recorded in
[the application completion receipt](APPLICATION_COMPLETION_2026_10_16.md).
