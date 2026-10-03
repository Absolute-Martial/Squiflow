namespace Application.Tenancy.Postgres;

internal static class MembershipLifecycleSql
{
    internal static readonly string TenantAvailability = Load(nameof(TenantAvailability));
    internal static readonly string AccountAvailability = Load(nameof(AccountAvailability));
    internal static readonly string InitialOwnerExists = Load(nameof(InitialOwnerExists));
    internal static readonly string InsertMembership = Load(nameof(InsertMembership));
    internal static readonly string UpdateMembership = Load(nameof(UpdateMembership));
    internal static readonly string FindMembership = Load(nameof(FindMembership));
    internal static readonly string InsertMembershipReceipt = Load(nameof(InsertMembershipReceipt));
    internal static readonly string FindMembershipReceipt = Load(nameof(FindMembershipReceipt));
    internal static readonly string AcquireLifecycleLock = Load(nameof(AcquireLifecycleLock));

    private static string Load(string name)
    {
        var resource = $"{typeof(MembershipLifecycleSql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(MembershipLifecycleSql).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded Tenancy SQL resource: {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
