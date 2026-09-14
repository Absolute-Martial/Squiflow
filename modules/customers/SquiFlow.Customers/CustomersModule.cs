using SquiFlow.ApplicationKernel;
using SquiFlow.ApplicationKernel.Features;
using SquiFlow.ApplicationKernel.Hosting;
using SquiFlow.ApplicationKernel.Modules;
using SquiFlow.ApplicationKernel.Permissions;
using SquiFlow.ApplicationKernel.Settings;

namespace SquiFlow.Customers;

public static class CustomersModule
{
    public static readonly ModuleId Id = new("customers");
    public static readonly FeatureId Feature = new("customers.enabled");
    public static readonly PermissionId ViewPermission = new("customers.view");
    public static readonly SettingDefinition<int> SearchResultLimit = new(
        new SettingKey("customers.search-result-limit"),
        Id,
        defaultValue: 50,
        allowedScopes: new HashSet<SettingScope> { SettingScope.Platform, SettingScope.Tenant },
        isValid: value => value > 0);

    public static ModuleDescriptor Descriptor { get; } = new(
        Id,
        new Version(0, 1, 0),
        Dependencies: Array.Empty<ModuleId>(),
        SupportedHosts: new HashSet<HostKind>
        {
            HostKind.Workstation,
            HostKind.CoreApi,
            HostKind.TenantWeb
        },
        Features:
        [
            new FeatureDefinition(
                Feature,
                Id,
                Dependencies: Array.Empty<FeatureId>(),
                SupportedHosts: new HashSet<HostKind>
                {
                    HostKind.Workstation,
                    HostKind.CoreApi,
                    HostKind.TenantWeb
                },
                ReleaseChannel.Stable,
                OfflineFeaturePolicy.SnapshotAllowed)
        ],
        Permissions:
        [
            new PermissionDefinition(
                ViewPermission,
                Id,
                "View customers",
                new HashSet<HostKind>
                {
                    HostKind.Workstation,
                    HostKind.CoreApi,
                    HostKind.TenantWeb
                },
                RequiredFeature: Feature)
        ],
        Settings:
        [
            SearchResultLimit
        ]);
}
