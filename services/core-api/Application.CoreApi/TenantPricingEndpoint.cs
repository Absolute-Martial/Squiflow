using System.Security.Claims;
using System.Text.Json.Serialization;
using Application.CoreApi.Authorization;
using Application.IdentityAccess;
using Application.Pricing;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Application.CoreApi;

internal static class TenantPricingEndpoint
{
    internal static async Task<IResult> CreateDraftAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        PricingApplication pricing, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TryKey(http, out var key)) return InvalidKey();
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<PricingDraftPayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var body = payload.Value!;
            var scope = ParseScope(body.Scope, body.TargetId);
            var request = new CreatePriceDraftRequest(body.ItemId, "UNIT", body.CurrencyCode ?? "", scope,
                body.BaseUnitPrice, new PriceValidity(body.ValidFrom.ToUniversalTime(), body.ValidTo?.ToUniversalTime()), body.UnitId, body.PriceId, body.ConversionRevision);
            var result = await pricing.CreateDraftAsync(Actor(access.Context!), request, key!, ct);
            if (result.Status == CreatePriceDraftStatus.IdempotencyKeyConflict) return TenantCustomerEndpoint.Conflict("Idempotency key conflict.", "The Idempotency-Key was used for a different pricing request.");
            var price = result.Price ?? throw new InvalidOperationException("Pricing create returned no snapshot.");
            RequireTenant(price, tenantId);
            var response = ToResponse(price);
            if (result.Status == CreatePriceDraftStatus.Replayed) { Replay(http); return TypedResults.Ok(response); }
            return TypedResults.Created($"/api/v1/tenants/{tenantId:D}/pricing/revisions/{price.RevisionId:D}", response);
        }
        catch (ArgumentException error) { return Invalid(error); }
    }

    internal static async Task<IResult> PublishAsync(Guid tenantId, Guid revisionId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        PricingApplication pricing, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TryKey(http, out var key)) return InvalidKey();
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<PricingPublishPayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var result = await pricing.PublishAsync(Actor(access.Context!), new PublishPriceRequest(revisionId, payload.Value!.SupersedeRevisionId), key!, ct);
            if (result.Status == PublishPriceStatus.NotFound) return MissingRevision();
            if (result.Status == PublishPriceStatus.IdempotencyKeyConflict) return TenantCustomerEndpoint.Conflict("Idempotency key conflict.", "The Idempotency-Key was used for a different pricing request.");
            if (result.Status is PublishPriceStatus.RevisionConflict or PublishPriceStatus.PublicationConflict)
                return Conflict(result.Status == PublishPriceStatus.PublicationConflict ? "pricing_publication_conflict" : "pricing_revision_conflict");
            var price = result.Price ?? throw new InvalidOperationException("Pricing publication returned no snapshot.");
            RequireTenant(price, tenantId);
            if (result.Status == PublishPriceStatus.Replayed) Replay(http);
            return TypedResults.Ok(ToResponse(price));
        }
        catch (ArgumentException error) { return Invalid(error); }
    }

    internal static async Task<IResult> RetireAsync(Guid tenantId, Guid revisionId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        IPricingPublicationStore store, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TryKey(http, out var key)) return InvalidKey();
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<PricingRetirePayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var result = await store.RetireAsync(Actor(access.Context!), new RetirePriceRequest(revisionId), key!, ct);
            if (result.Status == RetirePriceStatus.NotFound) return MissingRevision();
            if (result.Status == RetirePriceStatus.IdempotencyKeyConflict) return TenantCustomerEndpoint.Conflict("Idempotency key conflict.", "The Idempotency-Key was used for a different pricing request.");
            if (result.Status == RetirePriceStatus.RevisionConflict) return Conflict("pricing_revision_conflict");
            var price = result.Price ?? throw new InvalidOperationException("Pricing retirement returned no snapshot.");
            RequireTenant(price, tenantId);
            if (result.Status == RetirePriceStatus.Replayed) Replay(http);
            return TypedResults.Ok(ToResponse(price));
        }
        catch (ArgumentException error) { return Invalid(error); }
    }

    internal static async Task<IResult> ResolveAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        ITenantPricingAuthorization permissions, PricingApplication pricing, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<PricingSelectionPayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var context = access.Context!;
            var body = payload.Value!;
            var canOverride = false;
            var canBeyond = false;
            if (body.OverridePrice.HasValue)
            {
                canOverride = await permissions.CanOverrideAsync(context.AccountId, context.TenantId, ct);
            }
            var result = await pricing.ResolveAsync(Actor(context), body.ItemId, body.UnitId, body.CurrencyCode ?? "",
                new(body.CustomerId, body.ProgramId, body.OrganizationId, WholesaleApplicable: body.WholesaleApplicable, UnitConversionRevision: body.ConversionRevision),
                null, null, false, false, ct);
            if (body.OverridePrice.HasValue)
            {
                if (canOverride && PriceSelectionEngine.RequiresElevatedOverride(result, body.OverridePrice.Value))
                    canBeyond = await permissions.CanOverrideBeyondPolicyAsync(context.AccountId, context.TenantId, ct);
                result = PriceSelectionEngine.ApplyOverride(result, new(body.OverridePrice.Value, body.Reason ?? "", canOverride, canBeyond));
            }
            return TypedResults.Ok(new PricingResolutionResponse(result.Status.ToString(),
                result is PriceResolved resolved ? resolved.UnitPrice : null,
                result.Explanation.SelectedPriceId, result.Explanation.SelectedRevisionId, result.Explanation.SelectedRevision,
                result.Explanation.SelectedScope?.Kind.ToString(), result.Explanation.PolicyRevision, result.Explanation.EvaluatedAt,
                result is PriceOverrideRequired required ? required.Reason : result.Explanation.Why,
                result.Explanation.Candidates.Select(candidate => new PricingCandidateResponse(candidate.RevisionId, candidate.RevisionNumber,
                    candidate.Scope.Kind.ToString(), candidate.Validity.ValidFrom, candidate.Validity.ValidTo, candidate.BaseUnitPrice)).ToArray(),
                result.Explanation.Override));
        }
        catch (AuthorizationProviderUnavailableException) { return Unavailable(); }
        catch (ArgumentException error) { return Invalid(error); }
    }

    internal static async Task<IResult> GetAsync(Guid tenantId, Guid revisionId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        IPricingPublicationStore store, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        try
        {
            var result = await store.FindAsync(Actor(access.Context!), revisionId, ct);
            if (result is null) return MissingRevision();
            RequireTenant(result, tenantId);
            return TypedResults.Ok(ToResponse(result));
        }
        catch (ArgumentException error) { return Invalid(error); }
    }

    internal static async Task<IResult> HistoryAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        IPricingPublicationStore store, IPricingReferenceReader references, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<PricingHistoryPayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var body = payload.Value!;
            var actor = Actor(access.Context!);
            var context = new PriceSelectionContext(body.CustomerId, body.ProgramId, body.OrganizationId, WholesaleApplicable: body.WholesaleApplicable, UnitConversionRevision: body.ConversionRevision);
            PricingApplication.RequirePublicContext(context);
            await references.RequireContextAsync(actor, context, ct);
            var code = await references.RequireCompatibleUnitAsync(actor, body.ItemId, body.UnitId, body.ConversionRevision, ct);
            var rows = await store.ListRevisionsAsync(actor, new PriceLookupRequest(body.ItemId, code, body.CurrencyCode ?? "", body.UnitId,
                context, body.Limit, body.AfterRevision), ct);
            foreach (var row in rows) RequireTenant(row, tenantId);
            return TypedResults.Ok(new PricingHistoryResponse(rows.Select(ToResponse).ToArray(), rows.Count == body.Limit ? rows[^1].RevisionNumber : null));
        }
        catch (ArgumentException error) { return Invalid(error); }
    }

    internal static async Task<IResult> GetPolicyAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        IPricingPolicyStore policies, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        var snapshot = await policies.GetCurrentPolicyAsync(Actor(access.Context!), ct);
        if (snapshot is not null && snapshot.TenantId != tenantId) throw new InvalidOperationException("Pricing policy tenant mismatch.");
        return snapshot is null ? TenantCustomerEndpoint.NotFound("pricing_policy_missing", "No pricing policy is published.") : TypedResults.Ok(snapshot);
    }

    internal static async Task<IResult> PublishPolicyAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        IPricingPolicyStore policies, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TryKey(http, out var key)) return InvalidKey();
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<PricingPolicyPayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var body = payload.Value!;
            var result = await policies.PublishPolicyAsync(Actor(access.Context!), new PublishPricingPolicyRequest(body.ExpectedRevision,
                body.MinimumUnitPrice, body.MaximumUnitPrice, body.MaximumDecreasePercent, body.MaximumIncreasePercent), key!, ct);
            if (result.Status == PublishPricingPolicyStatus.IdempotencyKeyConflict) return TenantCustomerEndpoint.Conflict("Idempotency key conflict.", "The Idempotency-Key was used for a different pricing request.");
            if (result.Status == PublishPricingPolicyStatus.RevisionConflict) return Conflict("pricing_policy_revision_conflict");
            if (result.Snapshot is null || result.Snapshot.TenantId != tenantId) throw new InvalidOperationException("Pricing policy publication returned inconsistent facts.");
            if (result.Status == PublishPricingPolicyStatus.Replayed) Replay(http);
            return TypedResults.Ok(result.Snapshot);
        }
        catch (ArgumentException error) { return Invalid(error); }
    }

    private static PriceScope ParseScope(string? scope, Guid? target) => scope switch
    {
        "default" => new(PriceScopeKind.Default, target),
        "wholesale" when target is null => PriceScope.Wholesale(),
        "customer" => new(PriceScopeKind.Customer, target),
        "program" => new(PriceScopeKind.Program, target),
        "organization" => new(PriceScopeKind.Organization, target),
        _ => throw new PricingValidationException("pricing_scope_unsupported", "Scope must be default, wholesale, customer, program or organization."),
    };

    private static PricingActorContext Actor(TenantContext context) => new(context.TenantId, context.AccountId);
    private static void RequireTenant(PriceRevision value, Guid tenantId)
    { if (value.TenantId != tenantId) throw new InvalidOperationException("Pricing operation returned another tenant's facts."); }
    private static bool TryKey(HttpContext http, out string? key) => TenantCustomerEndpoint.TryKey(http.Request, out key) &&
        !string.IsNullOrWhiteSpace(key) && key.Length <= 128 && !key.Any(char.IsControl);
    private static ProblemHttpResult InvalidKey() => TenantCustomerEndpoint.Invalid("idempotency_key_invalid", "One bounded Idempotency-Key is required.");
    private static void Replay(HttpContext http) => http.Response.Headers.Append("Idempotency-Replayed", "true");
    private static ProblemHttpResult MissingRevision() => TenantCustomerEndpoint.NotFound("Price revision not found.", "pricing_revision_not_found", "Price revision not found in this tenant.");
    private static ProblemHttpResult Conflict(string code) => TypedResults.Problem(statusCode: 409, title: "Pricing command conflicts with current facts.",
        extensions: new Dictionary<string, object?> { ["code"] = code });
    private static ProblemHttpResult Unavailable() => TypedResults.Problem(statusCode: 503, title: "Authorization is temporarily unavailable.",
        extensions: new Dictionary<string, object?> { ["code"] = "authorization_unavailable" });
    private static ProblemHttpResult Invalid(ArgumentException error) => error is PricingValidationException validation
        ? validation.Code == "tenant_access_invalid" ? TenantRequestAccess.Problem(validation.Code, validation.Message)
            : TenantCustomerEndpoint.Invalid(validation.Code, validation.Message)
        : TenantCustomerEndpoint.Invalid("pricing_request_invalid", "Pricing request contains invalid values.");
    private static PricingRevisionResponse ToResponse(PriceRevision value) => new(value.PriceId, value.RevisionId, value.RevisionNumber,
        value.Key.ItemId, value.Key.UnitId, value.Key.UnitCode, value.Key.CurrencyCode, value.Key.Scope.Kind.ToString(), value.Key.Scope.TargetId,
        value.BaseUnitPrice, value.Validity.ValidFrom, value.Validity.ValidTo, value.State.ToString(), value.CreatedAt, value.PublishedAt, value.RetiredAt, value.Key.UnitConversionRevision);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record PricingDraftPayload([property: JsonRequired] Guid ItemId, [property: JsonRequired] Guid UnitId,
    [property: JsonRequired] string? CurrencyCode, [property: JsonRequired] string? Scope, Guid? TargetId,
    [property: JsonRequired] decimal BaseUnitPrice, [property: JsonRequired] DateTimeOffset ValidFrom, DateTimeOffset? ValidTo = null, Guid? PriceId = null, long? ConversionRevision = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record PricingPublishPayload(Guid? SupersedeRevisionId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record PricingRetirePayload;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record PricingSelectionPayload([property: JsonRequired] Guid ItemId, [property: JsonRequired] Guid UnitId, [property: JsonRequired] string? CurrencyCode, Guid? CustomerId = null,
    Guid? ProgramId = null, Guid? OrganizationId = null, bool WholesaleApplicable = false, decimal? OverridePrice = null, string? Reason = null, long? ConversionRevision = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record PricingHistoryPayload([property: JsonRequired] Guid ItemId, [property: JsonRequired] Guid UnitId, [property: JsonRequired] string? CurrencyCode, Guid? CustomerId = null,
    Guid? ProgramId = null, Guid? OrganizationId = null, bool WholesaleApplicable = false, int Limit = 25, long? AfterRevision = null, long? ConversionRevision = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record PricingPolicyPayload([property: JsonRequired] long ExpectedRevision, [property: JsonRequired] decimal MinimumUnitPrice, [property: JsonRequired] decimal MaximumUnitPrice,
    decimal? MaximumDecreasePercent = null, decimal? MaximumIncreasePercent = null);
internal sealed record PricingRevisionResponse(Guid PriceId, Guid RevisionId, long Revision, Guid ItemId, Guid UnitId, string UnitCode,
    string CurrencyCode, string Scope, Guid? TargetId, decimal BaseUnitPrice, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo,
    string State, DateTimeOffset CreatedAt, DateTimeOffset? PublishedAt, DateTimeOffset? RetiredAt, long? ConversionRevision);
internal sealed record PricingCandidateResponse(Guid RevisionId, long Revision, string Scope, DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo, decimal? BaseUnitPrice);
internal sealed record PricingResolutionResponse(string Status, decimal? UnitPrice, Guid? PriceId, Guid? RevisionId, long? Revision,
    string? Source, long PolicyRevision, DateTimeOffset EvaluatedAt, string Why, IReadOnlyList<PricingCandidateResponse> Candidates, PriceOverrideEvidence? Override);
internal sealed record PricingHistoryResponse(IReadOnlyList<PricingRevisionResponse> Items, long? AfterRevision);
