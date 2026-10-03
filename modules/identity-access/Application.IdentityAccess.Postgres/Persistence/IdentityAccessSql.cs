namespace Application.IdentityAccess.Postgres;

internal static class IdentityAccessSql
{
    internal static readonly string LockIdempotencyKey = Load(nameof(LockIdempotencyKey));
    internal static readonly string FindAccountReceipt = Load(nameof(FindAccountReceipt));
    internal static readonly string FindLinkReceipt = Load(nameof(FindLinkReceipt));
    internal static readonly string FindBinding = Load(nameof(FindBinding));
    internal static readonly string FindAccountAvailability = Load(nameof(FindAccountAvailability));
    internal static readonly string LockAccount = Load(nameof(LockAccount));
    internal static readonly string InsertAccount = Load(nameof(InsertAccount));
    internal static readonly string InsertBinding = Load(nameof(InsertBinding));
    internal static readonly string InsertAccountReceipt = Load(nameof(InsertAccountReceipt));
    internal static readonly string InsertLinkReceipt = Load(nameof(InsertLinkReceipt));

    private static string Load(string name)
    {
        var resource = $"{typeof(IdentityAccessSql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(IdentityAccessSql).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded IdentityAccess SQL resource: {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
