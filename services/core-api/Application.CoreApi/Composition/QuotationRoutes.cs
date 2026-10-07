using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.CoreApi.Authorization;
using Application.IdentityAccess;
using Application.Quotations;
using Application.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class QuotationRoutes
{
    private const int MaximumBodyBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    internal static void MapQuotationEndpoints(this WebApplication app)
    {
        const string root = "/api/v1/tenants/{tenantId:guid}/quotations";
        Configure(app.MapPost(root, CreateAsync), "CreateQuotationDraft", EndpointAccess.AuthorizedQuotationCreate).Produces<QuotationResponse>(201).Accepts<QuotationPayload>("application/json");
        Configure(app.MapPut(root + "/{quotationId:guid}/draft", ReviseAsync), "ReviseQuotationDraft", EndpointAccess.AuthorizedQuotationEdit).Accepts<QuotationPayload>("application/json");
        Configure(app.MapPost(root + "/{quotationId:guid}/issue", IssueAsync), "IssueQuotationRevision", EndpointAccess.AuthorizedQuotationIssue).Accepts<QuotationIssuePayload>("application/json");
        Configure(app.MapGet(root + "/{quotationId:guid}", GetAsync), "ReadQuotation", EndpointAccess.AuthorizedQuotationView);
        Configure(app.MapGet(root + "/{quotationId:guid}/issued", HistoryAsync), "ReadIssuedQuotationHistory", EndpointAccess.AuthorizedQuotationView).Produces<QuotationHistoryResponse>(200);
    }
    private static RouteHandlerBuilder Configure(RouteHandlerBuilder route, string name, EndpointAccess access) => route.WithName(name)
        .WithTags("Quotations").WithCoreApiAccess(access).WithCoreApiApplicationAuthorization(access).RequireAuthorization()
        .WithMetadata(new RequestSizeLimitAttribute(MaximumBodyBytes)).Produces<QuotationResponse>(200)
        .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
        .ProducesProblem(413).ProducesProblem(429).ProducesProblem(500).ProducesProblem(503).ProducesProblem(504);

    private static Task<IResult> CreateAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal, ResolveAccountBinding account,
        ResolveTenantContext tenants, QuotationApplication quotes, CancellationToken ct) =>
        DraftAsync(tenantId, null, http, principal, account, tenants, quotes, ct);
    private static Task<IResult> ReviseAsync(Guid tenantId, Guid quotationId, HttpContext http, ClaimsPrincipal principal, ResolveAccountBinding account,
        ResolveTenantContext tenants, QuotationApplication quotes, CancellationToken ct) =>
        DraftAsync(tenantId, quotationId, http, principal, account, tenants, quotes, ct);
    private static async Task<IResult> DraftAsync(Guid tenantId, Guid? id, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenants, QuotationApplication quotes, CancellationToken ct)
    {
        var access = await TenantRequestAccess.ResolveAsync(tenantId, http, principal, account, tenants, ct);
        if (access.Failure is not null) return access.Failure;
        try
        {
            var payload = await ReadAsync<QuotationPayload>(http.Request, ct);
            if (payload.Lines is null || payload.Lines.Count is < 1 or > 100 || payload.Lines.Any(line => line is null)) return Invalid();
            if (id.HasValue ? payload.ExpectedVersion is null : payload.ExpectedVersion is not null) return Invalid();
            if (!TenantOrderEndpoint.TryGetIdempotencyKey(http.Request.Headers, out var key)) return Invalid();
            var until = Instant(payload.ValidUntil);
            var mode = payload.Mode switch
            {
                "manual" => QuotationPriceMode.Manual,
                "catalog" => QuotationPriceMode.Catalog,
                _ => throw new QuotationValidationException("price_mode_invalid", "An explicit supported price mode is required."),
            };
            var request = new QuotationDraftRequest(mode, payload.Summary!, payload.CurrencyCode!, until,
                payload.Lines!.Select(line => new QuotationLineInput(line.Quantity, line.Description, line.UnitCode, line.UnitPrice,
                    line.ItemId, line.UnitId, line.ConversionRevision, line.OverridePrice, line.OverrideReason)).ToArray(), payload.Terms,
                payload.CustomerId, payload.CustomerContext is { } context ? new Application.Customers.CustomerOrderContext(context.OrganizationId, context.ProgramId) : null,
                payload.WholesaleApplicable);
            var result = id.HasValue
                ? await quotes.ReviseAsync(access.TenantContext!, id.Value, payload.ExpectedVersion!.Value, request, key!, ct)
                : await quotes.CreateAsync(access.TenantContext!, request, key!, ct);
            return Result(http, result, !id.HasValue);
        }
        catch (Exception error) when (IsExpected(error)) { return Failure(error); }
    }
    private static async Task<IResult> IssueAsync(Guid tenantId, Guid quotationId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenants, QuotationApplication quotes, CancellationToken ct)
    {
        var access = await TenantRequestAccess.ResolveAsync(tenantId, http, principal, account, tenants, ct);
        if (access.Failure is not null) return access.Failure;
        try
        {
            var request = await ReadAsync<QuotationIssuePayload>(http.Request, ct);
            if (!TenantOrderEndpoint.TryGetIdempotencyKey(http.Request.Headers, out var key)) return Invalid();
            return Result(http, await quotes.IssueAsync(access.TenantContext!, quotationId, request.ExpectedVersion, key!, ct), false);
        }
        catch (Exception error) when (IsExpected(error)) { return Failure(error); }
    }
    private static async Task<IResult> GetAsync(Guid tenantId, Guid quotationId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenants, QuotationApplication quotes, CancellationToken ct)
    {
        var access = await TenantRequestAccess.ResolveAsync(tenantId, http, principal, account, tenants, ct);
        if (access.Failure is not null) return access.Failure;
        try
        {
            var result = await quotes.FindAsync(access.TenantContext!, quotationId, ct);
            return result is null ? NotFound() : TypedResults.Ok(Response(result));
        }
        catch (Exception error) when (IsExpected(error)) { return Failure(error); }
    }
    private static async Task<IResult> HistoryAsync(Guid tenantId, Guid quotationId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenants, QuotationApplication quotes, CancellationToken ct)
    {
        var access = await TenantRequestAccess.ResolveAsync(tenantId, http, principal, account, tenants, ct);
        if (access.Failure is not null) return access.Failure;
        if (!Integer(http, "afterRevision", 0, out var after) || !Integer(http, "limit", 20, out var limit) || limit > int.MaxValue) return Invalid();
        try
        {
            var page = await quotes.ListIssuedAsync(access.TenantContext!, quotationId, after, (int)limit, ct);
            return TypedResults.Ok(new QuotationHistoryResponse(page.Items.Select(IssuedResponse).ToArray(), page.NextAfterRevision));
        }
        catch (Exception error) when (IsExpected(error)) { return Failure(error); }
    }
    private static bool Integer(HttpContext http, string key, long fallback, out long value)
    {
        value = fallback;
        return !http.Request.Query.TryGetValue(key, out var values) || values.Count == 1 &&
            long.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
    private static IResult Result(HttpContext http, QuotationCommandResult result, bool create)
    {
        if (result.Status == QuotationCommandStatus.NotFound) return NotFound();
        if (result.Status is not (QuotationCommandStatus.Created or QuotationCommandStatus.Revised or QuotationCommandStatus.Issued or QuotationCommandStatus.Replayed))
            return TypedResults.Problem(statusCode: 409, title: "Quotation command conflicts with current facts.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = result.Status switch
                    {
                        QuotationCommandStatus.RevisionConflict => "revision_conflict",
                        QuotationCommandStatus.IdempotencyKeyConflict => "idempotency_key_conflict",
                        QuotationCommandStatus.NoDraft => "quotation_no_draft",
                        QuotationCommandStatus.ValidityConflict => "quotation_validity_conflict",
                        _ => "quotation_commercial_facts_conflict",
                    }
                });
        if (result.Status == QuotationCommandStatus.Replayed) http.Response.Headers.Append("Idempotency-Replayed", "true");
        var snapshot = result.Quotation ?? throw new InvalidOperationException("A successful quotation command has no snapshot.");
        return create && result.Status == QuotationCommandStatus.Created
            ? TypedResults.Created($"/api/v1/tenants/{snapshot.TenantId:D}/quotations/{snapshot.QuotationId:D}", Response(snapshot))
            : TypedResults.Ok(Response(snapshot));
    }
    internal static QuotationResponse Response(QuotationSnapshot quote) => new(quote.QuotationId, quote.TenantId, quote.Version, quote.Number,
        quote.CreatedByAccountId, quote.CreatedAt, quote.Draft is null ? null : OfferResponse(quote.Draft),
        quote.CurrentIssued is null ? null : IssuedResponse(quote.CurrentIssued));
    private static QuotationIssuedResponse IssuedResponse(QuotationIssuedFacts facts) => new(facts.RevisionId, facts.RevisionNumber,
        facts.IssuedByAccountId, facts.IssuedAt, OfferResponse(facts.Offer));
    private static QuotationOfferResponse OfferResponse(QuotationDraftFacts offer) => new(offer.Mode.ToString().ToLowerInvariant(), offer.Summary,
        offer.CurrencyCode, offer.ValidUntil, offer.Total, offer.Terms, offer.CustomerId, offer.CustomerContext, offer.WholesaleApplicable,
        offer.Lines.Select(line => new QuotationLineResponse(line.Position, line.Description, line.Quantity, line.UnitCode, line.UnitPrice, line.LineTotal,
            line.Catalog?.ItemId, line.Catalog?.UnitId, line.PriceSelection?.Explanation.SelectedRevisionId,
            line.PriceSelection?.Explanation.PolicyRevision, line.PriceSelection?.Explanation.Override is { } evidence
                ? new QuotationOverrideResponse(evidence.UnitPrice, evidence.Reason, evidence.BeyondPolicy) : null)).ToArray());
    private static DateTimeOffset Instant(string? text)
    {
        if (text is null || text.Length > 64 || !(text.EndsWith('Z') || text.Length >= 6 && text[^6] is '+' or '-' && text[^3] == ':'))
            throw new QuotationValidationException("validity_invalid", "Expiry must be an ISO timestamp with an explicit timezone offset.");
        return JsonSerializer.Deserialize<DateTimeOffset>(JsonSerializer.Serialize(text));
    }
    private static async Task<T> ReadAsync<T>(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasJsonContentType()) throw new JsonException();
        if (request.ContentLength > MaximumBodyBytes) throw new BadHttpRequestException("Quotation input is too large.", 413);
        var bytes = new byte[MaximumBodyBytes + 1];
        var count = await request.Body.ReadAtLeastAsync(bytes, bytes.Length, false, ct);
        if (count > MaximumBodyBytes) throw new BadHttpRequestException("Quotation input is too large.", 413);
        using var json = JsonDocument.Parse(bytes.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 8 });
        if (json.RootElement.ValueKind != JsonValueKind.Object || Duplicated(json.RootElement)) throw new JsonException();
        return json.RootElement.Deserialize<T>(JsonOptions) ?? throw new JsonException();
    }
    private static bool Duplicated(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var member in value.EnumerateObject()) if (!seen.Add(member.Name) || Duplicated(member.Value)) return true;
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) if (Duplicated(item)) return true;
        return false;
    }
    private static bool IsExpected(Exception error) => error is JsonException or BadHttpRequestException or QuotationValidationException
        or QuotationCapabilityDeniedException or AuthorizationProviderUnavailableException or Application.Pricing.PricingValidationException
        or Application.Catalog.CatalogValidationException;
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Failure(Exception error) => error switch
    {
        QuotationCapabilityDeniedException => TypedResults.Problem(statusCode: 403, title: "Quotation operation is not permitted.", extensions: new Dictionary<string, object?> { ["code"] = "quotation_forbidden" }),
        AuthorizationProviderUnavailableException => TypedResults.Problem(statusCode: 503, title: "Authorization is temporarily unavailable.", extensions: new Dictionary<string, object?> { ["code"] = "authorization_unavailable" }),
        BadHttpRequestException { StatusCode: 413 } => TenantOrderEndpoint.RequestTooLarge(),
        QuotationValidationException validation => TenantOrderEndpoint.InvalidRequest(validation.Code, validation.Message),
        Application.Pricing.PricingValidationException => TenantOrderEndpoint.InvalidRequest("pricing_invalid", "Quotation pricing context is invalid."),
        Application.Catalog.CatalogValidationException => TenantOrderEndpoint.InvalidRequest("catalog_invalid", "Quotation catalog context is invalid."),
        _ => Invalid(),
    };
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Invalid() => TenantOrderEndpoint.InvalidRequest("request_invalid", "A strict bounded quotation request is required.");
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult NotFound() => TenantCustomerEndpoint.NotFound("quotation_not_found", "Quotation not found in this tenant.");
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record QuotationPayload([property: JsonRequired] string? Mode, [property: JsonRequired] string? Summary,
    [property: JsonRequired] string? CurrencyCode, [property: JsonRequired] string? ValidUntil,
    [property: JsonRequired] IReadOnlyList<QuotationLinePayload>? Lines, long? ExpectedVersion = null, string? Terms = null,
    Guid? CustomerId = null, QuotationCustomerPayload? CustomerContext = null, bool WholesaleApplicable = false);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record QuotationLinePayload([property: JsonRequired] decimal Quantity, string? Description = null, string? UnitCode = null,
    decimal? UnitPrice = null, Guid? ItemId = null, Guid? UnitId = null, long? ConversionRevision = null, decimal? OverridePrice = null, string? OverrideReason = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record QuotationCustomerPayload([property: JsonRequired] Guid OrganizationId, Guid? ProgramId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record QuotationIssuePayload([property: JsonRequired] long ExpectedVersion);
internal sealed record QuotationResponse(Guid QuotationId, Guid TenantId, long Version, long? Number, Guid CreatedByAccountId,
    DateTimeOffset CreatedAt, QuotationOfferResponse? Draft, QuotationIssuedResponse? CurrentIssued);
internal sealed record QuotationIssuedResponse(Guid RevisionId, long RevisionNumber, Guid IssuedByAccountId, DateTimeOffset IssuedAt, QuotationOfferResponse Offer);
internal sealed record QuotationOfferResponse(string Mode, string Summary, string CurrencyCode, DateTimeOffset ValidUntil, decimal Total,
    string? Terms, Guid? CustomerId, Application.Customers.CustomerOrderContext? CustomerContext, bool WholesaleApplicable, IReadOnlyList<QuotationLineResponse> Lines);
internal sealed record QuotationLineResponse(int Position, string Description, decimal Quantity, string UnitCode, decimal UnitPrice, decimal LineTotal,
    Guid? ItemId, Guid? UnitId, Guid? PriceRevisionId, long? PolicyRevision, QuotationOverrideResponse? Override);
internal sealed record QuotationOverrideResponse(decimal UnitPrice, string Reason, bool BeyondPolicy);

internal sealed record QuotationHistoryResponse(IReadOnlyList<QuotationIssuedResponse> Items, long? NextAfterRevision);
