using Application.Tenancy;
using Npgsql;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore(
    NpgsqlDataSource dataSource,
    TimeProvider? timeProvider = null,
    IOrderCommercialCommitGuard? commercialCommitGuard = null,
    IOrderProfilePolicySource? profilePolicySource = null)
    : IOrderDraftStore, IOrderDraftHistoryStore, IOrderDraftReceiptReader, IOrderProgramReferenceStore
{
    private const string CreateOperation = "create-order-draft";
    private const string AbandonOperation = "abandon-order-draft";
    private const string CommitOperation = "commit-order-draft";
    private const string ReviseOperation = "revise-order-draft";
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<CreateOrderDraftResult> CreateAsync(
        TenantContext tenantContext,
        OrderDraftIntent intent,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        await using var session = await OrderTenantDbSession
            .OpenAsync(dataSource, tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var existing = await FindReceiptAsync(
                session,
                tenantContext.TenantId,
                tenantContext.AccountId,
                CreateOperation,
                idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            var replayResult = await ToExistingResultAsync(session, tenantContext, existing, intent.Fingerprint, cancellationToken).ConfigureAwait(false);
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return replayResult;
        }

        var createdAt = _timeProvider.GetUtcNow();
        var order = new OrderDraftSnapshot(
            Guid.CreateVersion7(),
            tenantContext.TenantId,
            tenantContext.AccountId,
            intent.Summary,
            intent.CurrencyCode,
            intent.Total,
            Revision: 1,
            createdAt,
            intent.Lines,
            CustomerContext: intent.CustomerContext);

        if (profilePolicySource is not null)
            order = order with
            {
                ProgramPolicy = await profilePolicySource.ResolveActiveAsync(session.Connection, session.Transaction,
                tenantContext.TenantId, cancellationToken).ConfigureAwait(false) ?? throw new OrderProfileUnavailableException()
            };

        OrderCommercialFactsValidation.RequireValid(order);

        await InsertOrderAsync(session, order, cancellationToken).ConfigureAwait(false);
        await InsertLinesAsync(session, order, cancellationToken).ConfigureAwait(false);
        await InsertProgramPolicyAsync(session, order, cancellationToken).ConfigureAwait(false);

        if (await TryInsertReceiptAsync(
                session,
                tenantContext.AccountId,
                CreateOperation,
                idempotencyKey,
                intent.Fingerprint,
                order,
                createdAt,
                cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new CreateOrderDraftResult(CreateOrderDraftStatus.Created, order);
        }

        // A concurrent command won the receipt's unique key. This transaction's order and
        // lines are intentionally rolled back after reading the winner's durable outcome.
        existing = await FindReceiptAsync(
                session,
                tenantContext.TenantId,
                tenantContext.AccountId,
                CreateOperation,
                idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The order command receipt disappeared after a conflict.");
        var conflictResult = await ToExistingResultAsync(session, tenantContext, existing, intent.Fingerprint, cancellationToken).ConfigureAwait(false);
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return conflictResult;
    }

    public async Task<AbandonOrderDraftResult> AbandonAsync(
        TenantContext tenantContext,
        AbandonOrderDraftRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        await using var session = await OrderTenantDbSession
            .OpenAsync(dataSource, tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var receipt = await FindReceiptAsync(
            session, tenantContext.TenantId, tenantContext.AccountId,
            AbandonOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            var replayResult = await ToExistingAbandonResultAsync(session, tenantContext, receipt, fingerprint, cancellationToken).ConfigureAwait(false);
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return replayResult;
        }

        var abandonedAt = _timeProvider.GetUtcNow();
        var changed = await TryAbandonOrderAsync(
            session, tenantContext, request, abandonedAt, cancellationToken)
            .ConfigureAwait(false);
        if (!changed)
        {
            // A concurrent winner may have committed while the conditional UPDATE waited.
            receipt = await FindReceiptAsync(
                session, tenantContext.TenantId, tenantContext.AccountId,
                AbandonOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (receipt is not null)
            {
                var replayResult = await ToExistingAbandonResultAsync(session, tenantContext, receipt, fingerprint, cancellationToken).ConfigureAwait(false);
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return replayResult;
            }

            var current = await FindOrderStateAsync(
                session, tenantContext.TenantId, request.OrderId, cancellationToken)
                .ConfigureAwait(false);
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            var status = current is null
                ? AbandonOrderDraftStatus.NotFound
                : OrderDraftLifecycle.AssessAbandon(
                    current.Value.State, current.Value.Revision, request.ExpectedRevision);
            if (status == AbandonOrderDraftStatus.Abandoned)
            {
                throw new InvalidOperationException("The draft remained eligible after its conditional update failed.");
            }

            return new AbandonOrderDraftResult(
                status, null);
        }

        var order = await FindOrderAsync(
            session, tenantContext.TenantId, request.OrderId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The abandoned order disappeared before receipt creation.");
        await RequireAcceptedQuotationFactsAsync(tenantContext, order, cancellationToken).ConfigureAwait(false);
        if (await TryInsertReceiptAsync(
            session, tenantContext.AccountId, AbandonOperation,
            idempotencyKey, fingerprint, order, abandonedAt, cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new AbandonOrderDraftResult(AbandonOrderDraftStatus.Abandoned, order);
        }

        receipt = await FindReceiptAsync(
            session, tenantContext.TenantId, tenantContext.AccountId,
            AbandonOperation, idempotencyKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The order command receipt disappeared after a conflict.");
        var conflictResult = await ToExistingAbandonResultAsync(session, tenantContext, receipt, fingerprint, cancellationToken).ConfigureAwait(false);
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return conflictResult;
    }

    private static async Task<bool> TryAbandonOrderAsync(
        OrderTenantDbSession session,
        TenantContext tenantContext,
        AbandonOrderDraftRequest request,
        DateTimeOffset abandonedAt,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(OrderSql.TryAbandonOrder);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.AddWithValue("order_id", request.OrderId);
        command.Parameters.AddWithValue("expected_revision", request.ExpectedRevision);
        command.Parameters.AddWithValue("abandoned_at", abandonedAt);
        command.Parameters.AddWithValue("account_id", tenantContext.AccountId);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static async Task InsertOrderAsync(
        OrderTenantDbSession session,
        OrderDraftSnapshot order,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(OrderSql.InsertOrder);
        command.Parameters.AddWithValue("tenant_id", order.TenantId);
        command.Parameters.AddWithValue("id", order.OrderId);
        command.Parameters.AddWithValue("created_by_account_id", order.CreatedByAccountId);
        command.Parameters.AddWithValue("summary", order.Summary);
        command.Parameters.AddWithValue("currency_code", order.CurrencyCode);
        command.Parameters.AddWithValue("total", order.Total);
        command.Parameters.AddWithValue("revision", order.Revision);
        command.Parameters.AddWithValue("created_at", order.CreatedAt);
        command.Parameters.Add(new Npgsql.NpgsqlParameter("customer_organization_id", NpgsqlTypes.NpgsqlDbType.Uuid)
        {
            Value = (object?)order.CustomerContext?.OrganizationId ?? DBNull.Value,
        });
        command.Parameters.Add(new Npgsql.NpgsqlParameter("customer_program_id", NpgsqlTypes.NpgsqlDbType.Uuid)
        {
            Value = (object?)order.CustomerContext?.ProgramId ?? DBNull.Value,
        });
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertLinesAsync(
        OrderTenantDbSession session,
        OrderDraftSnapshot order,
        CancellationToken cancellationToken)
    {
        if (order.Lines.Count == 0)
        {
            return;
        }

        await using var batch = session.CreateBatch();
        foreach (var line in order.Lines)
        {
            var command = new NpgsqlBatchCommand(OrderSql.InsertLine);
            command.Parameters.AddWithValue("tenant_id", order.TenantId);
            command.Parameters.AddWithValue("order_id", order.OrderId);
            command.Parameters.AddWithValue("position", line.Position);
            command.Parameters.AddWithValue("description", line.Description);
            command.Parameters.AddWithValue("quantity", line.Quantity);
            command.Parameters.AddWithValue("unit_code", line.UnitCode);
            command.Parameters.AddWithValue("unit_price", line.UnitPrice);
            command.Parameters.AddWithValue("line_total", line.LineTotal);
            command.Parameters.Add(new NpgsqlParameter("commercial_facts", NpgsqlTypes.NpgsqlDbType.Jsonb)
            {
                Value = line.CommercialFacts is null ? DBNull.Value : System.Text.Json.JsonSerializer.Serialize(line.CommercialFacts, SnapshotJsonOptions),
            });
            batch.BatchCommands.Add(command);
        }
        await batch.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
