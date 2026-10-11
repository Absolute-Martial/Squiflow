using Application.Catalog;
using Application.Catalog.Postgres;
using Application.Fulfillment;
using Application.Fulfillment.Postgres;
using Application.Orders.Postgres;
using Application.Tenancy;
using Npgsql;

namespace Application.CoreApi.Composition;

internal sealed class FulfillmentSourceReader(
    PostgresOrderDraftStore orders) : IFulfillmentStartSource
{
    public async Task<FulfillmentSourceFacts?> ResolveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantContext tenantContext,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentOutOfRangeException.ThrowIfEqual(orderId, Guid.Empty);

        await CatalogPublicationPin.AcquireSharedAsync(
            connection,
            transaction,
            tenantContext.TenantId,
            cancellationToken).ConfigureAwait(false);

        var order = await orders.ReadWorkSourceInTransactionAsync(connection, transaction, tenantContext, orderId,
            cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return null;
        }

        if (order.TenantId != tenantContext.TenantId || order.OrderId != orderId)
        {
            throw new InvalidOperationException("The order store returned another tenant's or order's facts.");
        }

        var catalogFacts = order.Catalog;
        var item = catalogFacts is null
            ? null
            : await CatalogItemTransactionReader.ReadAsync(connection, transaction, tenantContext, catalogFacts.ItemId,
                cancellationToken).ConfigureAwait(false);
        if (item is not null && (item.TenantId != tenantContext.TenantId || item.ItemId != catalogFacts!.ItemId))
        {
            throw new InvalidOperationException("The catalog store returned another tenant's item facts.");
        }

        var conversion = catalogFacts?.Conversion;
        return new FulfillmentSourceFacts(
            order.TenantId,
            order.OrderId,
            order.OrderRevision,
            order.ProgramId,
            order.LinePosition,
            catalogFacts?.ItemId ?? Guid.Empty,
            catalogFacts?.UnitId ?? Guid.Empty,
            catalogFacts?.UnitCode ?? string.Empty,
            catalogFacts?.UnitPrecision ?? 0,
            order.OrderedQuantity,
            order.IsCommitted,
            order.LineCount,
            catalogFacts is not null,
            item?.StockMode == CatalogStockMode.NonStock,
            conversion is not null &&
                (conversion.SourceUnitId != conversion.TargetUnitId || conversion.Numerator != conversion.Denominator),
            item?.Kind == CatalogItemKind.Service);
    }
}
