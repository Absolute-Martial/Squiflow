namespace Application.Tenancy.Postgres;

internal static class TenantProvisioningSql
{
    internal static readonly string FindReceipt = Load(nameof(FindReceipt));
    internal static readonly string InsertTenant = Load(nameof(InsertTenant));
    internal static readonly string InsertReceipt = Load(nameof(InsertReceipt));

    private static string Load(string name)
    {
        var resource = $"{typeof(TenantProvisioningSql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(TenantProvisioningSql).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded Tenancy SQL resource: {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
