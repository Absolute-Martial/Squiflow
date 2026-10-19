namespace Application.PlatformAdministration.Postgres;

internal static class PlatformAdministrationSql
{
    internal static readonly string BrowseAccessAudit = Load(nameof(BrowseAccessAudit));

    private static string Load(string name)
    {
        var resource = $"{typeof(PlatformAdministrationSql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(PlatformAdministrationSql).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded PlatformAdministration SQL resource: {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
