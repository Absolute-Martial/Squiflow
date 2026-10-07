using System.Text.Json;
using Application.Tenancy;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    private const int CommercialReceiptSchemaVersion = 4;
    private const int QuotationReceiptSchemaVersion = 5;
    public async Task<CreateOrderDraftResult?> FindCreateReceiptAsync(Application.Tenancy.TenantContext context,
        string key, string fingerprint, CancellationToken ct)
    {
        await using var session = await OrderTenantDbSession.OpenAsync(dataSource, context.TenantId, ct).ConfigureAwait(false);
        var receipt = await FindReceiptAsync(session, context.TenantId, context.AccountId, CreateOperation, key, ct).ConfigureAwait(false);
        await session.CommitAsync(ct).ConfigureAwait(false);
        return receipt is null ? null : ToExistingResult(receipt, fingerprint);
    }

    public async Task<ReviseOrderDraftResult?> FindRevisionReceiptAsync(Application.Tenancy.TenantContext context,
        string key, string fingerprint, CancellationToken ct)
    {
        await using var session = await OrderTenantDbSession.OpenAsync(dataSource, context.TenantId, ct).ConfigureAwait(false);
        var receipt = await FindReceiptAsync(session, context.TenantId, context.AccountId, ReviseOperation, key, ct).ConfigureAwait(false);
        await session.CommitAsync(ct).ConfigureAwait(false);
        return receipt is null ? null : ToExistingReviseResult(receipt, fingerprint);
    }
    private const int CommitmentReceiptSchemaVersion = 3;
    private const string CommitResultType = "order-committed";
    private const int LegacyReceiptSchemaVersion = 1;
    private const int CustomerAttributionReceiptSchemaVersion = 2;
    private const string CreateResultType = "order-draft-created";
    private const string AbandonResultType = "order-draft-abandoned";
    private const string ReviseResultType = "order-draft-revised";
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task<bool> TryInsertReceiptAsync(
        OrderTenantDbSession session,
        Guid accountId,
        string operation,
        string idempotencyKey,
        string fingerprint,
        OrderDraftSnapshot order,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(OrderSql.InsertReceipt);
        command.Parameters.AddWithValue("tenant_id", order.TenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("order_id", order.OrderId);
        command.Parameters.AddWithValue("response_json", JsonSerializer.Serialize(
            new OrderReceiptEnvelope(
                order.QuotationOrigin is not null ? QuotationReceiptSchemaVersion :
                order.Lines.Any(line => line.CommercialFacts is not null) ? CommercialReceiptSchemaVersion :
                operation == CommitOperation ? CommitmentReceiptSchemaVersion : order.CustomerContext is null
                    ? LegacyReceiptSchemaVersion
                    : CustomerAttributionReceiptSchemaVersion,
                operation,
                ResultTypeForOperation(operation),
                order),
            SnapshotJsonOptions));
        command.Parameters.AddWithValue("created_at", createdAt);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static async Task<OrderCommandReceipt?> FindReceiptAsync(
        OrderTenantDbSession session,
        Guid tenantId,
        Guid accountId,
        string operation,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(OrderSql.FindReceipt);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new OrderCommandReceipt(
            reader.GetGuid(reader.GetOrdinal("tenant_id")),
            reader.GetGuid(reader.GetOrdinal("order_id")),
            reader.GetString(reader.GetOrdinal("fingerprint")),
            reader.GetString(reader.GetOrdinal("response_json")));
    }

    private static CreateOrderDraftResult ToExistingResult(
        OrderCommandReceipt receipt,
        string requestedFingerprint)
    {
        if (!string.Equals(receipt.Fingerprint, requestedFingerprint, StringComparison.Ordinal))
        {
            return new CreateOrderDraftResult(CreateOrderDraftStatus.IdempotencyKeyConflict, null);
        }

        var order = ReadReceiptSnapshot(receipt, CreateOperation);
        return new CreateOrderDraftResult(CreateOrderDraftStatus.Replayed, order);
    }

    private async Task<AbandonOrderDraftResult> ToExistingAbandonResultAsync(
        TenantContext context,
        OrderCommandReceipt receipt,
        string requestedFingerprint,
        CancellationToken ct)
    {
        if (!string.Equals(receipt.Fingerprint, requestedFingerprint, StringComparison.Ordinal))
        {
            return new AbandonOrderDraftResult(AbandonOrderDraftStatus.IdempotencyKeyConflict, null);
        }

        var order = ReadReceiptSnapshot(receipt, AbandonOperation);
        await RequireAcceptedQuotationFactsAsync(context, order, ct).ConfigureAwait(false);
        return new AbandonOrderDraftResult(AbandonOrderDraftStatus.Replayed, order);
    }

    private static ReviseOrderDraftResult ToExistingReviseResult(
        OrderCommandReceipt receipt,
        string requestedFingerprint)
    {
        if (!string.Equals(receipt.Fingerprint, requestedFingerprint, StringComparison.Ordinal))
        {
            return new ReviseOrderDraftResult(ReviseOrderDraftStatus.IdempotencyKeyConflict, null);
        }

        return new ReviseOrderDraftResult(
            ReviseOrderDraftStatus.Replayed,
            ReadReceiptSnapshot(receipt, ReviseOperation));
    }

    private async Task<CommitOrderDraftResult> ToExistingCommitResultAsync(TenantContext context, OrderCommandReceipt receipt,
        string fingerprint, CancellationToken ct)
    {
        if (!string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal))
            return new(CommitOrderDraftStatus.IdempotencyKeyConflict, null);
        var order = ReadReceiptSnapshot(receipt, CommitOperation);
        await RequireAcceptedQuotationFactsAsync(context, order, ct).ConfigureAwait(false);
        return new(CommitOrderDraftStatus.Replayed, order);
    }

    private static OrderDraftSnapshot ReadReceiptSnapshot(
        OrderCommandReceipt receipt,
        string expectedOperation)
    {
        try
        {
            using var document = JsonDocument.Parse(receipt.ResponseJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw InvalidReceipt();
            }

            // Existing receipts contain the snapshot directly. Any envelope marker opts into
            // strict version and operation checks, including partially written future shapes.
            var hasVersion = root.TryGetProperty("schemaVersion", out var version);
            var hasOperation = root.TryGetProperty("operation", out var operation);
            var hasResultType = root.TryGetProperty("resultType", out var resultType);
            var hasPayload = root.TryGetProperty("payload", out var payload);
            var isEnvelope = hasVersion || hasOperation || hasResultType || hasPayload;
            if (isEnvelope)
            {
                if (version.ValueKind != JsonValueKind.Number
                    || !version.TryGetInt32(out var schemaVersion)
                    || schemaVersion is not (LegacyReceiptSchemaVersion or CustomerAttributionReceiptSchemaVersion or CommitmentReceiptSchemaVersion or CommercialReceiptSchemaVersion or QuotationReceiptSchemaVersion)
                    || (schemaVersion is not (CommercialReceiptSchemaVersion or QuotationReceiptSchemaVersion) && (schemaVersion == CommitmentReceiptSchemaVersion) != (expectedOperation == CommitOperation))
                    || (expectedOperation == QuotationCreateOperation && schemaVersion != QuotationReceiptSchemaVersion)
                    || (schemaVersion == QuotationReceiptSchemaVersion && expectedOperation is not (QuotationCreateOperation or CommitOperation or AbandonOperation))
                    || operation.ValueKind != JsonValueKind.String
                    || !string.Equals(operation.GetString(), expectedOperation, StringComparison.Ordinal)
                    || resultType.ValueKind != JsonValueKind.String
                    || !string.Equals(
                        resultType.GetString(), ResultTypeForOperation(expectedOperation), StringComparison.Ordinal)
                    || payload.ValueKind != JsonValueKind.Object)
                {
                    throw InvalidReceipt();
                }

                return DeserializeSnapshot(
                    payload,
                    receipt,
                    expectedOperation,
                    requireLifecycleFields: true,
                    requireCustomerContext: schemaVersion is CommitmentReceiptSchemaVersion or CommercialReceiptSchemaVersion or QuotationReceiptSchemaVersion
                        ? null : schemaVersion == CustomerAttributionReceiptSchemaVersion,
                    requireCommercialFacts: schemaVersion == QuotationReceiptSchemaVersion ? null : schemaVersion == CommercialReceiptSchemaVersion,
                    requireQuotationOrigin: schemaVersion == QuotationReceiptSchemaVersion);
            }

            if (expectedOperation is CommitOperation or QuotationCreateOperation)
                throw InvalidReceipt();
            return DeserializeSnapshot(
                root,
                receipt,
                expectedOperation,
                requireLifecycleFields: false,
                requireCustomerContext: false,
                requireCommercialFacts: false);
        }
        catch (JsonException)
        {
            throw InvalidReceipt();
        }
    }

    private static OrderDraftSnapshot DeserializeSnapshot(
        JsonElement element,
        OrderCommandReceipt receipt,
        string expectedOperation,
        bool requireLifecycleFields,
        bool? requireCustomerContext,
        bool? requireCommercialFacts,
        bool requireQuotationOrigin = false)
    {
        foreach (var property in new[]
                 {
                     "orderId", "tenantId", "createdByAccountId", "summary", "currencyCode",
                     "total", "revision", "createdAt", "lines",
                 })
        {
            if (!element.TryGetProperty(property, out _))
            {
                throw InvalidReceipt();
            }
        }

        if (requireLifecycleFields && !element.TryGetProperty("state", out _))
        {
            throw InvalidReceipt();
        }

        var hasCustomerContext = element.TryGetProperty("customerContext", out var customerContextElement)
            && customerContextElement.ValueKind == JsonValueKind.Object;
        if (requireCustomerContext is { } required && hasCustomerContext != required)
        {
            throw InvalidReceipt();
        }

        var order = element.Deserialize<OrderDraftSnapshot>(SnapshotJsonOptions)
            ?? throw InvalidReceipt();
        var expectedState = expectedOperation switch
        {
            CreateOperation => OrderDraftState.Draft,
            QuotationCreateOperation => OrderDraftState.Draft,
            ReviseOperation => OrderDraftState.Draft,
            AbandonOperation => OrderDraftState.Abandoned,
            CommitOperation => OrderDraftState.Committed,
            _ => throw InvalidReceipt(),
        };
        if (order.OrderId != receipt.OrderId
            || order.TenantId != receipt.TenantId
            || order.CreatedByAccountId == Guid.Empty
            || order.Lines is null || order.Lines.Any(line => line is null)
            || order.Revision < 1
            || order.State != expectedState
            || (order.CustomerContext is { } customerContext
                && (customerContext.OrganizationId == Guid.Empty
                    || customerContext.ProgramId == Guid.Empty))
            || (expectedState != OrderDraftState.Committed
                && (order.CommittedAt is not null || order.CommittedByAccountId is not null))
            || (expectedState == OrderDraftState.Committed
                && (order.Revision < 2 || order.CommittedAt is null || order.CommittedByAccountId is null
                    || order.CommittedByAccountId == Guid.Empty))
            || (expectedState != OrderDraftState.Abandoned
                && (order.AbandonedAt is not null || order.AbandonedByAccountId is not null))
            || (expectedState == OrderDraftState.Abandoned
                && (order.AbandonedAt is null || order.AbandonedByAccountId is null)))
        {
            throw InvalidReceipt();
        }

        if (requireCommercialFacts is { } commercial && order.Lines.Any(line => line.CommercialFacts is not null) != commercial ||
            (order.QuotationOrigin is not null) != requireQuotationOrigin)
            throw InvalidReceipt();

        if (order.QuotationOrigin is { } origin)
            new AcceptedQuotationOrder(origin, order.Summary, order.CurrencyCode, order.Total, order.Lines, order.CustomerContext).RequireValid(order.TenantId);

        OrderCommercialFactsValidation.RequireValid(order);
        return order;
    }

    private static InvalidOperationException InvalidReceipt() =>
        new("The order command receipt has an unsupported or invalid response.");

    private static string ResultTypeForOperation(string operation) => operation switch
    {
        CreateOperation => CreateResultType,
        QuotationCreateOperation => "order-draft-created-from-quotation",
        ReviseOperation => ReviseResultType,
        AbandonOperation => AbandonResultType,
        CommitOperation => CommitResultType,
        _ => throw InvalidReceipt(),
    };

    private sealed record OrderReceiptEnvelope(
        int SchemaVersion,
        string Operation,
        string ResultType,
        OrderDraftSnapshot Payload);

    private sealed record OrderCommandReceipt(
        Guid TenantId,
        Guid OrderId,
        string Fingerprint,
        string ResponseJson);
}
