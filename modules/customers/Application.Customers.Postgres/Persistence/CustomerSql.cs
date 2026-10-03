namespace Application.Customers.Postgres;

internal static class CustomerSql
{
    internal static readonly string SetTenant = Load(nameof(SetTenant));
    internal static readonly string InsertOrganization = Load(nameof(InsertOrganization));
    internal static readonly string InsertOrganizationReceipt = Load(nameof(InsertOrganizationReceipt));
    internal static readonly string InsertProgram = Load(nameof(InsertProgram));
    internal static readonly string InsertProgramReceipt = Load(nameof(InsertProgramReceipt));
    internal static readonly string FindOrganization = Load(nameof(FindOrganization));
    internal static readonly string FindProgram = Load(nameof(FindProgram));
    internal static readonly string FindOrganizationReceipt = Load(nameof(FindOrganizationReceipt));
    internal static readonly string FindProgramReceipt = Load(nameof(FindProgramReceipt));
    internal static readonly string ListOrganizations = Load(nameof(ListOrganizations));
    internal static readonly string ListPrograms = Load(nameof(ListPrograms));
    internal static readonly string InsertIndividual = Load(nameof(InsertIndividual));
    internal static readonly string FindIndividual = Load(nameof(FindIndividual));
    internal static readonly string UpdateIndividualAvailability = Load(nameof(UpdateIndividualAvailability));
    internal static readonly string InsertIndividualReceipt = Load(nameof(InsertIndividualReceipt));
    internal static readonly string FindIndividualReceipt = Load(nameof(FindIndividualReceipt));

    private static string Load(string name)
    {
        var resourceName = $"{typeof(CustomerSql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(CustomerSql).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded Customers SQL resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
