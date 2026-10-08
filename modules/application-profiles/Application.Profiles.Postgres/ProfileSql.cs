namespace Application.Profiles.Postgres;

internal static class ProfileSql
{
    internal static readonly string InsertPolicyHead = Load(nameof(InsertPolicyHead));
    internal static readonly string UpdatePolicyHead = Load(nameof(UpdatePolicyHead));
    internal static readonly string InsertPolicyRevision = Load(nameof(InsertPolicyRevision));
    internal static readonly string PublishPolicyHead = Load(nameof(PublishPolicyHead));
    internal static readonly string InsertPublication = Load(nameof(InsertPublication));
    internal static readonly string FindCommandReceipt = Load(nameof(FindCommandReceipt));
    internal static readonly string InsertCommandReceipt = Load(nameof(InsertCommandReceipt));
    internal static readonly string ReadPolicyHead = Load(nameof(ReadPolicyHead));
    internal static readonly string ReadPolicyRevision = Load(nameof(ReadPolicyRevision));
    internal static readonly string ReadPublication = Load(nameof(ReadPublication));
    internal static readonly string ReadAuthority = Load(nameof(ReadAuthority));
    internal static readonly string InsertAuthority = Load(nameof(InsertAuthority));
    internal static readonly string UpdateAuthority = Load(nameof(UpdateAuthority));
    internal static readonly string SetTenant = Load(nameof(SetTenant));
    internal static readonly string LockTenant = Load(nameof(LockTenant));

    private static string Load(string name)
    {
        var resourceName = $"{typeof(ProfileSql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(ProfileSql).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded Profiles SQL resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
