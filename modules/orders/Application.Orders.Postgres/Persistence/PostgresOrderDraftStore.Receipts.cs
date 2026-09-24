using System.Text.Json;

namespace Application.Orders.Postgres;

public sealed partial class PostgresOrderDraftStore
{
    private const int LegacyReceiptSchemaVersion = 1;
    private const int CustomerAttributionReceiptSchemaVersion = 2;
    private const string CreateResultType = "order-draft-created";
    private const string AbandonResultType = "order-draft-abandoned";
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
                order.CustomerContext is null
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

    private static AbandonOrderDraftResult ToExistingAbandonResult(
        OrderCommandReceipt receipt,
        string requestedFingerprint)
    {
        if (!string.Equals(receipt.Fingerprint, requestedFingerprint, StringComparison.Ordinal))
        {
            return new AbandonOrderDraftResult(AbandonOrderDraftStatus.IdempotencyKeyConflict, null);
        }

        var order = ReadReceiptSnapshot(receipt, AbandonOperation);
        return new AbandonOrderDraftResult(AbandonOrderDraftStatus.Replayed, order);
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
                    || schemaVersion is not (LegacyReceiptSchemaVersion or CustomerAttributionReceiptSchemaVersion)
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
                    requireCustomerContext: schemaVersion == CustomerAttributionReceiptSchemaVersion);
            }

            return DeserializeSnapshot(
                root,
                receipt,
                expectedOperation,
                requireLifecycleFields: false,
                requireCustomerContext: false);
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
        bool requireCustomerContext)
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
        if (hasCustomerContext != requireCustomerContext)
        {
            throw InvalidReceipt();
        }

        var order = element.Deserialize<OrderDraftSnapshot>(SnapshotJsonOptions)
            ?? throw InvalidReceipt();
        var expectedState = expectedOperation switch
        {
            CreateOperation => OrderDraftState.Draft,
            AbandonOperation => OrderDraftState.Abandoned,
            _ => throw InvalidReceipt(),
        };
        if (order.OrderId != receipt.OrderId
            || order.TenantId != receipt.TenantId
            || order.CreatedByAccountId == Guid.Empty
            || order.Lines is null
            || order.Revision < 1
            || order.State != expectedState
            || (order.CustomerContext is { } customerContext
                && (customerContext.OrganizationId == Guid.Empty
                    || customerContext.ProgramId == Guid.Empty))
            || (expectedState == OrderDraftState.Draft
                && (order.AbandonedAt is not null || order.AbandonedByAccountId is not null))
            || (expectedState == OrderDraftState.Abandoned
                && (order.AbandonedAt is null || order.AbandonedByAccountId is null)))
        {
            throw InvalidReceipt();
        }

        return order;
    }

    private static InvalidOperationException InvalidReceipt() =>
        new("The order command receipt has an unsupported or invalid response.");

    private static string ResultTypeForOperation(string operation) => operation switch
    {
        CreateOperation => CreateResultType,
        AbandonOperation => AbandonResultType,
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
