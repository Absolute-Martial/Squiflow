using SquiFlow.ApplicationKernel;
using SquiFlow.ApplicationKernel.Authorization;
using SquiFlow.ApplicationKernel.Features;
using SquiFlow.ApplicationKernel.Modules;
using SquiFlow.ApplicationKernel.Settings;
using SquiFlow.ApplicationKernel.Tenancy;
using SquiFlow.Customers;
using SquiFlow.Customers.Api;

var builder = WebApplication.CreateBuilder(args);

var graph = ModuleGraph.Build([CustomersModule.Descriptor]);
var coreApiModules = graph.ForHost(HostKind.CoreApi);
var featureDefinitions = coreApiModules.SelectMany(module => module.Features).ToArray();
var featureSnapshot = FeatureSnapshotBuilder.Publish(
    featureDefinitions,
    platformAllowed: [CustomersModule.Feature],
    tenantRequested: [CustomersModule.Feature],
    revision: 1);

var effectiveSearchLimit = SettingResolver.ResolveBoundedInt(
    CustomersModule.SearchResultLimit,
    platformCeiling: 100,
    tenantOverride: 75);

builder.Services.AddSingleton(graph);
builder.Services.AddSingleton<IFeatureSnapshotAccessor>(new FixedFeatureSnapshotAccessor(featureSnapshot));
builder.Services.AddScoped(_ => new TenantContext(new TenantId("phase0-tenant"), new SubjectId("phase0-subject")));
builder.Services.AddSingleton<IPermissionEvaluator, Phase0PermissionEvaluator>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    host = "core-api",
    moduleCount = coreApiModules.Count,
    featureRevision = featureSnapshot.Revision,
    effectiveCustomerSearchLimit = effectiveSearchLimit
}));

app.MapCustomerEndpoints();
app.Run();

file sealed class FixedFeatureSnapshotAccessor(EffectiveFeatureSnapshot current) : IFeatureSnapshotAccessor
{
    public EffectiveFeatureSnapshot Current { get; } = current;
}

file sealed class Phase0PermissionEvaluator : IPermissionEvaluator
{
    public ValueTask<bool> IsAllowedAsync(
        TenantContext tenantContext,
        PermissionId permissionId,
        CancellationToken cancellationToken = default)
    {
        // Phase 0 proves the SquiFlow permission boundary only. OpenFGA replaces this in Phase 1.
        var allowed = tenantContext.TenantId.Value == "phase0-tenant"
            && permissionId == CustomersModule.ViewPermission;
        return ValueTask.FromResult(allowed);
    }
}
