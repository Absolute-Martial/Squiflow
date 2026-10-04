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

ADM-002 specifically exercises deadline versus caller cancellation, response-started behavior, permit release, protected no-store and public-route preservation. COM-020 checks immutable invoice facts, four-decimal ToEven validation, authority-before-replay and explicit undecided-policy outcomes. COM-021's new PostgreSQL suite must prove atomic invoice/number/receipt, tenant isolation, races and lost-response retry; it does not exist until that assignment earns it.

## Future hosts and external checks

Worker, tenant Web and Admin Web do not currently have executable build targets. Their implementation tasks add and document exact project/test/host commands under the independent-host contract. Browser tasks register a real Playwright command with their actual runner/framework/configuration; a guessed `npm test` or nonexistent `verify-host.sh web` cannot be reported as tested. Each future task handoff supplies that exact command for receiving integration.

Live ZITADEL, DNS/TLS, object/key/payment providers, target deployment, CI, backup/restore and load/release drills require the named authorized environment. Record the exact task-created test/runbook command and expected assertions as part of its bounded implementation. Testcontainers proves the selected local provider property; it does not qualify a managed IdP, cloud account, remote CI or production node.

If tooling/access is unavailable, return meaningful scoped tests plus static findings, exact unrun commands and expected assertions. Classify introduced material claims as awaiting evidence. Do not install a large toolchain, remove checks, serialize/retry failing suites or substitute mocks merely to obtain a qualification label.
