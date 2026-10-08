using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Catalog;
using Application.CoreApi.Authorization;
using Application.Customers;
using Application.IdentityAccess;
using Application.Orders;
using Application.Pricing;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi;

internal static class TenantCatalogOrderEndpoint
{
    internal static Task<IResult> CreateAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        CatalogOrderDraftApplication orders, IOrderPricingAuthorityReader pricingAuthority,
        CoreApiMutationDiagnostics diagnostics, CancellationToken ct) =>
        ExecuteAsync(tenantId, null, http, principal, account, tenant, authorization, orders, pricingAuthority, diagnostics, ct);

    internal static Task<IResult> ReviseAsync(Guid tenantId, Guid orderId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        CatalogOrderDraftApplication orders, IOrderPricingAuthorityReader pricingAuthority,
        CoreApiMutationDiagnostics diagnostics, CancellationToken ct) =>
        ExecuteAsync(tenantId, orderId, http, principal, account, tenant, authorization, orders, pricingAuthority, diagnostics, ct);

    private static async Task<IResult> ExecuteAsync(Guid tenantId, Guid? orderId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        CatalogOrderDraftApplication orders, IOrderPricingAuthorityReader pricingAuthority,
        CoreApiMutationDiagnostics diagnostics, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var access = await TenantRequestAccess.ResolveAsync(tenantId, http, principal, account, tenant, ct);
        if (access.Failure is not null) return access.Failure;
        var denied = await TenantOrderEndpoint.AuthorizeAsync(http, principal, access.TenantContext!, authorization,
            "The account is not permitted to select catalog-priced orders in this tenant.", ct);
        if (denied is not null) return denied;
        if (!TenantOrderEndpoint.TryGetIdempotencyKey(http.Request.Headers, out var key))
            return TenantOrderEndpoint.InvalidRequest("idempotency_key_invalid", "One Idempotency-Key is required.");

        var body = await ReadAsync(http.Request, orderId.HasValue, ct);
        if (body.Failure is not null) return body.Failure;
        try
        {
            var request = body.Request!;
            var authority = request.Lines.Any(line => line.OverridePrice.HasValue)
                ? await pricingAuthority.ReadAsync(access.TenantContext!, ct)
                : new OrderPricingAuthority(false);
            OrderDraftSnapshot? snapshot;
            bool replay;
            if (orderId is { } id)
            {
                var result = await orders.ReviseAsync(access.TenantContext!, new(id, body.ExpectedRevision!.Value, request), key!, authority, ct);
                if (result.Status == ReviseOrderDraftStatus.NotFound)
                    return TenantCustomerEndpoint.NotFound("Order not found.", "order_not_found", "Order not found in this tenant.");
                if (result.Status is not (ReviseOrderDraftStatus.Revised or ReviseOrderDraftStatus.Replayed))
                    return Conflict(result.Status switch
                    {
                        ReviseOrderDraftStatus.RevisionConflict => "revision_conflict",
                        ReviseOrderDraftStatus.AlreadyCommitted => "order_already_committed",
                        ReviseOrderDraftStatus.AlreadyAbandoned => "order_already_abandoned",
                        _ => "idempotency_key_conflict",
                    });
                snapshot = result.Order;
                replay = result.Status == ReviseOrderDraftStatus.Replayed;
            }
            else
            {
                var result = await orders.CreateAsync(access.TenantContext!, request, key!, authority, ct);
                if (result.Status == CreateOrderDraftStatus.IdempotencyKeyConflict) return Conflict("idempotency_key_conflict");
                snapshot = result.Order;
                replay = result.Status == CreateOrderDraftStatus.Replayed;
            }
            if (snapshot is null || snapshot.TenantId != tenantId || (orderId.HasValue && snapshot.OrderId != orderId.Value))
                throw new InvalidOperationException("The commercial order operation returned inconsistent facts.");
            diagnostics.RecordSuccess(orderId.HasValue ? CoreApiMutation.OrderDraftRevised : CoreApiMutation.OrderDraftCreated,
                access.TenantContext!, snapshot.OrderId, replay, http.TraceIdentifier);
            if (replay) http.Response.Headers.Append("Idempotency-Replayed", "true");
            var response = TenantOrderEndpoint.ToResponse(snapshot);
            return orderId.HasValue || replay ? TypedResults.Ok(response) :
                TypedResults.Created($"/api/v1/tenants/{tenantId:D}/orders/{snapshot.OrderId:D}", response);
        }
        catch (OrderCommercialSelectionException error)
        {
            return error.Code == "pricing_override_forbidden"
                ? TypedResults.Problem(statusCode: 403, title: "Price override is not permitted.",
                    extensions: new Dictionary<string, object?> { ["code"] = error.Code })
                : Conflict(error.Code);
        }
        catch (OrderDraftValidationException error) { return TenantOrderEndpoint.InvalidRequest("Invalid catalog order request.", error.Code, error.Message); }
        catch (CatalogValidationException error) { return TenantOrderEndpoint.InvalidRequest("Invalid catalog order request.", error.Code, error.Message); }
        catch (PricingValidationException error)
        {
            return error.Code == "pricing_policy_missing" ? Conflict(error.Code) :
                TenantOrderEndpoint.InvalidRequest("Invalid catalog order request.", error.Code, error.Message);
        }
        catch (CustomerOrderContextNotFoundException)
        { return TenantCustomerEndpoint.NotFound("customer_context_not_found", "Customer context not found in this tenant."); }
        catch (AuthorizationProviderUnavailableException)
        {
            return TypedResults.Problem(statusCode: 503, title: "Authorization is temporarily unavailable.",
                extensions: new Dictionary<string, object?> { ["code"] = "authorization_unavailable" });
        }
    }

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Conflict(string code) => TypedResults.Problem(statusCode: 409,
        title: "Order selection conflicts with current commercial facts.",
        extensions: new Dictionary<string, object?> { ["code"] = code });

    private static async Task<(CatalogOrderDraftRequest? Request, long? ExpectedRevision, IResult? Failure)> ReadAsync(
        HttpRequest request, bool revision, CancellationToken ct)
    {
        if (request.ContentLength > TenantOrderEndpoint.MaximumCreateRequestBodyBytes)
            return (null, null, TenantOrderEndpoint.RequestTooLarge());
        if (!request.HasJsonContentType()) return Invalid();
        try
        {
            var bytes = new byte[checked((int)TenantOrderEndpoint.MaximumCreateRequestBodyBytes + 1)];
            var count = await request.Body.ReadAtLeastAsync(bytes, bytes.Length, false, ct);
            if (count > TenantOrderEndpoint.MaximumCreateRequestBodyBytes)
                return (null, null, TenantOrderEndpoint.RequestTooLarge());
            using var json = JsonDocument.Parse(bytes.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 8 });
            if (json.RootElement.ValueKind != JsonValueKind.Object || HasDuplicateMembers(json.RootElement)) return Invalid();
            var value = json.RootElement.Deserialize<CatalogOrderPayload>(JsonOptions);
            if (value?.Lines is null || value.Lines.Any(line => line is null) ||
                (revision ? value.ExpectedRevision is null : value.ExpectedRevision is not null)) return Invalid();
            return (new(value.Summary ?? "", value.CurrencyCode ?? "",
                value.Lines.Select(line => new CatalogOrderLineInput(line.ItemId, line.UnitId, line.Quantity,
                    line.ConversionRevision, line.OverridePrice, line.OverrideReason)).ToArray(), value.CustomerId,
                value.CustomerContext is { } customer ? new CustomerOrderContext(customer.OrganizationId, customer.ProgramId) : null,
                value.WholesaleApplicable), value.ExpectedRevision, null);
        }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
        { return (null, null, TenantOrderEndpoint.RequestTooLarge()); }
        catch (Exception error) when (error is JsonException or BadHttpRequestException)
        { return Invalid(); }
    }

    private static bool HasDuplicateMembers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var member in value.EnumerateObject())
                if (!names.Add(member.Name) || HasDuplicateMembers(member.Value)) return true;
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) if (HasDuplicateMembers(item)) return true;
        return false;
    }

    private static (CatalogOrderDraftRequest?, long?, IResult?) Invalid() =>
        (null, null, TenantOrderEndpoint.InvalidRequest("Invalid catalog order request.", "request_invalid", "A strict catalog order JSON request is required."));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CatalogOrderPayload([property: JsonRequired] string? Summary,
    [property: JsonRequired] string? CurrencyCode, [property: JsonRequired] IReadOnlyList<CatalogOrderLinePayload>? Lines,
    long? ExpectedRevision = null, Guid? CustomerId = null, CatalogOrderCustomerPayload? CustomerContext = null,
    bool WholesaleApplicable = false);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CatalogOrderLinePayload([property: JsonRequired] Guid ItemId, [property: JsonRequired] Guid UnitId,
    [property: JsonRequired] decimal Quantity, long? ConversionRevision = null, decimal? OverridePrice = null, string? OverrideReason = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CatalogOrderCustomerPayload([property: JsonRequired] Guid OrganizationId, Guid? ProgramId = null);
