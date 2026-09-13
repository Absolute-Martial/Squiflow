using SquiFlow.ApplicationKernel;
using SquiFlow.ApplicationKernel.Authorization;
using SquiFlow.ApplicationKernel.Features;
using SquiFlow.ApplicationKernel.Modules;
using SquiFlow.ApplicationKernel.Settings;

namespace SquiFlow.Customers;

public static class CustomersModule
{
    public static readonly ModuleId Id = new("customers");
    public static readonly FeatureId Feature = new("customers.enabled");
    public static readonly PermissionId ViewPermission = new("customers.view");
    public static readonly SettingDefinition<int> SearchResultLimit = new(
        new SettingKey("customers.search-result-limit"),
        defaultValue: 50,
        isValid: value => value is >= 10 and <= 500);

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
            new FeatureDefinition(Feature, Id, Array.Empty<FeatureId>())
        ],
        Permissions:
        [
            new PermissionDefinition(ViewPermission, Id, "View customers", Feature)
        ],
        Settings:
        [
            SearchResultLimit
        ]);
}
