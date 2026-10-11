using System.Data;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Purchasing.Postgres;

public sealed partial class PostgresPurchasingStore
{
    public async Task<SupplierSnapshot?> FindSupplierAsync(TenantContext context, Guid supplierId, CancellationToken cancellationToken)
    {
        await RequireViewAsync(context, cancellationToken).ConfigureAwait(false);
        RequireId(supplierId);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        return await ReadSupplierAsync(connection, transaction, context.TenantId, supplierId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PurchaseSnapshot?> FindPurchaseAsync(TenantContext context, Guid purchaseId, CancellationToken cancellationToken)
    {
        await RequireViewAsync(context, cancellationToken).ConfigureAwait(false);
        RequireId(purchaseId);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        return await ReadPurchaseAsync(connection, transaction, context.TenantId, purchaseId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SupplierPage> ListSuppliersAsync(TenantContext context, PurchasingListRequest request, CancellationToken cancellationToken)
    {
        await RequireViewAsync(context, cancellationToken).ConfigureAwait(false);
        RequireList(request);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        await using var command = ListCommand(connection, transaction, "ListSuppliers", context.TenantId, request);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<SupplierSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) items.Add(ReadSupplier(reader, context.TenantId));
        var hasMore = items.Count > request.Limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new(items.AsReadOnly(), hasMore ? new(items[^1].CreatedAt, items[^1].SupplierId) : null);
    }

    public async Task<PurchasingPage> ListPurchasesAsync(TenantContext context, PurchasingListRequest request, CancellationToken cancellationToken)
    {
        await RequireViewAsync(context, cancellationToken).ConfigureAwait(false);
        RequireList(request);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        await using var command = ListCommand(connection, transaction, "ListPurchases", context.TenantId, request);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<PurchaseSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) items.Add(ReadPurchase(reader, context.TenantId));
        var hasMore = items.Count > request.Limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new(items.AsReadOnly(), hasMore ? new(items[^1].CreatedAt, items[^1].PurchaseId) : null);
    }

    public async Task<PurchasingHistoryPage?> HistoryAsync(TenantContext context, Guid purchaseId,
        PurchasingHistoryRequest request, CancellationToken cancellationToken)
    {
        await RequireViewAsync(context, cancellationToken).ConfigureAwait(false);
        RequireHistory(purchaseId, request);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        var current = await ReadPurchaseAsync(connection, transaction, context.TenantId, purchaseId, cancellationToken).ConfigureAwait(false);
        if (current is null) return null;
        await using var command = HistoryCommand(connection, transaction, "PurchaseHistory", context.TenantId, purchaseId, request);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<PurchasingEvent>();
        var expected = Math.Min(current.Revision, (request.BeforeRevision ?? checked(current.Revision + 1)) - 1);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var entry = ReadPurchaseEvent(reader, context.TenantId, purchaseId);
            var historical = Deserialize<PurchaseSnapshot>(reader.GetString(7));
            if (entry.Revision != expected-- || historical.CreatedAt != current.CreatedAt ||
                historical.CreatedByAccountId != current.CreatedByAccountId || !Same(Source(historical), Source(current))) throw InvalidFacts();
            items.Add(entry);
        }
        if (items.Count <= request.Limit && expected != 0) throw InvalidFacts();
        var hasMore = items.Count > request.Limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new(current.Revision, items.AsReadOnly(), hasMore ? items[^1].Revision : null);
    }

    public async Task<SupplierHistoryPage?> SupplierHistoryAsync(TenantContext context, Guid supplierId,
        PurchasingHistoryRequest request, CancellationToken cancellationToken)
    {
        await RequireViewAsync(context, cancellationToken).ConfigureAwait(false);
        RequireHistory(supplierId, request);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        var current = await ReadSupplierAsync(connection, transaction, context.TenantId, supplierId, cancellationToken).ConfigureAwait(false);
        if (current is null) return null;
        await using var command = HistoryCommand(connection, transaction, "SupplierHistory", context.TenantId, supplierId, request);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<SupplierEvent>();
        var expected = Math.Min(current.Revision, (request.BeforeRevision ?? checked(current.Revision + 1)) - 1);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var entry = ReadSupplierEvent(reader, context.TenantId, supplierId);
            if (entry.Revision != expected-- || entry.Supplier.CreatedAt != current.CreatedAt ||
                entry.Supplier.CreatedByAccountId != current.CreatedByAccountId) throw InvalidFacts();
            items.Add(entry);
        }
        if (items.Count <= request.Limit && expected != 0) throw InvalidFacts();
        var hasMore = items.Count > request.Limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new(current.Revision, items.AsReadOnly(), hasMore ? items[^1].Revision : null);
    }

    public async Task<SupplierBalance?> SupplierBalanceAsync(TenantContext context, Guid supplierId, CancellationToken cancellationToken)
    {
        await RequireViewAsync(context, cancellationToken).ConfigureAwait(false);
        RequireId(supplierId);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        if (await ReadSupplierAsync(connection, transaction, context.TenantId, supplierId, cancellationToken).ConfigureAwait(false) is null) return null;
        await using var command = Command(connection, transaction, "SupplierBalance", ("tenant", context.TenantId), ("supplier", supplierId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            decimal cost = 0m, paid = 0m, refunded = 0m, outstanding = 0m;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var purchase = ReadPurchase(reader, context.TenantId);
                cost = checked(cost + purchase.TotalCost);
                paid = checked(paid + purchase.PaidAmount);
                refunded = checked(refunded + purchase.RefundedAmount);
                outstanding = checked(outstanding + purchase.OutstandingPayable);
            }
            return new(supplierId, "NPR", cost, paid, refunded, outstanding);
        }
        catch (OverflowException) { throw InvalidFacts(); }
    }

    private async Task RequireViewAsync(TenantContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var authority = await authorityReader.ResolveAsync(context, ct).ConfigureAwait(false);
        if (!authority.CanView || authority.AuthorizationRevision < 1) throw new PurchasingAccessDeniedException();
    }

    private static void RequireId(Guid id)
    {
        if (id == Guid.Empty) throw new PurchasingValidationException("purchasing_request_invalid");
    }

    private static void RequireList(PurchasingListRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Limit is < 1 or > 50 || request.After is { } cursor &&
            (cursor.CreatedAt == default || cursor.CreatedAt.Offset != TimeSpan.Zero || cursor.Id == Guid.Empty))
            throw new PurchasingValidationException("purchasing_page_invalid");
    }

    private static void RequireHistory(Guid id, PurchasingHistoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireId(id);
        if (request.Limit is < 1 or > 10 || request.BeforeRevision is < 2)
            throw new PurchasingValidationException("purchasing_history_invalid");
    }

    private static NpgsqlCommand ListCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, string name,
        Guid tenant, PurchasingListRequest request)
    {
        var command = Command(connection, transaction, name, ("tenant", tenant), ("limit", request.Limit + 1));
        command.Parameters.Add(new NpgsqlParameter("after_at", NpgsqlDbType.TimestampTz) { Value = (object?)request.After?.CreatedAt ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("after_id", NpgsqlDbType.Uuid) { Value = (object?)request.After?.Id ?? DBNull.Value });
        return command;
    }

    private static NpgsqlCommand HistoryCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, string name,
        Guid tenant, Guid id, PurchasingHistoryRequest request)
    {
        var command = Command(connection, transaction, name, ("tenant", tenant), ("id", id), ("limit", request.Limit + 1));
        command.Parameters.Add(new NpgsqlParameter("before", NpgsqlDbType.Bigint) { Value = (object?)request.BeforeRevision ?? DBNull.Value });
        return command;
    }
}
