namespace Application.Catalog.Postgres;

internal static class CatalogSql
{
    internal static readonly string SetTenant = Load(nameof(SetTenant));
    internal static readonly string LockScope = Load(nameof(LockScope));
    internal static readonly string LockPublication = Load(nameof(LockPublication));
    internal static readonly string FindUnitForSelection = Load(nameof(FindUnitForSelection));
    internal static readonly string FindItemForSelection = Load(nameof(FindItemForSelection));
    internal static readonly string FindConversion = Load(nameof(FindConversion));
    internal static readonly string FindConversionRevision = Load(nameof(FindConversionRevision));
    internal static readonly string InsertConversion = Load(nameof(InsertConversion));
    internal static readonly string ChangeAvailability = Load(nameof(ChangeAvailability));
    internal static readonly string FindUnit = Load(nameof(FindUnit));
    internal static readonly string FindItem = Load(nameof(FindItem));
    internal static readonly string ListUnits = Load(nameof(ListUnits));
    internal static readonly string ListItems = Load(nameof(ListItems));
    internal static readonly string FindReceipt = Load(nameof(FindReceipt));
    internal static readonly string InsertUnit = Load(nameof(InsertUnit));
    internal static readonly string InsertItem = Load(nameof(InsertItem));
    internal static readonly string InsertReceipt = Load(nameof(InsertReceipt));
    internal static readonly string RenameUnit = Load(nameof(RenameUnit));
    internal static readonly string RenameItem = Load(nameof(RenameItem));
    internal static readonly string RetireUnit = Load(nameof(RetireUnit));
    internal static readonly string RetireItem = Load(nameof(RetireItem));

    private static string Load(string name)
    {
        var resourceName = $"{typeof(CatalogSql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(CatalogSql).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded Catalog SQL resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
