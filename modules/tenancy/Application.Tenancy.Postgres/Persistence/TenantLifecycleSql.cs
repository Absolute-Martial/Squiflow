namespace Application.Tenancy.Postgres;

internal static class TenantLifecycleSql
{
    internal static readonly string FindTenantForUpdate = Load(nameof(FindTenantForUpdate));
    internal static readonly string UpdateTenantLifecycle = Load(nameof(UpdateTenantLifecycle));
    internal static readonly string FindTenantLifecycleReceipt = Load(nameof(FindTenantLifecycleReceipt));
    internal static readonly string InsertTenantLifecycleReceipt = Load(nameof(InsertTenantLifecycleReceipt));
    internal static readonly string AcquireLifecycleLock = MembershipLifecycleSql.AcquireLifecycleLock;

    private static string Load(string name)
    {
        var resource = $"{typeof(TenantLifecycleSql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(TenantLifecycleSql).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded Tenancy SQL resource: {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
