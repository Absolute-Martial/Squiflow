namespace Application.Orders.Postgres;

internal static class OrderSql
{
    internal static readonly string QuotationOriginsSchema = Load(nameof(QuotationOriginsSchema));
    internal static readonly string InsertQuotationOrigin = Load(nameof(InsertQuotationOrigin));
    internal static readonly string FindQuotationOrigin = Load(nameof(FindQuotationOrigin));
    internal static readonly string PinCommercialPublication = Load(nameof(PinCommercialPublication));
    internal static readonly string LockOrderForCommit = Load(nameof(LockOrderForCommit));
    internal static readonly string TryCommitOrder = Load(nameof(TryCommitOrder));
    internal static readonly string TryAbandonOrder = Load(nameof(TryAbandonOrder));
    internal static readonly string TryReviseOrder = Load(nameof(TryReviseOrder));
    internal static readonly string DeleteLines = Load(nameof(DeleteLines));
    internal static readonly string FindOrderState = Load(nameof(FindOrderState));
    internal static readonly string SetTenant = Load(nameof(SetTenant));
    internal static readonly string InsertOrder = Load(nameof(InsertOrder));
    internal static readonly string InsertLine = Load(nameof(InsertLine));
    internal static readonly string InsertReceipt = Load(nameof(InsertReceipt));
    internal static readonly string FindReceipt = Load(nameof(FindReceipt));
    internal static readonly string FindOrder = Load(nameof(FindOrder));
    internal static readonly string FindLines = Load(nameof(FindLines));
    internal static readonly string ListOrderHeaders = Load(nameof(ListOrderHeaders));
    internal static readonly string ListOrderHistory = Load(nameof(ListOrderHistory));

    private static string Load(string name)
    {
        var resourceName = $"{typeof(OrderSql).Namespace}.Sql.{name}.sql";
        using var stream = typeof(OrderSql).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded Orders SQL resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
