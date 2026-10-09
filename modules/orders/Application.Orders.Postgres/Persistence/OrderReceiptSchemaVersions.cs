namespace Application.Orders.Postgres;

// Every command-receipt envelope version PostgresOrderDraftStore can write. The writer and the
// 202610070001 downgrade guard must share this set: a guard that recognizes a subset lets a
// rollback drop commercial_facts while newer envelopes still carry the shape it discarded.
internal static class OrderReceiptSchemaVersions
{
    internal const int Legacy = 1;
    internal const int CustomerAttribution = 2;
    internal const int Commitment = 3;
    internal const int Commercial = 4;
    internal const int Quotation = 5;
    internal const int ProgramPolicy = 6;

    internal static readonly int[] All =
        [Legacy, CustomerAttribution, Commitment, Commercial, Quotation, ProgramPolicy];

    internal static string SqlLiteralList => string.Join(", ", All.Select(version => $"'{version}'"));
}
