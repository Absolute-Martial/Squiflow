namespace Application.Pricing.Postgres;

internal static class PricingSql
{
    internal static readonly string SetTenant = Load(nameof(SetTenant));
    internal static readonly string Lock = Load(nameof(Lock));
    internal static readonly string LockPublication = Load(nameof(LockPublication));
    internal static readonly string FindFamily = Load(nameof(FindFamily));
    internal static readonly string FindPolicy = Load(nameof(FindPolicy));
    internal static readonly string InsertPolicy = Load(nameof(InsertPolicy));
    internal static readonly string NextRevision = Load(nameof(NextRevision));
    internal static readonly string InsertDraft = Load(nameof(InsertDraft));
    internal static readonly string FindRevision = Load(nameof(FindRevision));
    internal static readonly string FindRevisionForUpdate = Load(nameof(FindRevisionForUpdate));
    internal static readonly string FindOverlap = Load(nameof(FindOverlap));
    internal static readonly string PublishRevision = Load(nameof(PublishRevision));
    internal static readonly string SupersedeRevision = Load(nameof(SupersedeRevision));
    internal static readonly string RetireRevision = Load(nameof(RetireRevision));
    internal static readonly string FindCandidates = Load(nameof(FindCandidates));
    internal static readonly string ListRevisions = Load(nameof(ListRevisions));
    internal static readonly string FindReceipt = Load(nameof(FindReceipt));
    internal static readonly string InsertReceipt = Load(nameof(InsertReceipt));

    private static string Load(string name)
    {
        var resourceName = $"{typeof(PricingSql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(PricingSql).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded Pricing SQL resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
