using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class CustomerSettlementRoutes
{
    internal const int MaximumRequestBodyBytes = 16384;
    private const string Root = "/api/v1/tenants/{tenantId:guid}";
    private const string Payments = Root + "/customer-payments";
    private const string Payment = Payments + "/{paymentId:guid}";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 6,
        NumberHandling = JsonNumberHandling.Strict,
    };

    internal static WebApplication MapCustomerSettlementRoutes(this WebApplication app)
    {
        Protect(app.MapGet(Root + "/receivables/{invoiceId:guid}", (Guid tenantId, Guid invoiceId, HttpContext http, ICustomerSettlementStore store) =>
            GuardAsync(async () =>
            {
                RequireQuery(http);
                return Project(await store.ReadReceivableAsync(Context(tenantId, http), invoiceId, http.RequestAborted).ConfigureAwait(false), value => Receivable(value, tenantId, invoiceId));
            })), EndpointAccess.FinancialView).Produces<ReceivableResponse>();
        Protect(app.MapGet(Root + "/receivables", (Guid tenantId, HttpContext http, ICustomerSettlementStore store) =>
            GuardAsync(async () =>
            {
                var filter = ListQuery(http);
                return Project(await store.BrowseReceivablesAsync(Context(tenantId, http), filter.Debtor, filter.Cursor, http.RequestAborted).ConfigureAwait(false),
                    page => new PageResponse<ReceivableResponse>(page.Items.Select(value => Receivable(value, tenantId)).ToArray(), page.NextCursor));
            })), EndpointAccess.FinancialView).Produces<PageResponse<ReceivableResponse>>();
        Protect(app.MapGet(Payments, (Guid tenantId, HttpContext http, ICustomerSettlementStore store) =>
            GuardAsync(async () =>
            {
                var filter = ListQuery(http);
                return Project(await store.BrowsePaymentsAsync(Context(tenantId, http), filter.Debtor, filter.Cursor, http.RequestAborted).ConfigureAwait(false),
                    page => new PageResponse<PaymentResponse>(page.Items.Select(value => PaymentFacts(value, tenantId)).ToArray(), page.NextCursor));
            })), EndpointAccess.FinancialView).Produces<PageResponse<PaymentResponse>>();
        Protect(app.MapGet(Payment, (Guid tenantId, Guid paymentId, HttpContext http, ICustomerSettlementStore store) =>
            GuardAsync(async () =>
            {
                RequireQuery(http);
                return Project(await store.ReadPaymentAsync(Context(tenantId, http), paymentId, http.RequestAborted).ConfigureAwait(false), value => PaymentFacts(value, tenantId, paymentId));
            })), EndpointAccess.FinancialView).Produces<PaymentResponse>();
        Protect(app.MapGet(Payment + "/history", (Guid tenantId, Guid paymentId, HttpContext http, ICustomerSettlementStore store) =>
            GuardAsync(async () =>
            {
                RequireQuery(http, "cursor");
                return Project(await store.ReadHistoryAsync(Context(tenantId, http), paymentId, QueryValue(http, "cursor"), http.RequestAborted).ConfigureAwait(false),
                    page => new PageResponse<PaymentEventResponse>(page.Items.Select(value => Event(value, paymentId)).ToArray(), page.NextCursor));
            })), EndpointAccess.FinancialView).Produces<PageResponse<PaymentEventResponse>>();
        Protect(app.MapGet(Root + "/debtors/{kind}/{debtorId:guid}/balance", (Guid tenantId, string kind, Guid debtorId, HttpContext http, ICustomerSettlementStore store) =>
            GuardAsync(async () =>
            {
                RequireQuery(http);
                var debtor = Debtor(kind, debtorId);
                return Project(await store.ReadBalanceAsync(Context(tenantId, http), debtor, http.RequestAborted).ConfigureAwait(false),
                    value => Balance(value, debtor));
            })), EndpointAccess.FinancialView).Produces<BalanceResponse>();

        Protect(app.MapPut(Payment, (Guid tenantId, Guid paymentId, HttpContext http, ICustomerSettlementStore store) =>
            GuardAsync(async () =>
            {
                RequireQuery(http);
                var key = Key(http);
                var body = await PayloadAsync<RecordPayload>(http).ConfigureAwait(false);
                var channel = body.Channel switch { "Cash" => CustomerPaymentChannel.Cash, "Bank" => CustomerPaymentChannel.Bank, _ => throw new FinancialValidationException() };
                var result = await store.RecordAsync(Context(tenantId, http), paymentId,
                    new(Debtor(body.DebtorKind, body.DebtorId), body.CurrencyCode, body.Amount, channel, body.Evidence, body.ExternalReference),
                    key, http.RequestAborted).ConfigureAwait(false);
                return CommandResult(http, tenantId, paymentId, result, true);
            })), EndpointAccess.CustomerPaymentRecord).Accepts<RecordPayload>("application/json")
            .Produces<CommandResponse>(201).Produces<CommandResponse>(200);

        foreach (var (path, operation) in new[]
        {
            ("confirm", CustomerPaymentOperation.Confirm), ("reject", CustomerPaymentOperation.Reject),
            ("dispute", CustomerPaymentOperation.Dispute), ("reverse", CustomerPaymentOperation.Reverse),
        })
        {
            var route = Protect(app.MapPost(Payment + "/" + path, (Guid tenantId, Guid paymentId, HttpContext http, ICustomerSettlementStore store) =>
                GuardAsync(async () =>
                {
                    RequireQuery(http);
                    var key = Key(http);
                    CustomerPaymentMutation mutation;
                    if (operation == CustomerPaymentOperation.Reverse)
                    {
                        var body = await PayloadAsync<ReasonPayload>(http).ConfigureAwait(false);
                        mutation = new(operation, body.ExpectedRevision, body.Reason);
                    }
                    else
                    {
                        var body = await PayloadAsync<EvidencePayload>(http).ConfigureAwait(false);
                        mutation = new(operation, body.ExpectedRevision, body.Evidence);
                    }
                    return CommandResult(http, tenantId, paymentId, await store.ExecuteAsync(Context(tenantId, http), paymentId, mutation,
                        key, http.RequestAborted).ConfigureAwait(false), false);
                })), operation == CustomerPaymentOperation.Reverse ? EndpointAccess.CustomerSettlementCorrect : EndpointAccess.CustomerPaymentReconcile)
                .Produces<CommandResponse>();
            if (operation == CustomerPaymentOperation.Reverse) route.Accepts<ReasonPayload>("application/json");
            else route.Accepts<EvidencePayload>("application/json");
        }
        Protect(app.MapPost(Payment + "/allocations", (Guid tenantId, Guid paymentId, HttpContext http, ICustomerSettlementStore store) =>
            GuardAsync(async () =>
            {
                RequireQuery(http);
                var key = Key(http);
                var body = await PayloadAsync<AllocationPayload>(http).ConfigureAwait(false);
                return CommandResult(http, tenantId, paymentId, await store.ExecuteAsync(Context(tenantId, http), paymentId,
                    new(CustomerPaymentOperation.Allocate, body.ExpectedRevision, ReceivableId: body.ReceivableId, Amount: body.Amount),
                    key, http.RequestAborted).ConfigureAwait(false), false);
            })), EndpointAccess.CustomerPaymentAllocate).Accepts<AllocationPayload>("application/json").Produces<CommandResponse>();
        Protect(app.MapPost(Payment + "/allocations/{allocationId:guid}/release", (Guid tenantId, Guid paymentId, Guid allocationId, HttpContext http, ICustomerSettlementStore store) =>
            GuardAsync(async () =>
            {
                RequireQuery(http);
                var key = Key(http);
                var body = await PayloadAsync<ReasonPayload>(http).ConfigureAwait(false);
                return CommandResult(http, tenantId, paymentId, await store.ExecuteAsync(Context(tenantId, http), paymentId,
                    new(CustomerPaymentOperation.Release, body.ExpectedRevision, body.Reason, AllocationId: allocationId),
                    key, http.RequestAborted).ConfigureAwait(false), false);
            })), EndpointAccess.CustomerSettlementCorrect).Accepts<ReasonPayload>("application/json").Produces<CommandResponse>();
        return app;
    }

    private static Application.Tenancy.TenantContext Context(Guid tenantId, HttpContext http) => TenantRequestAccess.RequireResolvedContext(tenantId, http);

    private static RouteHandlerBuilder Protect(RouteHandlerBuilder route, EndpointAccess access) => route.RequireAuthorization()
        .WithCoreApiAccess(access).WithCoreApiApplicationAuthorization(access).WithTags("Customer settlement")
        .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestBodyBytes))
        .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
        .ProducesProblem(413).ProducesProblem(429).ProducesProblem(500).ProducesProblem(503).ProducesProblem(504);

    private static IResult Project<T, TResponse>(FinancialResult<T> result, Func<T, TResponse> project) where T : class =>
        result.Status == FinancialStatus.Found && result.Value is { } value ? Results.Ok(project(value)) : Failure(result.Status);

    private static IResult CommandResult(HttpContext http, Guid tenantId, Guid paymentId, FinancialResult<FinancialCommandReceipt> result, bool created)
    {
        if (result.Status is not (FinancialStatus.Applied or FinancialStatus.Replayed)) return Failure(result.Status);
        var value = result.Value ?? throw new InvalidOperationException("The financial command result is missing.");
        if (value.PaymentId != paymentId || value.EventId == Guid.Empty || value.Revision < 1)
            throw new InvalidOperationException("The financial command returned another resource's receipt.");
        var response = new CommandResponse(value.PaymentId, value.EventId, value.Revision);
        if (result.Status == FinancialStatus.Replayed) http.Response.Headers.Append("Idempotency-Replayed", "true");
        return created && result.Status == FinancialStatus.Applied
            ? Results.Created($"/api/v1/tenants/{tenantId:D}/customer-payments/{value.PaymentId:D}", response) : Results.Ok(response);
    }

    private static FinancialDebtor Debtor(string kind, Guid id) => new(kind switch
    {
        "Organization" => InvoiceDebtorKind.Organization,
        "Program" => InvoiceDebtorKind.Program,
        "Individual" => InvoiceDebtorKind.Individual,
        _ => throw new FinancialValidationException(),
    }, id);

    private static (FinancialDebtor? Debtor, string? Cursor) ListQuery(HttpContext http)
    {
        RequireQuery(http, "debtorKind", "debtorId", "cursor");
        var kind = QueryValue(http, "debtorKind");
        var rawId = QueryValue(http, "debtorId");
        if (kind is null && rawId is null) return (null, QueryValue(http, "cursor"));
        if (kind is null || !Guid.TryParseExact(rawId, "D", out var id) || id == Guid.Empty) throw new FinancialValidationException();
        return (Debtor(kind, id), QueryValue(http, "cursor"));
    }

    private static void RequireQuery(HttpContext http, params string[] allowed)
    {
        if (http.Request.Query.Any(pair => pair.Value.Count != 1 || !allowed.Contains(pair.Key, StringComparer.Ordinal)))
            throw new FinancialValidationException();
    }

    private static string? QueryValue(HttpContext http, string name) => http.Request.Query.TryGetValue(name, out var values)
        ? values[0] is { Length: > 0 and <= 160 } value ? value : throw new FinancialValidationException() : null;

    private static string Key(HttpContext http) => http.Request.Headers.TryGetValue("Idempotency-Key", out var values) && values.Count == 1
        ? CustomerSettlementRules.Key(values[0]!) : throw new FinancialValidationException();

    private static async Task<T> PayloadAsync<T>(HttpContext http)
    {
        if (!http.Request.HasJsonContentType()) throw new FinancialValidationException();
        if (http.Request.ContentLength > MaximumRequestBodyBytes) throw new FinancialPayloadTooLargeException();
        var buffer = new byte[MaximumRequestBodyBytes + 1];
        var count = await http.Request.Body.ReadAtLeastAsync(buffer, buffer.Length, false, http.RequestAborted).ConfigureAwait(false);
        if (count > MaximumRequestBodyBytes) throw new FinancialPayloadTooLargeException();
        using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 6 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        UniqueNames(document.RootElement);
        return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, count), JsonOptions) ?? throw new JsonException();
    }

    private static void UniqueNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException();
                UniqueNames(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) UniqueNames(item);
    }

    private static async Task<IResult> GuardAsync(Func<Task<IResult>> action)
    {
        try { return await action().ConfigureAwait(false); }
        catch (FinancialPayloadTooLargeException) { return Problem(413, "request_too_large"); }
        catch (BadHttpRequestException error) when (error.StatusCode == 413) { return Problem(413, "request_too_large"); }
        catch (ArgumentException) { return Problem(400, "financial_request_invalid"); }
        catch (JsonException) { return Problem(400, "financial_request_invalid"); }
    }

    private static IResult Failure(FinancialStatus status) => status switch
    {
        FinancialStatus.NotFound or FinancialStatus.DebtorNotFound => Problem(404, "financial_resource_not_found"),
        FinancialStatus.Denied => Problem(403, "financial_access_denied"),
        FinancialStatus.Unavailable => Problem(503, "financial_dependency_unavailable"),
        FinancialStatus.OutcomeUnknown => Problem(503, "financial_outcome_unknown"),
        FinancialStatus.HistoryInvalid => Problem(500, "financial_history_invalid"),
        FinancialStatus.RevisionConflict => Problem(409, "financial_revision_conflict"),
        FinancialStatus.IdempotencyConflict => Problem(409, "idempotency_key_conflict"),
        FinancialStatus.InvalidTransition => Problem(409, "payment_transition_invalid"),
        FinancialStatus.SelfConfirmation => Problem(409, "payment_independent_reconciler_required"),
        FinancialStatus.DuplicateReference => Problem(409, "payment_reference_conflict"),
        FinancialStatus.DebtorMismatch => Problem(409, "payment_debtor_mismatch"),
        FinancialStatus.InsufficientFunds => Problem(409, "payment_funds_insufficient"),
        FinancialStatus.OverAllocation => Problem(409, "receivable_amount_exceeded"),
        FinancialStatus.ActiveAllocations => Problem(409, "payment_has_active_allocations"),
        FinancialStatus.ResourceLimit => Problem(409, "financial_resource_limit"),
        _ => throw new InvalidOperationException("The financial operation returned an unsupported result."),
    };

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        title: "The customer financial operation cannot continue.", extensions: new Dictionary<string, object?> { ["code"] = code });

    private static ReceivableResponse Receivable(ReceivableSnapshot value, Guid tenantId, Guid? invoiceId = null)
    {
        if (value.TenantId != tenantId || value.InvoiceId == Guid.Empty || invoiceId is { } id && value.InvoiceId != id)
            throw new InvalidOperationException("The financial query returned another resource's receivable.");
        return new(value.InvoiceId, value.Debtor.Kind.ToString(), value.Debtor.Id, value.CurrencyCode, value.OriginalAmount,
            value.InvoiceDate, value.RecognizedAt, value.AllocatedAmount, value.OutstandingAmount, value.IsSettled);
    }

    private static PaymentResponse PaymentFacts(CustomerPaymentSnapshot value, Guid tenantId, Guid? paymentId = null)
    {
        if (value.TenantId != tenantId || value.PaymentId == Guid.Empty || paymentId is { } id && value.PaymentId != id)
            throw new InvalidOperationException("The financial query returned another resource's payment.");
        return new(value.PaymentId, value.Debtor.Kind.ToString(), value.Debtor.Id, value.CurrencyCode, value.Amount, value.Channel.ToString(),
            value.Evidence, value.ExternalReference, value.RecordedByAccountId, value.RecordedAt, value.Revision, value.State.ToString(), value.AllocatedAmount, value.AvailableAmount);
    }

    private static PaymentEventResponse Event(CustomerPaymentEvent value, Guid paymentId)
    {
        if (value.PaymentId != paymentId || value.EventId == Guid.Empty) throw new InvalidOperationException("The financial query returned another payment's event.");
        return new(value.EventId, value.PaymentId, value.Revision, value.Operation.ToString(), value.State.ToString(),
            value.ActorAccountId, value.RecordedAt, value.Evidence, value.ReceivableId, value.Amount, value.AllocationId);
    }

    private static BalanceResponse Balance(DebtorBalance value, FinancialDebtor debtor)
    {
        if (value.Debtor != debtor || value.CurrencyCode != "NPR") throw new InvalidOperationException("The financial query returned another debtor's balance.");
        return new(value.Debtor.Kind.ToString(), value.Debtor.Id, value.CurrencyCode, value.RecognizedAmount, value.OutstandingAmount, value.ConfirmedUnappliedAmount);
    }

    private sealed class FinancialPayloadTooLargeException : Exception;
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record RecordPayload([property: JsonRequired] string DebtorKind, [property: JsonRequired] Guid DebtorId,
        [property: JsonRequired] string CurrencyCode, [property: JsonRequired] decimal Amount, [property: JsonRequired] string Channel,
        [property: JsonRequired] string Evidence, string? ExternalReference);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record EvidencePayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] string Evidence);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record ReasonPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] string Reason);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record AllocationPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] Guid ReceivableId, [property: JsonRequired] decimal Amount);
    private sealed record CommandResponse(Guid PaymentId, Guid EventId, long Revision);
    private sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
    private sealed record ReceivableResponse(Guid InvoiceId, string DebtorKind, Guid DebtorId, string CurrencyCode, decimal OriginalAmount,
        DateOnly InvoiceDate, DateTimeOffset RecognizedAt, decimal AllocatedAmount, decimal OutstandingAmount, bool IsSettled);
    private sealed record PaymentResponse(Guid PaymentId, string DebtorKind, Guid DebtorId, string CurrencyCode, decimal Amount,
        string Channel, string Evidence, string? ExternalReference, Guid RecordedByAccountId, DateTimeOffset RecordedAt, long Revision,
        string State, decimal AllocatedAmount, decimal AvailableAmount);
    private sealed record PaymentEventResponse(Guid EventId, Guid PaymentId, long Revision, string Operation, string State,
        Guid ActorAccountId, DateTimeOffset RecordedAt, string? Evidence, Guid? ReceivableId, decimal? Amount, Guid? AllocationId);
    private sealed record BalanceResponse(string DebtorKind, Guid DebtorId, string CurrencyCode, decimal RecognizedAmount,
        decimal OutstandingAmount, decimal ConfirmedUnappliedAmount);
}
