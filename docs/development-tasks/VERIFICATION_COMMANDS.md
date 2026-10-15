# Verification command and evidence handoff

Run from the assigned repository root. Commands here name current entry points. A task defines the assertions; an exit code alone does not prove the intended property. Use [handoff rules](HANDOFF_AND_INTEGRATION.md) and record the actual SDK/provider/browser versions, command, exit, test totals/failures/skips, log location/digest and relevant nonsecret configuration.

## Current executable checks

```bash
python3 docs/development-tasks/validate_catalog.py
./eng/verify.sh
./eng/verify-host.sh admin-api
./eng/verify-host.sh core-api
./eng/build-host.sh admin-api --publish
./eng/build-host.sh core-api --publish
./eng/verify-host.sh web
./eng/verify-host.sh workstation
./eng/build-host.sh web --publish
./eng/build-host.sh workstation --publish
```

The normal full gate performs locked restore, formatting verification, Release build and all suites using normal repository parallel behavior. Docker supports the real PostgreSQL/OpenFGA fixtures where required. Run one integrated full gate without competing builds; focused checks during implementation do not replace it. Coverage is `COLLECT_COVERAGE=1 ./eng/verify.sh` when the assigned qualification needs fresh coverage. Do not repeat coverage just to create another artifact after an already sufficient pass.

## Existing focused suites

These commands restore/build/test their actual project graphs. Add a deliberate `--filter` only after reading the selected test names; zero matching tests is not a pass. Provider fixtures require their real Docker/runtime dependencies.

```bash
dotnet test tests/integration/Application.AdminApi.Tests/Application.AdminApi.Tests.csproj --configuration Release
dotnet test tests/integration/Application.CoreApi.Tests/Application.CoreApi.Tests.csproj --configuration Release
dotnet test tests/unit/Application.Invoices.Tests/Application.Invoices.Tests.csproj --configuration Release
dotnet test tests/architecture/Application.Architecture.Tests/Application.Architecture.Tests.csproj --configuration Release
dotnet test tests/integration/Application.Orders.Postgres.Tests/Application.Orders.Postgres.Tests.csproj --configuration Release
dotnet test tests/integration/Application.Customers.Postgres.Tests/Application.Customers.Postgres.Tests.csproj --configuration Release
```

ADM-002 specifically exercises deadline versus caller cancellation, response-started behavior, permit release, protected no-store and public-route preservation. COM-020 checks immutable full-Order invoice facts, organization ordinal, UTC date, active debtor eligibility, four-decimal ToEven validation and authority-before-replay. COM-021's earned PostgreSQL suite must prove atomic invoice/number/receipt, tenant isolation, races, uncertain commit and lost-response recovery. COM-022 separately proves independent issue/view authority through actual pinned OpenFGA and native API/provider cases.

## Future hosts and external checks

Worker and Admin Web do not currently have executable build targets. Tenant Web and Workstation have only the bounded public launch/connection hosts owned by `docs/implementation/PUBLIC_APPLICATION_ENTRY.md`; the commands above verify those exact graphs. Their authenticated business/session journeys remain absent. Future implementation tasks add their own named tests and real provider/browser/target evidence.

The public Web browser guard uses a built Release host and an external Playwright installation (tested with 1.64.0 and Chromium). Keep this tool outside product source; it does not introduce an npm frontend project:

```bash
npm install --prefix /tmp/application-entry-browser --no-audit --no-fund playwright@1.64.0
PLAYWRIGHT_BROWSERS_PATH=/tmp/application-entry-browsers \
  /tmp/application-entry-browser/node_modules/.bin/playwright install chromium
APPLICATION_ENTRY_PLAYWRIGHT_PATH=/tmp/application-entry-browser/node_modules/playwright \
  PLAYWRIGHT_BROWSERS_PATH=/tmp/application-entry-browsers \
  node eng/test-application-entry-browser.cjs
```

The guard owns a synthetic loopback public-contract fixture and the actual Web process. It checks responsive rendering, refresh, busy/cancel/deadline, encoded hostile names and process-loss/reload. It does not qualify live ZITADEL, Windows or a production provider deployment.

Live ZITADEL, DNS/TLS, object/key/payment providers, target deployment, CI, backup/restore and load/release drills require the named authorized environment. Record the exact task-created test/runbook command and expected assertions as part of its bounded implementation. Testcontainers proves the selected local provider property; it does not qualify a managed IdP, cloud account, remote CI or production node.

If tooling/access is unavailable, return meaningful scoped tests plus static findings, exact unrun commands and expected assertions. Classify introduced material claims as awaiting evidence. Do not install a large toolchain, remove checks, serialize/retry failing suites or substitute mocks merely to obtain a qualification label.

The configured signed-in entry has an additional real HTTPS browser/process guard:

```sh
APPLICATION_ENTRY_PLAYWRIGHT_PATH=/path/to/node_modules/playwright \
PLAYWRIGHT_BROWSERS_PATH=/path/to/browsers \
node eng/test-signed-in-entry-browser.cjs
```

It requires OpenSSL and a built Web host. It uses an isolated signed local issuer
and temporary per-process certificate trust; it does not qualify live ZITADEL or
Windows. Exact session/API scope and current qualification limits belong to
`docs/implementation/SIGNED_IN_APPLICATION_ENTRY.md`.
