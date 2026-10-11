using Application.Catalog;
using Application.Catalog.Postgres;
using Application.Orders.Postgres;
using Application.Purchasing;
using Application.Purchasing.Postgres;
using Application.Tenancy;
using Npgsql;

namespace Application.CoreApi.Composition;

internal sealed class PurchasingSourceReader : IPurchasingSourceReader
{
    public async Task<PurchasingSourceFacts?> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, PurchasingMutation mutation, CancellationToken cancellationToken)
    {
        await CatalogPublicationPin.AcquireSharedAsync(connection, transaction, context.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (mutation.UnitId is not { } unitId) return null;
        var unit = await CatalogUnitTransactionReader.ReadAsync(connection, transaction, context, unitId, cancellationToken)
            .ConfigureAwait(false);
        if (unit is null) return null;
        if (unit.TenantId != context.TenantId || unit.UnitId != unitId)
            throw new InvalidOperationException("Catalog returned another resource's facts.");
        PurchasingOrderFacts? orderFacts = null;
        if (mutation.Order is { } link)
        {
            var reference = await CommittedOrderLineTransactionReader.ReadAsync(connection, transaction, context,
                link.OrderId, link.LinePosition, link.OrderRevision, cancellationToken).ConfigureAwait(false);
            if (reference is null) return null;
            if (reference.TenantId != context.TenantId || reference.OrderId != link.OrderId ||
                reference.OrderRevision != link.OrderRevision || reference.LinePosition != link.LinePosition)
                throw new InvalidOperationException("Orders returned another resource's facts.");
            orderFacts = new(context.TenantId, reference.OrderId, reference.LinePosition, reference.OrderRevision, true);
        }
        return new(new(context.TenantId, unit.UnitId, unit.Code, unit.Precision, unit.Revision,
            unit.Status == CatalogEntityStatus.Active), orderFacts);
    }
}
