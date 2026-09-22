#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
snapshot_root="$repo_root/reference-sources/snapshots"
cache_root="${SQUIFLOW_REFERENCE_CACHE:-/tmp}"

mkdir -p "$snapshot_root/base-reference" "$snapshot_root/selected-runtime" "$snapshot_root/direct-candidates" "$snapshot_root/poc-gated" "$snapshot_root/donors"

materialize() {
  local class="$1"
  local name="$2"
  local cache_name="$3"
  local url="$4"
  local revision="$5"
  local license="$6"
  shift 6

  local destination="$snapshot_root/$class/$name"
  if [[ -e "$destination" ]]; then
    printf 'SKIP %s (already exists)\n' "$destination"
    return
  fi

  local source_repo="$cache_root/$cache_name"
  local temporary_repo=""
  local ref="$revision"

  if [[ ! -d "$source_repo/.git" ]] || ! git -C "$source_repo" cat-file -e "$revision^{commit}" 2>/dev/null; then
    temporary_repo="$(mktemp -d)"
    source_repo="$temporary_repo/repository"
    git init -q "$source_repo"
    git -C "$source_repo" remote add origin "$url"
    git -C "$source_repo" fetch -q --depth 1 origin "$revision"
    ref="FETCH_HEAD"
  fi

  local staging
  staging="$(mktemp -d "$snapshot_root/.staging-$name-XXXXXX")"
  if ! git -C "$source_repo" archive --format=tar "$ref" -- "$@" | tar -xf - -C "$staging"; then
    printf 'FAILED %s; staging retained at %s\n' "$name" "$staging" >&2
    return 1
  fi

  cat > "$staging/_SQUIFLOW_SNAPSHOT.md" <<META
# SquiFlow local source snapshot

- Source: $url
- Revision: \`$revision\`
- License: $license
- Admission class: $class
- Materialized: $(date -u +%Y-%m-%dT%H:%M:%SZ)
- Contents: allow-listed source/tests/docs only; no upstream Git metadata

This snapshot is research evidence, not SquiFlow product code. See \`reference-sources/README.md\` and \`reference-sources/SOURCES.md\`.
META

  mv "$staging" "$destination"
  if [[ -n "$temporary_repo" ]]; then
    rm -rf "$temporary_repo"
  fi
  printf 'ADDED %s\n' "$destination"
}

materialize_files() {
  local class="$1"
  local name="$2"
  local source_url="$3"
  local raw_base="$4"
  local revision="$5"
  local license="$6"
  shift 6

  local destination="$snapshot_root/$class/$name"
  if [[ -e "$destination" ]]; then
    printf 'SKIP %s (already exists)\n' "$destination"
    return
  fi

  local staging
  staging="$(mktemp -d "$snapshot_root/.staging-$name-XXXXXX")"
  local path
  for path in "$@"; do
    mkdir -p "$staging/$(dirname "$path")"
    curl -fsSL "$raw_base/$revision/$path" -o "$staging/$path"
  done

  cat > "$staging/_SQUIFLOW_SNAPSHOT.md" <<META
# SquiFlow local source snapshot

- Source: $source_url
- Revision: \`$revision\`
- License: $license
- Admission class: $class
- Materialized: $(date -u +%Y-%m-%dT%H:%M:%SZ)
- Contents: allow-listed source/tests/docs only; no upstream Git metadata

This snapshot is research evidence, not SquiFlow product code. See \`reference-sources/README.md\` and \`reference-sources/SOURCES.md\`.
META

  mv "$staging" "$destination"
  printf 'ADDED %s\n' "$destination"
}

materialize base-reference fullstackhero-backend squiflow-ref-fsh \
  https://github.com/fullstackhero/dotnet-starter-kit.git \
  3f2959e683e9f83f13e55e1678c9119f63c7e8e5 MIT \
  LICENSE README.md global.json src

materialize selected-runtime proto-actor squiflow-protoactor \
  https://github.com/asynkron/protoactor-dotnet.git \
  6a5706283022e865f0e490e85ec7a7c5f4f891e0 Apache-2.0 \
  LICENSE README.md Directory.Packages.props global.json \
  src/Proto.Actor src/Proto.TestKit tests/Proto.Actor.Tests tests/Proto.TestKit.Tests

materialize selected-runtime quartz-net squiflow-quartznet \
  https://github.com/quartznet/quartznet.git \
  d523c898bc0222084bf48749a835063e75e70127 Apache-2.0 \
  license.txt README.md Directory.Build.props Directory.Packages.props global.json \
  src/Quartz src/Quartz.Extensions.DependencyInjection src/Quartz.Extensions.Hosting \
  src/Quartz.Serialization.SystemTextJson src/Quartz.Tests.Unit \
  database/tables/tables_postgres.sql database/migrations/4.0 docs

materialize direct-candidates communitytoolkit-mvvm squiflow-ct \
  https://github.com/CommunityToolkit/dotnet.git \
  b135626dd54d33b8f05f2ff31591592c004aa848 MIT \
  License.md README.md Directory.Build.props Directory.Packages.props \
  src/CommunityToolkit.Mvvm src/CommunityToolkit.Mvvm.SourceGenerators \
  tests/CommunityToolkit.Mvvm.UnitTests tests/CommunityToolkit.Mvvm.SourceGenerators.UnitTests

materialize direct-candidates finbuckle-multitenant squiflow-ref-finbuckle \
  https://github.com/Finbuckle/Finbuckle.MultiTenant.git \
  ad67b15ecb6158f041abbb0c39ae4d718f3fda42 Apache-2.0 \
  LICENSE README.md Directory.Build.props Directory.Packages.props \
  src/Finbuckle.MultiTenant src/Finbuckle.MultiTenant.Abstractions \
  src/Finbuckle.MultiTenant.AspNetCore src/Finbuckle.MultiTenant.EntityFrameworkCore \
  test/Finbuckle.MultiTenant.Test test/Finbuckle.MultiTenant.AspNetCore.Test \
  test/Finbuckle.MultiTenant.EntityFrameworkCore.Test

materialize direct-candidates openfga-dotnet-sdk squiflow-ref-openfga-dotnet \
  https://github.com/openfga/dotnet-sdk.git \
  ec8ee04761b41e2400693b911a17463877e500c3 Apache-2.0 \
  LICENSE README.md SUPPORTED_FRAMEWORKS.md OpenFga.Sdk.sln \
  src/OpenFga.Sdk src/OpenFga.Sdk.Test

materialize poc-gated casbin-net squiflow-ref-casbin-net \
  https://github.com/apache/casbin-Casbin.NET.git \
  30b142f0f5c4598852e8258d638bded3e24caf2c Apache-2.0 \
  LICENSE NOTICE DISCLAIMER README.md INCREMENTAL_FILTERED_POLICY.md Casbin.NET.sln global.json \
  Casbin Casbin.UnitTests Casbin.Benchmark

materialize poc-gated casbin-efcore-adapter squiflow-ref-casbin-efcore \
  https://github.com/apache/casbin-efcore-adapter.git \
  1cc2c9ae985e48a93c38d1b884095502d15d52f8 Apache-2.0 \
  LICENSE README.md MULTI_CONTEXT_DESIGN.md MULTI_CONTEXT_USAGE_GUIDE.md EFCore-Adapter.sln \
  Casbin.Persist.Adapter.EFCore Casbin.Persist.Adapter.EFCore.UnitTest \
  Casbin.Persist.Adapter.EFCore.IntegrationTest

materialize direct-candidates stateless squiflow-ref-stateless \
  https://github.com/dotnet-state-machine/stateless.git \
  588f1a1a08683b452eb7c05562d9f055693cba5d Apache-2.0 \
  LICENSE README.md src test

materialize poc-gated dock-avalonia squiflow-dock \
  https://github.com/wieslawsoltes/Dock.git \
  cc08602d02fde1b85067cec064da29f34785e505 MIT \
  LICENSE.TXT README.md Directory.Build.props Directory.Packages.props src tests

materialize poc-gated identitymodel-oidc-client squiflow-oidc \
  https://github.com/DuendeSoftware/foss.git \
  6eaad5d969799f3a7eb388238fecaa655c66bd19 Apache-2.0 \
  LICENSE README.md Directory.Packages.props global.json \
  identity-model-oidc-client/src identity-model-oidc-client/test \
  identity-model-oidc-client/samples/WindowsConsoleSystemBrowser

materialize poc-gated autofac-multitenant squiflow-autofac-multitenant \
  https://github.com/autofac/Autofac.Multitenant.git \
  2fdd4c0fc6a913324f5b985184d73f484db501e4 MIT \
  LICENSE README.md Directory.Build.props global.json \
  src/Autofac.Multitenant test/Autofac.Multitenant.Test \
  test/Autofac.Multitenant.AspNetCore.Test

materialize poc-gated autofac-aspnetcore-multitenant squiflow-autofac-aspnetcore-multitenant \
  https://github.com/autofac/Autofac.AspNetCore.Multitenant.git \
  42851fdc88d2a988f266e70d108064210c0559d2 MIT \
  LICENSE README.md Directory.Build.props global.json \
  src test

materialize poc-gated temporal-dotnet squiflow-temporal-sdk \
  https://github.com/temporalio/sdk-dotnet.git \
  4a183307d90d6291fc213941a4f8d2506bd85800 MIT \
  LICENSE README.md CHANGELOG.md Directory.Build.props Directory.Packages.props global.json \
  src/Temporalio/Temporalio.csproj src/Temporalio/Activities src/Temporalio/Client/Schedules \
  src/Temporalio/Worker src/Temporalio/Workflows src/Temporalio.Extensions.Hosting \
  tests/Temporalio.Tests/Activities tests/Temporalio.Tests/Worker tests/Temporalio.Tests/Workflows \
  tests/Temporalio.Tests/Extensions/Hosting

materialize donors fullstackhero squiflow-ref-fsh \
  https://github.com/fullstackhero/dotnet-starter-kit.git \
  3f2959e683e9f83f13e55e1678c9119f63c7e8e5 MIT \
  LICENSE README.md global.json \
  src/BuildingBlocks/Persistence/TenantIsolationExtensions.cs \
  src/BuildingBlocks/Web/Modules/ModuleLoader.cs \
  src/Modules/Multitenancy/Modules.Multitenancy/Provisioning \
  src/Tests/Architecture.Tests \
  src/Tests/Multitenancy.Tests/Provisioning \
  src/Tests/Integration.Tests/Tests/Multitenancy

materialize donors orchard-core squiflow-ref-orchard \
  https://github.com/OrchardCMS/OrchardCore.git \
  b304fcd78a70b792c6f63916c0ce6e6957bb1aa0 BSD-3-Clause \
  LICENSE README.md Directory.Build.props Directory.Packages.props global.json \
  src/OrchardCore/OrchardCore.Abstractions/Extensions/Features/IFeatureInfo.cs \
  src/OrchardCore/OrchardCore.Abstractions/Shell/IShellFeaturesManager.cs \
  src/OrchardCore/OrchardCore/Shell/ShellFeaturesManager.cs \
  src/OrchardCore/OrchardCore.Recipes.Abstractions/Services/IRecipeExecutor.cs \
  src/OrchardCore/OrchardCore.Recipes.Core/Services/RecipeExecutor.cs \
  src/OrchardCore.Modules/OrchardCore.Features/Recipes/Executors/FeatureStep.cs \
  test/OrchardCore.Tests/Recipes/RecipeExecutorTests.cs

materialize donors prism squiflow-ref-prism \
  https://github.com/PrismLibrary/Prism.git \
  358118cd640d9a22ff8cf21c8ad197fa038b7990 'Community-or-Commercial' \
  LICENSE README.md Directory.Build.props Directory.Packages.props global.json \
  src/Prism.Core src/Avalonia e2e/Avalonia/PrismAvaloniaDemo

materialize donors uno-extensions squiflow-uno \
  https://github.com/unoplatform/uno.extensions.git \
  945312137dd56f42745a58cb5c65d9e81922d659 Apache-2.0 \
  LICENSE.md README.md Directory.Build.props global.json \
  src/Uno.Extensions.Hosting src/Uno.Extensions.Hosting.UI \
  src/Uno.Extensions.Navigation src/Uno.Extensions.Navigation.Tests src/Uno.Extensions.Navigation.UI \
  src/Uno.Extensions.Authentication src/Uno.Extensions.Authentication.Tests src/Uno.Extensions.Authentication.Oidc \
  src/Uno.Extensions.Storage src/Uno.Extensions.Localization src/Uno.Extensions.Configuration src/Uno.Extensions.Logging

materialize donors csla squiflow-csla \
  https://github.com/MarimerLLC/csla.git \
  408c05eef72c0ffe651fac6565641c71b21d7457 MIT \
  license.md README.md Directory.Build.props Directory.Packages.props \
  Source/Csla Source/docs

materialize donors elsa squiflow-ref-elsa \
  https://github.com/elsa-workflows/elsa-core.git \
  aa021de39ee1323c212190a5f561b45d858206ec MIT \
  LICENSE README.md Directory.Build.props Directory.Packages.props global.json \
  src/modules/Elsa.Workflows.Runtime \
  test/unit/Elsa.Workflows.Runtime.UnitTests \
  test/integration/Elsa.Workflows.IntegrationTests/GracefulShutdown/InterruptedRecoveryIntegrationTests.cs \
  test/integration/Elsa.Workflows.IntegrationTests/Scenarios/PublishEventOutbox/Tests.cs

materialize_files donors abp-framework \
  https://github.com/abpframework/abp.git \
  https://raw.githubusercontent.com/abpframework/abp \
  955a7876537ebeaedbcb81e5407facb0840616bb LGPL-3.0 \
  LICENSE.md README.md \
  framework/src/Volo.Abp.Core/Volo.Abp.Core.csproj \
  framework/src/Volo.Abp.Core/Volo/Abp/Modularity/AbpModule.cs \
  framework/src/Volo.Abp.Core/Volo/Abp/Modularity/AbpModuleDescriptor.cs \
  framework/src/Volo.Abp.Core/Volo/Abp/Modularity/DependsOnAttribute.cs \
  framework/src/Volo.Abp.Core/Volo/Abp/Modularity/ModuleLoader.cs \
  framework/src/Volo.Abp.Core/Volo/Abp/DependencyInjection/DefaultConventionalRegistrar.cs \
  framework/test/Volo.Abp.Core.Tests/Volo/Abp/Modularity/ModuleLoader_Tests.cs \
  framework/src/Volo.Abp.Features/Volo/Abp/Features/FeatureDefinition.cs \
  framework/src/Volo.Abp.Features/Volo/Abp/Features/FeatureDefinitionManager.cs \
  framework/src/Volo.Abp.Features/Volo/Abp/Features/FeatureChecker.cs \
  framework/src/Volo.Abp.Settings/Volo/Abp/Settings/SettingDefinition.cs \
  framework/src/Volo.Abp.Settings/Volo/Abp/Settings/SettingDefinitionManager.cs \
  framework/src/Volo.Abp.Settings/Volo/Abp/Settings/SettingProvider.cs \
  framework/src/Volo.Abp.Authorization.Abstractions/Volo/Abp/Authorization/Permissions/PermissionDefinition.cs \
  framework/src/Volo.Abp.Authorization/Volo/Abp/Authorization/Permissions/PermissionDefinitionManager.cs \
  framework/src/Volo.Abp.Auditing/Volo/Abp/Auditing/AuditingManager.cs \
  framework/src/Volo.Abp.Auditing/Volo/Abp/Auditing/JsonAuditSerializer.cs \
  framework/src/Volo.Abp.MultiTenancy/Volo/Abp/MultiTenancy/CurrentTenant.cs \
  framework/src/Volo.Abp.MultiTenancy/Volo/Abp/MultiTenancy/TenantResolver.cs \
  framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWork.cs \
  framework/src/Volo.Abp.Uow/Volo/Abp/Uow/UnitOfWorkInterceptor.cs \
  framework/test/Volo.Abp.Uow.Tests/Volo/Abp/Uow/UnitOfWork_Ambient_Scope_Tests.cs \
  framework/test/Volo.Abp.Uow.Tests/Volo/Abp/Uow/UnitOfWork_Nested_Tests.cs

materialize donors oqtane-framework squiflow-research-oqtane \
  https://github.com/oqtane/oqtane.framework.git \
  b5e76441a4139966beb9327708ce39a59764787d MIT \
  LICENSE README.md \
  Oqtane.Shared/Interfaces Oqtane.Shared/Models/ModuleDefinition.cs Oqtane.Shared/Models/Tenant.cs \
  Oqtane.Server/Infrastructure/TenantManager.cs \
  Oqtane.Server/Controllers/ModuleController.cs Oqtane.Server/Controllers/ModuleDefinitionController.cs \
  Oqtane.Server/Repository/ModuleDefinitionRepository.cs \
  Oqtane.Client/Modules/Admin/ModuleDefinitions Oqtane.Client/Services/ModuleDefinitionService.cs

materialize donors extcore squiflow-research-extcore \
  https://github.com/ExtCore/ExtCore.git \
  3d10fcb358e3828b42e138fbbc942ef14fd2fe2a Apache-2.0 \
  LICENSE.txt README.md \
  src/ExtCore.Infrastructure src/ExtCore.WebApplication src/ExtCore.Mvc.Infrastructure

materialize_files donors simplcommerce \
  https://github.com/simplcommerce/SimplCommerce.git \
  https://raw.githubusercontent.com/simplcommerce/SimplCommerce \
  3472ba02a6f2d9b6bdca7f7fb84957176aa799dc Apache-2.0 \
  License.txt README.md \
  src/SimplCommerce.Infrastructure/Modules/IModuleConfigurationManager.cs \
  src/SimplCommerce.Infrastructure/Modules/IModuleInitializer.cs \
  src/SimplCommerce.Infrastructure/Modules/MissingModuleManifestException.cs \
  src/SimplCommerce.Infrastructure/Modules/ModuleConfigurationManager.cs \
  src/SimplCommerce.Infrastructure/Modules/ModuleInfo.cs \
  src/Modules/SimplCommerce.Module.Core/ModuleInitializer.cs \
  src/Modules/SimplCommerce.Module.Catalog/ModuleInitializer.cs

materialize_files donors serenity \
  https://github.com/serenity-is/Serenity.git \
  https://raw.githubusercontent.com/serenity-is/Serenity \
  2d854c6550436d957945898867194ab8260f946f MIT \
  LICENSE.md README.md \
  src/core/ComponentModel/PropertyGrid/PropertyItem.cs \
  src/core/ComponentModel/PropertyGrid/PropertyItemsData.cs \
  src/core/ComponentModel/PropertyGrid/RequiredAttribute.cs \
  src/core/ComponentModel/PropertyGrid/Editing/EditorTypeAttribute.cs \
  src/services/Entity/PropertyGrid/DefaultPropertyItemProvider.cs \
  src/services/Entity/PropertyGrid/IPropertyItemProvider.cs \
  src/services/Entity/PropertyGrid/IPropertyProcessor.cs \
  src/services/Entity/PropertyGrid/BasicPropertyProcessor/BasicPropertyProcessor.cs \
  src/services/Entity/PropertyGrid/BasicPropertyProcessor/BasicPropertyProcessor.Editing.cs \
  src/services/Entity/PropertyGrid/BasicPropertyProcessor/BasicPropertyProcessor.ReadPermission.cs \
  packages/corelib/src/base/propertyitem.ts \
  packages/corelib/src/ui/widgets/propertygrid.tsx \
  packages/corelib/src/ui/widgets/propertygrid.spec.tsx \
  tests/Serenity.Net.Tests/core/componentmodel/propertygrid/PropertyItemTests.cs \
  tests/Serenity.Net.Tests/services/entity/propertygrid/DefaultPropertyItemProviderTests.cs

printf '\nReference source snapshots are ready under %s\n' "$snapshot_root"
