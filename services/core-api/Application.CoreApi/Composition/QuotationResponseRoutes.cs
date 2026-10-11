using System.Security.Claims;
using System.Text.Json.Serialization;
using Application.CoreApi.Authorization;
using Application.IdentityAccess;
using Application.Orders;
using Application.Quotations;
using Application.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class QuotationResponseRoutes
{
    private const int MaximumBodyBytes = 64 * 1024;

    internal static void MapQuotationResponseEndpoints(this WebApplication app)
    {
        const string root = "/api/v1/tenants/{tenantId:guid}/quotations/{quotationId:guid}";
        Configure(app.MapPost(root + "/accept", (Guid tenantId, Guid quotationId, HttpContext http, ClaimsPrincipal principal,
            ResolveAccountBinding account, ResolveTenantContext tenants, QuotationApplication quotes, CancellationToken ct) =>
            RespondAsync(tenantId, quotationId, QuotationResponseKind.Accepted, http, principal, account, tenants, quotes, ct)),
            "AcceptQuotation", EndpointAccess.AuthorizedQuotationRespond).Accepts<QuotationResponsePayload>("application/json").Produces<QuotationCommandResponse>(200);
        Configure(app.MapPost(root + "/reject", (Guid tenantId, Guid quotationId, HttpContext http, ClaimsPrincipal principal,
            ResolveAccountBinding account, ResolveTenantContext tenants, QuotationApplication quotes, CancellationToken ct) =>
            RespondAsync(tenantId, quotationId, QuotationResponseKind.Rejected, http, principal, account, tenants, quotes, ct)),
            "RejectQuotation", EndpointAccess.AuthorizedQuotationRespond).Accepts<QuotationResponsePayload>("application/json").Produces<QuotationCommandResponse>(200);
        Configure(app.MapPost(root + "/expire", ExpireAsync), "ExpireQuotation", EndpointAccess.AuthorizedQuotationExpire)
            .Accepts<QuotationResponsePayload>("application/json").Produces<QuotationCommandResponse>(200);
        Configure(app.MapPost(root + "/convert", ConvertAsync), "ConvertAcceptedQuotation", EndpointAccess.AuthorizedQuotationConvert)
            .Accepts<QuotationConvertPayload>("application/json").Produces<QuotationCommandResponse>(200);
        Configure(app.MapGet(root + "/issued/{revisionId:guid}/response", FindResponseAsync),
            "ReadQuotationResponse", EndpointAccess.AuthorizedQuotationView).Produces<QuotationResponseFactResponse>(200);
    }

    private static RouteHandlerBuilder Configure(RouteHandlerBuilder route, string name, EndpointAccess access) => route
        .WithName(name).WithTags("Quotations").WithCoreApiAccess(access).WithCoreApiApplicationAuthorization(access).RequireAuthorization()
        .WithMetadata(new RequestSizeLimitAttribute(MaximumBodyBytes))
        .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
        .ProducesProblem(413).ProducesProblem(429).ProducesProblem(500).ProducesProblem(503).ProducesProblem(504);

    private static Task<IResult> ExpireAsync(Guid tenantId, Guid quotationId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenants, QuotationApplication quotes, CancellationToken ct) =>
        RespondAsync(tenantId, quotationId, QuotationResponseKind.Expired, http, principal, account, tenants, quotes, ct);

    private static async Task<IResult> RespondAsync(Guid tenantId, Guid quotationId, QuotationResponseKind kind, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenants, QuotationApplication quotes, CancellationToken ct)
    {
        var access = await TenantRequestAccess.ResolveAsync(tenantId, http, principal, account, tenants, ct);
        if (access.Failure is not null) return access.Failure;
        try
        {
            var payload = await QuotationRoutes.ReadAsync<QuotationResponsePayload>(http.Request, ct);
            if (!TenantOrderEndpoint.TryGetIdempotencyKey(http.Request.Headers, out var key)) return QuotationRoutes.Invalid();
            var request = new QuotationResponseRequest(payload.IssuedRevisionId, payload.ExpectedVersion, payload.Evidence!, payload.CustomerClaim);
            var result = kind switch
            {
                QuotationResponseKind.Accepted => await quotes.AcceptAsync(access.TenantContext!, quotationId, request, key!, ct),
                QuotationResponseKind.Rejected => await quotes.RejectAsync(access.TenantContext!, quotationId, request, key!, ct),
                _ => await quotes.ExpireAsync(access.TenantContext!, quotationId, request, key!, ct),
            };
            return Result(http, result);
        }
        catch (Exception error) when (QuotationRoutes.IsExpected(error)) { return QuotationRoutes.Failure(error); }
    }

    private static async Task<IResult> ConvertAsync(Guid tenantId, Guid quotationId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenants, QuotationApplication quotes, CancellationToken ct)
    {
        var access = await TenantRequestAccess.ResolveAsync(tenantId, http, principal, account, tenants, ct);
        if (access.Failure is not null) return access.Failure;
        try
        {
            var payload = await QuotationRoutes.ReadAsync<QuotationConvertPayload>(http.Request, ct);
            if (!TenantOrderEndpoint.TryGetIdempotencyKey(http.Request.Headers, out var key)) return QuotationRoutes.Invalid();
            var result = await quotes.ConvertAsync(access.TenantContext!, quotationId,
                new QuotationConvertRequest(payload.IssuedRevisionId, payload.ExpectedVersion), key!, ct);
            return Result(http, result);
        }
        catch (Exception error) when (QuotationRoutes.IsExpected(error)) { return QuotationRoutes.Failure(error); }
    }

    private static async Task<IResult> FindResponseAsync(Guid tenantId, Guid quotationId, Guid revisionId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenants, QuotationApplication quotes, CancellationToken ct)
    {
        var access = await TenantRequestAccess.ResolveAsync(tenantId, http, principal, account, tenants, ct);
        if (access.Failure is not null) return access.Failure;
        try
        {
            var response = await quotes.FindResponseAsync(access.TenantContext!, quotationId, revisionId, ct);
            return response is null ? TenantCustomerEndpoint.NotFound("quotation_response_not_found", "Quotation response not found.") : TypedResults.Ok(Response(response));
        }
        catch (Exception error) when (QuotationRoutes.IsExpected(error)) { return QuotationRoutes.Failure(error); }
    }

    private static IResult Result(HttpContext http, QuotationCommandResult result)
    {
        if (result.Status == QuotationCommandStatus.NotFound)
            return TenantCustomerEndpoint.NotFound("quotation_not_found", "Quotation not found in this tenant.");
        if (result.Status is not (QuotationCommandStatus.Accepted or QuotationCommandStatus.Rejected or QuotationCommandStatus.Expired
            or QuotationCommandStatus.Converted or QuotationCommandStatus.AlreadyLinked or QuotationCommandStatus.Replayed))
            return TypedResults.Problem(statusCode: 409, title: "Quotation response conflicts with current facts.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = result.Status switch
                    {
                        QuotationCommandStatus.RevisionConflict => "revision_conflict",
                        QuotationCommandStatus.IdempotencyKeyConflict => "idempotency_key_conflict",
                        QuotationCommandStatus.Superseded => "quotation_revision_superseded",
                        QuotationCommandStatus.AlreadyResponded => "quotation_already_responded",
                        QuotationCommandStatus.NotAccepted => "quotation_not_accepted",
                        QuotationCommandStatus.AcceptedFamily => "quotation_family_accepted",
                        QuotationCommandStatus.ValidityConflict => "quotation_validity_conflict",
                        _ => "quotation_response_conflict",
                    }
                });
        if (result.Status == QuotationCommandStatus.Replayed) http.Response.Headers.Append("Idempotency-Replayed", "true");
        var quote = result.Quotation ?? throw new InvalidOperationException("A successful quotation response has no snapshot.");
        return TypedResults.Ok(new QuotationCommandResponse(quote.QuotationId, quote.TenantId, quote.Version,
            quote.CurrentResponse is null ? null : Response(quote.CurrentResponse),
            quote.Conversion is null ? null : Conversion(quote.Conversion)));
    }

    internal static QuotationResponseFactResponse Response(QuotationResponseFacts facts) => new(facts.QuotationId, facts.IssuedRevisionId,
        facts.Kind.ToString().ToLowerInvariant(), facts.RecordedByAccountId, facts.RecordedAt, facts.Evidence, facts.CustomerClaim);

    internal static QuotationConversionLinkResponse Conversion(QuotationConversionFacts facts) => new(facts.QuotationId,
        facts.IssuedRevisionId, facts.ConvertedByAccountId, facts.ConvertedAt,
        new(facts.OriginalOrder.OrderId, facts.OriginalOrder.Revision));
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record QuotationResponsePayload([property: JsonRequired] Guid IssuedRevisionId,
    [property: JsonRequired] long ExpectedVersion, [property: JsonRequired] string? Evidence, string? CustomerClaim = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record QuotationConvertPayload([property: JsonRequired] Guid IssuedRevisionId,
    [property: JsonRequired] long ExpectedVersion);
internal sealed record QuotationCommandResponse(Guid QuotationId, Guid TenantId, long Version,
    QuotationResponseFactResponse? Response, QuotationConversionLinkResponse? Conversion);
internal sealed record QuotationResponseFactResponse(Guid QuotationId, Guid IssuedRevisionId, string Kind,
    Guid RecordedByAccountId, DateTimeOffset RecordedAt, string Evidence, string? CustomerClaim);
internal sealed record QuotationConversionLinkResponse(Guid QuotationId, Guid IssuedRevisionId, Guid ConvertedByAccountId,
    DateTimeOffset ConvertedAt, QuotationOrderIdentityResponse OriginalOrder);
internal sealed record QuotationOrderIdentityResponse(Guid OrderId, long Revision);
