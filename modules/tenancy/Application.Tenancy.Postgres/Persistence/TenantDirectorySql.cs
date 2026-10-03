namespace Application.Tenancy.Postgres;

internal static class TenantDirectorySql
{
    internal static readonly string FindTenant = Load(nameof(FindTenant));

    private static string Load(string name)
    {
        var resource = $"{typeof(TenantDirectorySql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(TenantDirectorySql).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded Tenancy SQL resource: {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
