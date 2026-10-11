using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.CoreApi.Authorization;
using Application.Purchasing;
using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class PurchasingRoutes
{
    internal const int MaximumRequestBodyBytes = 8192;
    private const string BasePath = "/api/v1/tenants/{tenantId:guid}/purchasing";
    private const string SupplierPath = BasePath + "/suppliers/{supplierId:guid}";
    private const string PurchasePath = BasePath + "/requests/{purchaseId:guid}";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 6 };

    internal static WebApplication MapPurchasingRoutes(this WebApplication app)
    {
        foreach (var (suffix, kind) in new[] { ("", SupplierCommandKind.Create), ("/revise", SupplierCommandKind.Revise), ("/retire", SupplierCommandKind.Retire) })
        {
            var route = Protect(app.MapPost(SupplierPath + suffix, (Guid tenantId, Guid supplierId, HttpContext http, IPurchasingStore store) =>
                SupplierCommandAsync(tenantId, supplierId, http, store, kind)), EndpointAccess.PurchasingSupplierManage)
                .Produces<PurchasingCommandResponse>();
            if (kind == SupplierCommandKind.Retire) route.Accepts<RevisionPayload>("application/json");
            else route.Accepts<SupplierPayload>("application/json");
        }
        foreach (var (suffix, kind, access) in new[]
        {
            ("", PurchasingCommandKind.Request, EndpointAccess.PurchasingRequestRecord),
            ("/receipts", PurchasingCommandKind.Receive, EndpointAccess.PurchasingReceiptRecord),
            ("/payments", PurchasingCommandKind.Pay, EndpointAccess.PurchasingPaymentRecord),
            ("/refunds", PurchasingCommandKind.Refund, EndpointAccess.PurchasingPaymentRecord),
            ("/corrections", PurchasingCommandKind.Correct, EndpointAccess.PurchasingCorrect),
            ("/cancel", PurchasingCommandKind.Cancel, EndpointAccess.PurchasingRequestRecord),
        })
        {
            var route = Protect(app.MapPost(PurchasePath + suffix, (Guid tenantId, Guid purchaseId, HttpContext http, IPurchasingStore store) =>
                PurchaseCommandAsync(tenantId, purchaseId, http, store, kind)), access).Produces<PurchasingCommandResponse>();
            switch (kind)
            {
                case PurchasingCommandKind.Request: route.Accepts<RequestPayload>("application/json"); break;
                case PurchasingCommandKind.Receive: route.Accepts<ReceiptPayload>("application/json"); break;
                case PurchasingCommandKind.Pay: route.Accepts<PaymentPayload>("application/json"); break;
                case PurchasingCommandKind.Refund: route.Accepts<RefundPayload>("application/json"); break;
                case PurchasingCommandKind.Correct: route.Accepts<CorrectionPayload>("application/json"); break;
                case PurchasingCommandKind.Cancel: route.Accepts<CancelPayload>("application/json"); break;
            }
        }
        Protect(app.MapGet(SupplierPath, (Guid tenantId, Guid supplierId, HttpContext http, IPurchasingStore store) =>
            ReadAsync(tenantId, supplierId, http, store, ReadKind.Supplier)), EndpointAccess.PurchasingView).Produces<SupplierResponse>();
        Protect(app.MapGet(PurchasePath, (Guid tenantId, Guid purchaseId, HttpContext http, IPurchasingStore store) =>
            ReadAsync(tenantId, purchaseId, http, store, ReadKind.Purchase)), EndpointAccess.PurchasingView).Produces<PurchaseResponse>();
        Protect(app.MapGet(SupplierPath + "/balance", (Guid tenantId, Guid supplierId, HttpContext http, IPurchasingStore store) =>
            ReadAsync(tenantId, supplierId, http, store, ReadKind.Balance)), EndpointAccess.PurchasingView).Produces<SupplierBalanceResponse>();
        Protect(app.MapGet(SupplierPath + "/history", (Guid tenantId, Guid supplierId, HttpContext http, IPurchasingStore store) =>
            ReadAsync(tenantId, supplierId, http, store, ReadKind.SupplierHistory)), EndpointAccess.PurchasingView).Produces<SupplierHistoryResponse>();
        Protect(app.MapGet(PurchasePath + "/history", (Guid tenantId, Guid purchaseId, HttpContext http, IPurchasingStore store) =>
            ReadAsync(tenantId, purchaseId, http, store, ReadKind.PurchaseHistory)), EndpointAccess.PurchasingView).Produces<PurchaseHistoryResponse>();
        Protect(app.MapGet(BasePath + "/suppliers", (Guid tenantId, HttpContext http, IPurchasingStore store) =>
            ReadAsync(tenantId, Guid.Empty, http, store, ReadKind.Suppliers)), EndpointAccess.PurchasingView).Produces<SupplierPageResponse>();
        Protect(app.MapGet(BasePath + "/requests", (Guid tenantId, HttpContext http, IPurchasingStore store) =>
            ReadAsync(tenantId, Guid.Empty, http, store, ReadKind.Purchases)), EndpointAccess.PurchasingView).Produces<PurchasePageResponse>();
        return app;
    }

    private static RouteHandlerBuilder Protect(RouteHandlerBuilder route, EndpointAccess access) => route
        .RequireAuthorization().WithCoreApiAccess(access).WithCoreApiApplicationAuthorization(access).WithTags("Purchasing")
        .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestBodyBytes))
        .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404)
        .ProducesProblem(409).ProducesProblem(413).ProducesProblem(429).ProducesProblem(500).ProducesProblem(503).ProducesProblem(504);

    private static async Task<IResult> SupplierCommandAsync(Guid tenantId, Guid supplierId, HttpContext http,
        IPurchasingStore store, SupplierCommandKind kind) => await GuardAsync(async () =>
    {
        if (supplierId == Guid.Empty || !TryKey(http, out var key)) return Invalid("purchasing_request_invalid");
        SupplierMutation mutation;
        if (kind == SupplierCommandKind.Retire)
        {
            var payload = await ReadPayloadAsync<RevisionPayload>(http).ConfigureAwait(false);
            mutation = new(kind, payload.ExpectedRevision);
        }
        else
        {
            var payload = await ReadPayloadAsync<SupplierPayload>(http).ConfigureAwait(false);
            mutation = new(kind, payload.ExpectedRevision, payload.Name, payload.Contact);
        }
        if (mutation.ExpectedRevision is < 0 or >= long.MaxValue - 1 ||
            kind != SupplierCommandKind.Create && mutation.ExpectedRevision == 0) return Invalid("purchasing_request_invalid");
        var result = await store.ExecuteSupplierAsync(TenantRequestAccess.RequireResolvedContext(tenantId, http),
            supplierId, mutation, key, http.RequestAborted).ConfigureAwait(false);
        if (result.Status is not (PurchasingStoreStatus.Applied or PurchasingStoreStatus.Replayed))
            return Outcome(result.Status, result.Rejection);
        var supplier = result.Supplier ?? throw new InvalidOperationException("Supplier result is missing.");
        RequireSupplier(supplier, tenantId, supplierId);
        if (result.Status == PurchasingStoreStatus.Replayed) http.Response.Headers.Append("Idempotency-Replayed", "true");
        return Results.Ok(new PurchasingCommandResponse(supplierId.ToString("D"), supplier.Revision));
    }).ConfigureAwait(false);

    private static async Task<IResult> PurchaseCommandAsync(Guid tenantId, Guid purchaseId, HttpContext http,
        IPurchasingStore store, PurchasingCommandKind kind) => await GuardAsync(async () =>
    {
        if (purchaseId == Guid.Empty || !TryKey(http, out var key)) return Invalid("purchasing_request_invalid");
        if (kind == PurchasingCommandKind.Correct)
        {
            var denied = http.RequestServices.GetRequiredService<HighRiskActionAdmission>()
                .Require(http.User, HighRiskAction.CorrectPurchasing);
            if (denied is not null) return denied;
        }
        PurchasingMutation mutation;
        switch (kind)
        {
            case PurchasingCommandKind.Request:
                var request = await ReadPayloadAsync<RequestPayload>(http).ConfigureAwait(false);
                mutation = new(kind, request.ExpectedRevision, SupplierId: request.SupplierId, UnitId: request.UnitId,
                    Order: request.Order is null ? null : new(request.Order.OrderId, request.Order.LinePosition, request.Order.OrderRevision),
                    Description: request.Description, Quantity: request.Quantity);
                break;
            case PurchasingCommandKind.Receive:
                var receipt = await ReadPayloadAsync<ReceiptPayload>(http).ConfigureAwait(false);
                mutation = new(kind, receipt.ExpectedRevision, Quantity: receipt.Quantity, Amount: receipt.Cost);
                break;
            case PurchasingCommandKind.Pay:
                var payment = await ReadPayloadAsync<PaymentPayload>(http).ConfigureAwait(false);
                if (!TryEnum(payment.Method, out SupplierPaymentMethod method)) return Invalid("purchasing_request_invalid");
                mutation = new(kind, payment.ExpectedRevision, Amount: payment.Amount, ReceiptId: payment.ReceiptId,
                    Method: method, Reference: payment.Reference);
                break;
            case PurchasingCommandKind.Refund:
                var refund = await ReadPayloadAsync<RefundPayload>(http).ConfigureAwait(false);
                if (!TryEnum(refund.Method, out SupplierPaymentMethod refundMethod)) return Invalid("purchasing_request_invalid");
                mutation = new(kind, refund.ExpectedRevision, Amount: refund.Amount, PaymentId: refund.PaymentId,
                    Method: refundMethod, Reference: refund.Reference);
                break;
            case PurchasingCommandKind.Cancel:
                var cancel = await ReadPayloadAsync<CancelPayload>(http).ConfigureAwait(false);
                mutation = new(kind, cancel.ExpectedRevision, Reason: cancel.Reason);
                break;
            case PurchasingCommandKind.Correct:
                var correction = await ReadPayloadAsync<CorrectionPayload>(http).ConfigureAwait(false);
                PurchasingFact? replacement = null;
                if (correction.Replacement is { } value)
                {
                    if (!TryEnum(value.Kind, out PurchasingEventKind eventKind)) return Invalid("purchasing_request_invalid");
                    SupplierPaymentMethod? replacementMethod = null;
                    if (value.Method is not null)
                    {
                        if (!TryEnum(value.Method, out SupplierPaymentMethod parsed)) return Invalid("purchasing_request_invalid");
                        replacementMethod = parsed;
                    }
                    replacement = new(eventKind, value.Quantity, value.Amount, value.ReceiptId, value.PaymentId,
                        replacementMethod, value.Reference);
                }
                mutation = new(kind, correction.ExpectedRevision, Reason: correction.Reason,
                    TargetEventId: correction.TargetEventId, Replacement: replacement);
                break;
            default: throw new InvalidOperationException("Unsupported purchasing command.");
        }
        if (mutation.ExpectedRevision is < 0 or >= long.MaxValue - 1 ||
            kind != PurchasingCommandKind.Request && mutation.ExpectedRevision == 0) return Invalid("purchasing_request_invalid");
        var result = await store.ExecutePurchaseAsync(TenantRequestAccess.RequireResolvedContext(tenantId, http),
            purchaseId, mutation, key, http.RequestAborted).ConfigureAwait(false);
        if (result.Status is not (PurchasingStoreStatus.Applied or PurchasingStoreStatus.Replayed))
            return Outcome(result.Status, result.Rejection);
        var purchase = result.Purchase ?? throw new InvalidOperationException("Purchase result is missing.");
        RequirePurchase(purchase, tenantId, purchaseId);
        if (result.Status == PurchasingStoreStatus.Replayed) http.Response.Headers.Append("Idempotency-Replayed", "true");
        return Results.Ok(new PurchasingCommandResponse(purchaseId.ToString("D"), purchase.Revision));
    }).ConfigureAwait(false);

    private enum ReadKind { Supplier, Purchase, Balance, SupplierHistory, PurchaseHistory, Suppliers, Purchases }

    private static async Task<IResult> ReadAsync(Guid tenantId, Guid id, HttpContext http, IPurchasingStore store, ReadKind kind) =>
        await GuardAsync(async () =>
        {
            var context = TenantRequestAccess.RequireResolvedContext(tenantId, http);
            if (kind is ReadKind.Suppliers or ReadKind.Purchases)
            {
                if (!TryQuery(http.Request.Query, ["limit", "after"], 50, 25, out var limit)) return Invalid("purchasing_page_invalid");
                PurchasingCursor? cursor = null;
                if (http.Request.Query.TryGetValue("after", out var raw) && !TryCursor(raw, tenantId, kind, out cursor))
                    return Invalid("purchasing_cursor_invalid");
                if (kind == ReadKind.Suppliers)
                {
                    var page = await store.ListSuppliersAsync(context, new(limit, cursor), http.RequestAborted).ConfigureAwait(false);
                    if (page.Items.Count > limit) throw new InvalidOperationException("Purchasing page exceeds its bound.");
                    foreach (var item in page.Items) RequireSupplier(item, tenantId, item.SupplierId);
                    return Results.Ok(new SupplierPageResponse(page.Items.Select(Supplier).ToArray(),
                        page.NextCursor is null ? null : EncodeCursor(tenantId, kind, page.NextCursor)));
                }
                var purchases = await store.ListPurchasesAsync(context, new(limit, cursor), http.RequestAborted).ConfigureAwait(false);
                if (purchases.Items.Count > limit) throw new InvalidOperationException("Purchasing page exceeds its bound.");
                foreach (var item in purchases.Items) RequirePurchase(item, tenantId, item.PurchaseId);
                return Results.Ok(new PurchasePageResponse(purchases.Items.Select(Purchase).ToArray(),
                    purchases.NextCursor is null ? null : EncodeCursor(tenantId, kind, purchases.NextCursor)));
            }
            if (kind is ReadKind.SupplierHistory or ReadKind.PurchaseHistory)
            {
                if (!TryQuery(http.Request.Query, ["limit", "beforeRevision"], 10, 10, out var limit)) return Invalid("purchasing_page_invalid");
                long? before = null;
                if (http.Request.Query.TryGetValue("beforeRevision", out var raw))
                {
                    if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var revision) || revision < 1)
                        return Invalid("purchasing_page_invalid");
                    before = revision;
                }
                if (kind == ReadKind.SupplierHistory)
                {
                    var history = await store.SupplierHistoryAsync(context, id, new(limit, before), http.RequestAborted).ConfigureAwait(false);
                    if (history is null) return Outcome(PurchasingStoreStatus.NotFound);
                    if (history.Items.Count > limit) throw new InvalidOperationException("Purchasing history exceeds its bound.");
                    foreach (var entry in history.Items) RequireSupplier(entry.Supplier, tenantId, id);
                    return Results.Ok(new SupplierHistoryResponse(history.ObservedRevision, history.Items.Select(entry =>
                        new SupplierHistoryEventResponse(entry.EventId.ToString("D"), entry.Revision, entry.ActorAccountId.ToString("D"),
                            entry.RecordedAt, entry.AuthorizationRevision, entry.Kind.ToString(), Supplier(entry.Supplier))).ToArray(), history.NextRevision));
                }
                var events = await store.HistoryAsync(context, id, new(limit, before), http.RequestAborted).ConfigureAwait(false);
                if (events is null) return Outcome(PurchasingStoreStatus.NotFound);
                if (events.Items.Count > limit) throw new InvalidOperationException("Purchasing history exceeds its bound.");
                return Results.Ok(new PurchaseHistoryResponse(events.ObservedRevision, events.Items.Select(entry =>
                    new PurchaseHistoryEventResponse(entry.EventId.ToString("D"), entry.Revision, entry.ActorAccountId.ToString("D"),
                        entry.RecordedAt, entry.AuthorizationRevision, entry.Kind.ToString(), Fact(entry.Facts), entry.Reason,
                        entry.TargetEventId?.ToString("D"), Fact(entry.Replacement))).ToArray(), events.NextRevision));
            }
            if (http.Request.Query.Count != 0 || id == Guid.Empty) return Invalid("purchasing_request_invalid");
            switch (kind)
            {
                case ReadKind.Supplier:
                    var supplier = await store.FindSupplierAsync(context, id, http.RequestAborted).ConfigureAwait(false);
                    if (supplier is null) return Outcome(PurchasingStoreStatus.NotFound);
                    RequireSupplier(supplier, tenantId, id);
                    return Results.Ok(Supplier(supplier));
                case ReadKind.Purchase:
                    var purchase = await store.FindPurchaseAsync(context, id, http.RequestAborted).ConfigureAwait(false);
                    if (purchase is null) return Outcome(PurchasingStoreStatus.NotFound);
                    RequirePurchase(purchase, tenantId, id);
                    return Results.Ok(Purchase(purchase));
                case ReadKind.Balance:
                    var balance = await store.SupplierBalanceAsync(context, id, http.RequestAborted).ConfigureAwait(false);
                    if (balance is null) return Outcome(PurchasingStoreStatus.NotFound);
                    if (balance.SupplierId != id || balance.Currency != "NPR") throw new InvalidOperationException("Supplier balance is invalid.");
                    return Results.Ok(new SupplierBalanceResponse(id.ToString("D"), balance.Currency, balance.TotalCost,
                        balance.PaidAmount, balance.RefundedAmount, balance.OutstandingPayable));
                default: throw new InvalidOperationException("Unsupported purchasing query.");
            }
        }).ConfigureAwait(false);

    private static SupplierResponse Supplier(SupplierSnapshot value) => new(value.SupplierId.ToString("D"), value.Revision,
        value.Name, value.Contact, value.Status.ToString(), value.CreatedAt, value.UpdatedAt);
    private static PurchaseResponse Purchase(PurchaseSnapshot value) => new(value.PurchaseId.ToString("D"), value.Revision,
        Supplier(value.Supplier), value.Unit.UnitId.ToString("D"), value.Unit.UnitCode, value.Unit.UnitPrecision, value.Unit.UnitRevision,
        value.Order is null ? null : new(value.Order.OrderId, value.Order.LinePosition, value.Order.OrderRevision), value.Description,
        value.RequestedQuantity, value.ReceivedQuantity, value.RemainingQuantity, value.CancelledQuantity, value.UnfulfilledQuantity,
        "NPR", value.TotalCost, value.PaidAmount, value.RefundedAmount, value.OutstandingPayable,
        value.Receipts.Select(x => new ReceiptResponse(x.EntryId.ToString("D"), x.EffectiveEventId.ToString("D"), x.Quantity, x.Cost, x.IsVoided)).ToArray(),
        value.Payments.Select(x => new PaymentResponse(x.EntryId.ToString("D"), x.EffectiveEventId.ToString("D"), x.ReceiptId.ToString("D"),
            x.Amount, x.Method.ToString(), x.Reference, x.IsVoided)).ToArray(),
        value.Refunds.Select(x => new RefundResponse(x.EntryId.ToString("D"), x.EffectiveEventId.ToString("D"), x.PaymentId.ToString("D"),
            x.Amount, x.Method.ToString(), x.Reference, x.IsVoided)).ToArray(), value.CreatedAt, value.UpdatedAt);
    private static FactResponse? Fact(PurchasingFact? fact) => fact is null ? null : new(fact.Kind.ToString(), fact.Quantity,
        fact.Amount, fact.ReceiptId?.ToString("D"), fact.PaymentId?.ToString("D"), fact.Method?.ToString(), fact.Reference);
    private static void RequireSupplier(SupplierSnapshot value, Guid tenant, Guid id)
    {
        if (value.TenantId != tenant || value.SupplierId != id || id == Guid.Empty)
            throw new InvalidOperationException("Supplier returned another resource.");
    }
    private static void RequirePurchase(PurchaseSnapshot value, Guid tenant, Guid id)
    {
        if (value.TenantId != tenant || value.PurchaseId != id || id == Guid.Empty || value.Unit.TenantId != tenant || value.Supplier.TenantId != tenant)
            throw new InvalidOperationException("Purchasing returned another resource.");
    }
    private static async Task<T> ReadPayloadAsync<T>(HttpContext http)
    {
        if (!http.Request.HasJsonContentType()) throw new JsonException();
        if (http.Request.ContentLength > MaximumRequestBodyBytes) throw new PurchasingPayloadTooLargeException();
        var buffer = new byte[MaximumRequestBodyBytes + 1];
        var count = await http.Request.Body.ReadAtLeastAsync(buffer, buffer.Length, false, http.RequestAborted).ConfigureAwait(false);
        if (count > MaximumRequestBodyBytes) throw new PurchasingPayloadTooLargeException();
        using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 6 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        ValidateUniqueNames(document.RootElement);
        return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, count), JsonOptions) ?? throw new JsonException();
    }
    private static void ValidateUniqueNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException();
                ValidateUniqueNames(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) ValidateUniqueNames(item);
    }
    private static bool TryKey(HttpContext http, out string key)
    {
        key = string.Empty;
        if (!http.Request.Headers.TryGetValue("Idempotency-Key", out var values) || values.Count != 1) return false;
        key = values[0] ?? string.Empty;
        return key.Length is >= 1 and <= 128 && key.All(c => c is >= '!' and <= '~');
    }
    private static bool TryEnum<T>(string? value, out T parsed) where T : struct, Enum =>
        Enum.TryParse(value, false, out parsed) && Enum.IsDefined(parsed) && Enum.GetName(parsed) == value;
    private static bool TryQuery(IQueryCollection query, string[] names, int maximum, int defaultLimit, out int limit)
    {
        limit = defaultLimit;
        if (query.Any(pair => pair.Value.Count != 1 || !names.Contains(pair.Key, StringComparer.Ordinal))) return false;
        return !query.TryGetValue("limit", out var raw) || int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit >= 1 && limit <= maximum;
    }
    private static string EncodeCursor(Guid tenantId, ReadKind kind, PurchasingCursor cursor) => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        string.Create(CultureInfo.InvariantCulture, $"1:{tenantId:N}:{kind}:{cursor.CreatedAt.UtcTicks}:{cursor.Id:N}")))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static bool TryCursor(string? raw, Guid tenantId, ReadKind kind, out PurchasingCursor? cursor)
    {
        cursor = null;
        if (raw is null || raw.Length is < 1 or > 160 || raw.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) return false;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(raw.Replace('-', '+').Replace('_', '/').PadRight((raw.Length + 3) / 4 * 4, '='))).Split(':');
            if (parts.Length != 5 || parts[0] != "1" || !Guid.TryParseExact(parts[1], "N", out var tenant) || tenant != tenantId ||
                parts[2] != kind.ToString() || !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
                !Guid.TryParseExact(parts[4], "N", out var id) || id == Guid.Empty) return false;
            cursor = new(new DateTimeOffset(ticks, TimeSpan.Zero), id);
            return true;
        }
        catch (Exception error) when (error is FormatException or ArgumentOutOfRangeException) { return false; }
    }
    private static async Task<IResult> GuardAsync(Func<Task<IResult>> operation)
    {
        try { return await operation().ConfigureAwait(false); }
        catch (PurchasingPayloadTooLargeException) { return TooLarge(); }
        catch (BadHttpRequestException error) when (error.StatusCode == 413) { return TooLarge(); }
        catch (PurchasingAccessDeniedException) { return Outcome(PurchasingStoreStatus.Forbidden); }
        catch (PurchasingValidationException error) { return Invalid(error.Code); }
        catch (JsonException) { return Invalid("purchasing_request_invalid"); }
        catch (ArgumentException) { return Invalid("purchasing_request_invalid"); }
        catch (AuthorizationProviderUnavailableException)
        {
            return Results.Problem(statusCode: 503, title: "Purchasing authority is temporarily unavailable.",
            extensions: new Dictionary<string, object?> { ["code"] = "authorization_unavailable" });
        }
    }
    private static IResult Invalid(string code) => Results.Problem(statusCode: 400, title: "The purchasing request is invalid.",
        extensions: new Dictionary<string, object?> { ["code"] = code });
    private static IResult TooLarge() => Results.Problem(statusCode: 413, title: "Purchasing request exceeds the supported size.",
        extensions: new Dictionary<string, object?> { ["code"] = "request_too_large" });
    private static IResult Outcome(PurchasingStoreStatus status, PurchasingMutationStatus? rejection = null) => Results.Problem(
        statusCode: status switch
        {
            PurchasingStoreStatus.Forbidden => 403,
            PurchasingStoreStatus.NotFound => 404,
            PurchasingStoreStatus.TransitionRejected when rejection == PurchasingMutationStatus.NotFound => 404,
            PurchasingStoreStatus.TransitionRejected when rejection == PurchasingMutationStatus.InvalidProjection => 500,
            PurchasingStoreStatus.TransitionRejected when rejection is PurchasingMutationStatus.InputInvalid or PurchasingMutationStatus.TextInvalid
                or PurchasingMutationStatus.QuantityInvalid or PurchasingMutationStatus.AmountInvalid => 400,
            _ => 409,
        }, title: "The purchasing operation cannot continue in its current state.",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = status switch
            {
                PurchasingStoreStatus.Forbidden => "purchasing_denied",
                PurchasingStoreStatus.NotFound => "purchasing_not_found",
                PurchasingStoreStatus.IdempotencyConflict => "idempotency_key_conflict",
                _ => RejectionCode(rejection),
            }
        });

    private static string RejectionCode(PurchasingMutationStatus? status) => status switch
    {
        PurchasingMutationStatus.InputInvalid => "purchasing_request_invalid",
        PurchasingMutationStatus.TextInvalid => "purchasing_text_invalid",
        PurchasingMutationStatus.QuantityInvalid => "purchasing_quantity_invalid",
        PurchasingMutationStatus.AmountInvalid => "purchasing_amount_invalid",
        PurchasingMutationStatus.SourceIneligible => "purchasing_source_ineligible",
        PurchasingMutationStatus.RevisionConflict => "purchasing_revision_conflict",
        PurchasingMutationStatus.AlreadyExists => "purchasing_already_exists",
        PurchasingMutationStatus.NotFound => "purchasing_not_found",
        PurchasingMutationStatus.SupplierRetired => "purchasing_supplier_retired",
        PurchasingMutationStatus.RequestCancelled => "purchasing_request_cancelled",
        PurchasingMutationStatus.OverReceipt => "purchasing_over_receipt",
        PurchasingMutationStatus.OverPayment => "purchasing_over_payment",
        PurchasingMutationStatus.OverRefund => "purchasing_over_refund",
        PurchasingMutationStatus.DependencyInvalid => "purchasing_dependency_invalid",
        PurchasingMutationStatus.CorrectionTargetInvalid => "purchasing_correction_target_invalid",
        PurchasingMutationStatus.CorrectionHasDependents => "purchasing_correction_has_dependents",
        PurchasingMutationStatus.InvalidProjection => "purchasing_state_invalid",
        PurchasingMutationStatus.ResourceLimit => "purchasing_resource_limit",
        PurchasingMutationStatus.AlreadyRetired => "purchasing_already_retired",
        PurchasingMutationStatus.NoRemainingQuantity => "purchasing_no_remaining_quantity",
        _ => "purchasing_conflict",
    };

    private sealed class PurchasingPayloadTooLargeException : Exception;
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record RevisionPayload([property: JsonRequired] long ExpectedRevision);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record SupplierPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] string Name, string? Contact);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record OrderPayload([property: JsonRequired] Guid OrderId, [property: JsonRequired] int LinePosition, [property: JsonRequired] long OrderRevision);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record RequestPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] Guid SupplierId,
        [property: JsonRequired] Guid UnitId, [property: JsonRequired] string Description, [property: JsonRequired] decimal Quantity, OrderPayload? Order);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record ReceiptPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] decimal Quantity, [property: JsonRequired] decimal Cost);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record PaymentPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] Guid ReceiptId,
        [property: JsonRequired] decimal Amount, [property: JsonRequired] string Method, string? Reference);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record RefundPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] Guid PaymentId,
        [property: JsonRequired] decimal Amount, [property: JsonRequired] string Method, string? Reference);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record CancelPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] string Reason);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record CorrectionPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] Guid TargetEventId,
        [property: JsonRequired] string Reason, [property: JsonRequired] ReplacementPayload? Replacement);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record ReplacementPayload([property: JsonRequired] string Kind, decimal? Quantity, decimal? Amount,
        Guid? ReceiptId, Guid? PaymentId, string? Method, string? Reference);

    private sealed record PurchasingCommandResponse(string Id, long Revision);
    private sealed record SupplierResponse(string SupplierId, long Revision, string Name, string? Contact, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
    private sealed record SupplierPageResponse(IReadOnlyList<SupplierResponse> Items, string? NextCursor);
    private sealed record SupplierBalanceResponse(string SupplierId, string Currency, decimal TotalCost, decimal PaidAmount, decimal RefundedAmount, decimal OutstandingPayable);
    private sealed record ReceiptResponse(string EntryId, string EffectiveEventId, decimal Quantity, decimal Cost, bool IsVoided);
    private sealed record PaymentResponse(string EntryId, string EffectiveEventId, string ReceiptId, decimal Amount, string Method, string? Reference, bool IsVoided);
    private sealed record RefundResponse(string EntryId, string EffectiveEventId, string PaymentId, decimal Amount, string Method, string? Reference, bool IsVoided);
    private sealed record PurchaseResponse(string PurchaseId, long Revision, SupplierResponse Supplier, string UnitId, string UnitCode, int UnitPrecision,
        long UnitRevision, OrderPayload? Order, string Description, decimal RequestedQuantity, decimal ReceivedQuantity, decimal RemainingQuantity,
        decimal? CancelledQuantity, decimal UnfulfilledQuantity, string Currency, decimal TotalCost, decimal PaidAmount, decimal RefundedAmount,
        decimal OutstandingPayable, IReadOnlyList<ReceiptResponse> Receipts, IReadOnlyList<PaymentResponse> Payments, IReadOnlyList<RefundResponse> Refunds,
        DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
    private sealed record PurchasePageResponse(IReadOnlyList<PurchaseResponse> Items, string? NextCursor);
    private sealed record FactResponse(string Kind, decimal? Quantity, decimal? Amount, string? ReceiptId, string? PaymentId, string? Method, string? Reference);
    private sealed record PurchaseHistoryEventResponse(string EventId, long Revision, string ActorAccountId, DateTimeOffset RecordedAt,
        long AuthorizationRevision, string Kind, FactResponse? Facts, string? Reason, string? TargetEventId, FactResponse? Replacement);
    private sealed record PurchaseHistoryResponse(long ObservedRevision, IReadOnlyList<PurchaseHistoryEventResponse> Items, long? NextRevision);
    private sealed record SupplierHistoryEventResponse(string EventId, long Revision, string ActorAccountId, DateTimeOffset RecordedAt,
        long AuthorizationRevision, string Kind, SupplierResponse Supplier);
    private sealed record SupplierHistoryResponse(long ObservedRevision, IReadOnlyList<SupplierHistoryEventResponse> Items, long? NextRevision);
}
