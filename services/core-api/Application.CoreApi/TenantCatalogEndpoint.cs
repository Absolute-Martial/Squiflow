using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Catalog;
using Application.IdentityAccess;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi;

internal static class TenantCatalogEndpoint
{
    internal const long MaximumBodyBytes = 8192;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        MaxDepth = 2,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static Task<IResult> CreateUnitAsync(Guid tenantId, HttpContext http, CancellationToken ct) =>
        MutateAsync<CreateCatalogUnitPayload>(tenantId, http, async (context, payload, key) =>
        {
            var result = await http.RequestServices.GetRequiredService<CreateCatalogUnit>().ExecuteAsync(context,
                new CreateCatalogUnitRequest(payload.Code ?? string.Empty, payload.Name ?? string.Empty, payload.Precision), key, ct);
            return CreateResponse(http, result.Status.ToString(), result.Unit is null ? null : UnitResponse(result.Unit, context),
                result.Unit?.UnitId, $"/api/v1/tenants/{tenantId:D}/catalog/units");
        }, ct);

    internal static Task<IResult> CreateItemAsync(Guid tenantId, HttpContext http, CancellationToken ct) =>
        MutateAsync<CreateCatalogItemPayload>(tenantId, http, async (context, payload, key) =>
        {
            var result = await http.RequestServices.GetRequiredService<CreateCatalogItem>().ExecuteAsync(context,
                new CreateCatalogItemRequest(payload.Code, payload.Name ?? string.Empty, payload.Description,
                    ParseKind(payload.Kind), payload.BaseUnitId, ParseStockMode(payload.StockMode)), key, ct);
            return CreateResponse(http, result.Status.ToString(), result.Item is null ? null : ItemResponse(result.Item, context),
                result.Item?.ItemId, $"/api/v1/tenants/{tenantId:D}/catalog/items");
        }, ct);

    internal static Task<IResult> GetUnitAsync(Guid tenantId, Guid unitId, HttpContext http, CancellationToken ct) =>
        ReadAsync(tenantId, http, async context =>
        {
            var value = await http.RequestServices.GetRequiredService<GetCatalogUnit>().ExecuteAsync(context, unitId, ct);
            return value is null ? Problem(404, "unit_not_found") : TypedResults.Ok(UnitResponse(value, context, unitId));
        }, ct);

    internal static Task<IResult> GetItemAsync(Guid tenantId, Guid itemId, HttpContext http, CancellationToken ct) =>
        ReadAsync(tenantId, http, async context =>
        {
            var value = await http.RequestServices.GetRequiredService<GetCatalogItem>().ExecuteAsync(context, itemId, ct);
            return value is null ? Problem(404, "item_not_found") : TypedResults.Ok(ItemResponse(value, context, itemId));
        }, ct);

    internal static Task<IResult> ListUnitsAsync(Guid tenantId, HttpContext http, CancellationToken ct) =>
        ReadAsync(tenantId, http, async context =>
        {
            var (limit, after) = ReadPage(http.Request.Query, tenantId, "units");
            var page = await http.RequestServices.GetRequiredService<ListCatalogUnits>().ExecuteAsync(context,
                new ListCatalogUnitsRequest(limit, after is null ? null : new(after.Value.At, after.Value.Id)), ct);
            if (page.Items.Count > limit) throw new InvalidOperationException("Catalog unit browse exceeded its bound.");
            return TypedResults.Ok(new CatalogUnitPageResponse(page.Items.Select(value => UnitResponse(value, context)).ToArray(),
                page.NextCursor is null ? null : EncodeCursor(tenantId, "units", page.NextCursor.CreatedAt, page.NextCursor.UnitId)));
        }, ct);

    internal static Task<IResult> ListItemsAsync(Guid tenantId, HttpContext http, CancellationToken ct) =>
        ReadAsync(tenantId, http, async context =>
        {
            var (limit, after) = ReadPage(http.Request.Query, tenantId, "items");
            var page = await http.RequestServices.GetRequiredService<ListCatalogItems>().ExecuteAsync(context,
                new ListCatalogItemsRequest(limit, after is null ? null : new(after.Value.At, after.Value.Id)), ct);
            if (page.Items.Count > limit) throw new InvalidOperationException("Catalog item browse exceeded its bound.");
            return TypedResults.Ok(new CatalogItemPageResponse(page.Items.Select(value => ItemResponse(value, context)).ToArray(),
                page.NextCursor is null ? null : EncodeCursor(tenantId, "items", page.NextCursor.CreatedAt, page.NextCursor.ItemId)));
        }, ct);

    internal static Task<IResult> RenameUnitAsync(Guid tenantId, Guid unitId, HttpContext http, CancellationToken ct) =>
        MutateAsync<CatalogUnitDisplayPayload>(tenantId, http, async (context, payload, key) =>
        {
            var result = await http.RequestServices.GetRequiredService<RenameCatalogUnit>().ExecuteAsync(context,
                new RenameCatalogUnitRequest(unitId, payload.ExpectedRevision, payload.Name ?? string.Empty), key, ct);
            if (result.Unit is not null) ValidateIdentity(result.Unit.TenantId, result.Unit.UnitId, context, unitId);
            return MutationResponse(http, result.Status.ToString(), result.Unit is null ? null :
                new CatalogMutationResponse(unitId, result.Unit.Revision, WireStatus(result.Unit.Status), null));
        }, ct);

    internal static Task<IResult> RenameItemAsync(Guid tenantId, Guid itemId, HttpContext http, CancellationToken ct) =>
        MutateAsync<CatalogItemDisplayPayload>(tenantId, http, async (context, payload, key) =>
        {
            var result = await http.RequestServices.GetRequiredService<RenameCatalogItem>().ExecuteAsync(context,
                new RenameCatalogItemRequest(itemId, payload.ExpectedRevision, payload.Name ?? string.Empty, payload.Description), key, ct);
            if (result.Item is not null) ValidateIdentity(result.Item.TenantId, result.Item.ItemId, context, itemId);
            return MutationResponse(http, result.Status.ToString(), result.Item is null ? null :
                new CatalogMutationResponse(itemId, result.Item.Revision, WireStatus(result.Item.Status), null));
        }, ct);

    internal static Task<IResult> RetireUnitAsync(Guid tenantId, Guid unitId, HttpContext http, CancellationToken ct) =>
        MutateAsync<CatalogRevisionPayload>(tenantId, http, async (context, payload, key) =>
        {
            var result = await http.RequestServices.GetRequiredService<RetireCatalogUnit>().ExecuteAsync(context,
                new RetireCatalogUnitRequest(unitId, payload.ExpectedRevision), key, ct);
            if (result.Unit is not null) ValidateIdentity(result.Unit.TenantId, result.Unit.UnitId, context, unitId);
            return MutationResponse(http, result.Status.ToString(), result.Unit is null ? null :
                new CatalogMutationResponse(unitId, result.Unit.Revision, WireStatus(result.Unit.Status), result.Unit.RetiredAt));
        }, ct);

    internal static Task<IResult> RetireItemAsync(Guid tenantId, Guid itemId, HttpContext http, CancellationToken ct) =>
        MutateAsync<CatalogRevisionPayload>(tenantId, http, async (context, payload, key) =>
        {
            var result = await http.RequestServices.GetRequiredService<RetireCatalogItem>().ExecuteAsync(context,
                new RetireCatalogItemRequest(itemId, payload.ExpectedRevision), key, ct);
            if (result.Item is not null) ValidateIdentity(result.Item.TenantId, result.Item.ItemId, context, itemId);
            return MutationResponse(http, result.Status.ToString(), result.Item is null ? null :
                new CatalogMutationResponse(itemId, result.Item.Revision, WireStatus(result.Item.Status), result.Item.RetiredAt));
        }, ct);

    internal static Task<IResult> ChangeAvailabilityAsync(Guid tenantId, Guid itemId, HttpContext http, CancellationToken ct) =>
        MutateAsync<CatalogAvailabilityPayload>(tenantId, http, async (context, payload, key) =>
        {
            var requested = payload.Availability switch
            {
                "available" => CatalogAvailability.Available,
                "unavailable" => CatalogAvailability.Unavailable,
                _ => throw new CatalogValidationException("availability_invalid", "Availability must be available or unavailable."),
            };
            var result = await http.RequestServices.GetRequiredService<ChangeCatalogAvailability>().ExecuteAsync(context,
                new ChangeCatalogAvailabilityRequest(itemId, payload.ExpectedRevision, requested), key, ct);
            if (result.Item is not null) ValidateIdentity(result.Item.TenantId, result.Item.ItemId, context, itemId);
            return MutationResponse(http, result.Status.ToString(), result.Item is null ? null :
                new CatalogMutationResponse(itemId, result.Item.Revision, WireAvailability(result.Item.Availability)!, result.Item.AvailabilityChangedAt));
        }, ct);

    internal static Task<IResult> PublishConversionAsync(Guid tenantId, Guid sourceUnitId, Guid targetUnitId, HttpContext http, CancellationToken ct) =>
        MutateAsync<CatalogConversionPayload>(tenantId, http, async (context, payload, key) =>
        {
            var result = await http.RequestServices.GetRequiredService<PublishCatalogConversion>().ExecuteAsync(context,
                new PublishCatalogConversionRequest(sourceUnitId, targetUnitId, payload.ExpectedRevision, payload.Numerator, payload.Denominator), key, ct);
            return MutationResponse(http, result.Status.ToString(), result.Conversion is null ? null :
                ConversionResponse(result.Conversion, context, sourceUnitId, targetUnitId));
        }, ct);

    internal static Task<IResult> GetConversionAsync(Guid tenantId, Guid sourceUnitId, Guid targetUnitId, HttpContext http, CancellationToken ct) =>
        ReadAsync(tenantId, http, async context =>
        {
            var query = http.Request.Query;
            if (query.Count != 1 || !query.TryGetValue("revision", out var values) || values.Count != 1 ||
                !long.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out var revision))
                return Problem(400, "conversion_revision_invalid");
            var value = await http.RequestServices.GetRequiredService<GetCatalogConversion>().ExecuteAsync(context, sourceUnitId, targetUnitId, revision, ct);
            return value is null ? Problem(404, "conversion_not_found") : TypedResults.Ok(ConversionResponse(value, context, sourceUnitId, targetUnitId));
        }, ct);

    internal static Task<IResult> SelectLineFactsAsync(Guid tenantId, HttpContext http, CancellationToken ct) =>
        PayloadAsync<CatalogSelectionPayload>(tenantId, http, false, async (context, payload, _) =>
        {
            var result = await http.RequestServices.GetRequiredService<SelectCatalogLineFacts>().ExecuteAsync(context,
                new CatalogLineSelection(payload.ItemId, payload.UnitId, payload.Quantity, payload.ConversionRevision), ct);
            if (result.Status != CatalogLineFactsStatus.Available) return Failure(result.Status.ToString());
            var facts = result.Facts ?? throw new InvalidOperationException("Catalog selection returned no facts.");
            if (facts.ItemId != payload.ItemId || facts.UnitId != payload.UnitId)
                throw new InvalidOperationException("Catalog selection returned inconsistent identities.");
            return TypedResults.Ok(facts);
        }, ct);

    private static Task<IResult> MutateAsync<T>(Guid tenantId, HttpContext http,
        Func<TenantContext, T, string, Task<IResult>> operation, CancellationToken ct) => PayloadAsync(tenantId, http, true, operation, ct);

    private static async Task<IResult> ReadAsync(Guid tenantId, HttpContext http,
        Func<TenantContext, Task<IResult>> operation, CancellationToken ct)
    {
        var access = await ResolveAsync(tenantId, http, ct);
        if (access.Failure is not null) return access.Failure;
        try { return await operation(access.Context!); }
        catch (CatalogValidationException error) { return Problem(400, error.Code, error.Message); }
    }

    private static async Task<IResult> PayloadAsync<T>(Guid tenantId, HttpContext http, bool needsKey,
        Func<TenantContext, T, string, Task<IResult>> operation, CancellationToken ct)
    {
        var access = await ResolveAsync(tenantId, http, ct);
        if (access.Failure is not null) return access.Failure;
        try
        {
            var key = string.Empty;
            if (needsKey)
            {
                if (!TenantCustomerEndpoint.TryKey(http.Request, out var supplied)) return Problem(400, "idempotency_key_invalid");
                key = CatalogRules.NormalizeIdempotencyKey(supplied!);
            }
            var payload = await ReadPayloadAsync<T>(http.Request, ct);
            if (payload.Failure is not null) return payload.Failure;
            return await operation(access.Context!, payload.Value!, key);
        }
        catch (CatalogValidationException error) { return Problem(400, error.Code, error.Message); }
    }

    private static Task<(TenantContext? Context, IResult? Failure)> ResolveAsync(Guid tenantId, HttpContext http, CancellationToken ct) =>
        TenantCustomerEndpoint.ResolveAsync(tenantId, http, http.User,
            http.RequestServices.GetRequiredService<ResolveAccountBinding>(),
            http.RequestServices.GetRequiredService<ResolveTenantContext>(),
            http.RequestServices.GetRequiredService<IAuthorizationService>(), ct);

    private static async Task<(T? Value, IResult? Failure)> ReadPayloadAsync<T>(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength is > MaximumBodyBytes) return (default, Problem(413, "request_too_large"));
        if (!request.HasJsonContentType()) return (default, Problem(400, "request_invalid"));
        try
        {
            var buffer = new byte[MaximumBodyBytes + 1];
            var count = 0;
            while (count < buffer.Length)
            {
                var read = await request.Body.ReadAsync(buffer.AsMemory(count), ct);
                if (read == 0) break;
                count += read;
            }
            if (count > MaximumBodyBytes) return (default, Problem(413, "request_too_large"));
            using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 2 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return (default, Problem(400, "request_invalid"));
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
                if (!names.Add(property.Name)) return (default, Problem(400, "request_invalid"));
            var payload = JsonSerializer.Deserialize<T>(buffer.AsSpan(0, count), JsonOptions);
            return payload is null ? (default, Problem(400, "request_invalid")) : (payload, null);
        }
        catch (BadHttpRequestException error) { return (default, Problem(error.StatusCode == 413 ? 413 : 400, error.StatusCode == 413 ? "request_too_large" : "request_invalid")); }
        catch (JsonException) { return (default, Problem(400, "request_invalid")); }
    }

    private static IResult CreateResponse<T>(HttpContext http, string status, T? value, Guid? id, string path)
    {
        if (status == "Created")
        {
            if (value is null || id is null || id == Guid.Empty) throw new InvalidOperationException("Catalog create returned no identity.");
            return TypedResults.Created($"{path}/{id:D}", value);
        }
        return MutationResponse(http, status, value);
    }

    private static IResult MutationResponse<T>(HttpContext http, string status, T? value)
    {
        if (status is not ("Created" or "Replayed" or "Renamed" or "Retired" or "Published" or "Changed")) return Failure(status);
        if (value is null) throw new InvalidOperationException("Successful catalog mutation returned no facts.");
        if (status == "Replayed") http.Response.Headers.Append("Idempotency-Replayed", "true");
        return TypedResults.Ok(value);
    }

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Failure(string status) => status switch
    {
        "NotFound" or "ItemNotFound" or "UnitNotFound" or "BaseUnitNotFound" or "ConversionNotFound" => Problem(404, "catalog_resource_not_found"),
        "CodeConflict" => Problem(409, "code_conflict"),
        "IdempotencyKeyConflict" => Problem(409, "idempotency_key_conflict"),
        "RevisionConflict" => Problem(409, "revision_conflict"),
        "AlreadyRetired" or "ItemRetired" or "UnitRetired" or "BaseUnitRetired" => Problem(409, "catalog_resource_retired"),
        "ItemUnavailable" => Problem(409, "item_unavailable"),
        "StockModeMismatch" => Problem(409, "stock_mode_mismatch"),
        "AlreadyInState" => Problem(409, "availability_unchanged"),
        "QuantityInvalid" => Problem(400, "quantity_invalid"),
        "UnitMismatch" => Problem(400, "unit_mismatch"),
        "ConversionRevisionRequired" => Problem(400, "conversion_revision_required"),
        _ => throw new InvalidOperationException("Catalog operation returned an unsupported outcome."),
    };

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Problem(int status, string code, string? detail = null) => TypedResults.Problem(
        statusCode: status, title: "Catalog operation could not complete.", detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = code });

    private static CatalogUnitResponse UnitResponse(CatalogUnitSnapshot value, TenantContext context, Guid? id = null)
    {
        ValidateIdentity(value.TenantId, value.UnitId, context, id ?? value.UnitId);
        return new(value.UnitId, value.Code, value.Name, value.Precision, WireStatus(value.Status), value.Revision, value.CreatedAt, value.RetiredAt);
    }

    private static CatalogItemResponse ItemResponse(CatalogItemSnapshot value, TenantContext context, Guid? id = null)
    {
        ValidateIdentity(value.TenantId, value.ItemId, context, id ?? value.ItemId);
        return new(value.ItemId, value.Code, value.Name, value.Description, value.Kind switch
        {
            CatalogItemKind.Product => "product",
            CatalogItemKind.Service => "service",
            _ => throw new InvalidOperationException("Catalog item kind is unsupported."),
        }, WireStatus(value.Status), value.BaseUnitId, value.StockMode switch
        {
            CatalogStockMode.PreciseStock => "preciseStock",
            CatalogStockMode.AvailabilityOnly => "availabilityOnly",
            CatalogStockMode.NonStock => "nonStock",
            _ => throw new InvalidOperationException("Catalog stock mode is unsupported."),
        }, WireAvailability(value.Availability), value.Revision, value.CreatedAt, value.RetiredAt, value.AvailabilityChangedAt);
    }

    private static CatalogConversionResponse ConversionResponse(CatalogConversionSnapshot value, TenantContext context, Guid source, Guid target)
    {
        ValidateIdentity(value.TenantId, value.SourceUnitId, context, source);
        if (value.TargetUnitId != target) throw new InvalidOperationException("Catalog conversion returned an inconsistent target.");
        return new(source, target, value.Revision, value.Numerator, value.Denominator, value.PublishedAt);
    }

    private static void ValidateIdentity(Guid tenantId, Guid id, TenantContext context, Guid expected)
    {
        if (id == Guid.Empty || id != expected || tenantId != context.TenantId)
            throw new InvalidOperationException("Catalog operation returned an inconsistent identity.");
    }

    private static string WireStatus(CatalogEntityStatus status) => status switch
    {
        CatalogEntityStatus.Active => "active",
        CatalogEntityStatus.Retired => "retired",
        _ => throw new InvalidOperationException("Catalog status is unsupported."),
    };

    private static string? WireAvailability(CatalogAvailability? availability) => availability switch
    {
        null => null,
        CatalogAvailability.Available => "available",
        CatalogAvailability.Unavailable => "unavailable",
        _ => throw new InvalidOperationException("Catalog availability is unsupported."),
    };

    private static CatalogItemKind ParseKind(string? kind) => kind switch
    {
        "product" => CatalogItemKind.Product,
        "service" => CatalogItemKind.Service,
        _ => throw new CatalogValidationException("item_kind_invalid", "Kind must be product or service."),
    };

    private static CatalogStockMode ParseStockMode(string? mode) => mode switch
    {
        "preciseStock" => CatalogStockMode.PreciseStock,
        "availabilityOnly" => CatalogStockMode.AvailabilityOnly,
        "nonStock" => CatalogStockMode.NonStock,
        _ => throw new CatalogValidationException("stock_mode_invalid", "Stock mode is unsupported."),
    };

    private static (int Limit, (DateTimeOffset At, Guid Id)? After) ReadPage(IQueryCollection query, Guid tenantId, string resource)
    {
        if (query.Keys.Any(key => key is not ("limit" or "after"))) throw new CatalogValidationException("query_invalid", "Only limit and after are supported.");
        var limit = 25;
        if (query.TryGetValue("limit", out var limits) && (limits.Count != 1 || !int.TryParse(limits[0], NumberStyles.None, CultureInfo.InvariantCulture, out limit)))
            throw new CatalogValidationException("page_size_invalid", "Limit must occur once and be an integer from 1 to 50.");
        CatalogRules.RequirePage(limit);
        if (!query.TryGetValue("after", out var values)) return (limit, null);
        if (values.Count != 1 || !TryDecodeCursor(values[0], tenantId, resource, out var cursor))
            throw new CatalogValidationException("cursor_invalid", "After must be one tenant/resource-bound cursor.");
        return (limit, cursor);
    }

    private static string EncodeCursor(Guid tenantId, string resource, DateTimeOffset at, Guid id)
    {
        CatalogRules.RequireCursor(at, id, "cursor_invalid");
        var text = string.Create(CultureInfo.InvariantCulture, $"v1:{tenantId:N}:{resource}:{at.UtcTicks}:{id:N}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool TryDecodeCursor(string? encoded, Guid tenantId, string resource, out (DateTimeOffset At, Guid Id)? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(encoded) || encoded.Length > 192 || encoded.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))) return false;
        try
        {
            var padded = encoded.Replace('-', '+').Replace('_', '/').PadRight((encoded.Length + 3) / 4 * 4, '=');
            var parts = StrictUtf8.GetString(Convert.FromBase64String(padded)).Split(':');
            if (parts.Length != 5 || parts[0] != "v1" || parts[1] != tenantId.ToString("N") || parts[2] != resource ||
                !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) || ticks <= 0 || ticks > DateTime.MaxValue.Ticks ||
                !Guid.TryParseExact(parts[4], "N", out var id) || id == Guid.Empty) return false;
            cursor = (new DateTimeOffset(ticks, TimeSpan.Zero), id);
            return encoded == EncodeCursor(tenantId, resource, cursor.Value.At, cursor.Value.Id);
        }
        catch (Exception error) when (error is FormatException or DecoderFallbackException) { return false; }
    }
}

internal sealed record CreateCatalogUnitPayload(string? Code, string? Name, [property: JsonRequired] int Precision);
internal sealed record CreateCatalogItemPayload(string? Code, string? Name, string? Description, string? Kind, Guid BaseUnitId, string? StockMode);
internal sealed record CatalogUnitDisplayPayload(long ExpectedRevision, string? Name);
internal sealed record CatalogItemDisplayPayload(long ExpectedRevision, string? Name, string? Description);
internal sealed record CatalogRevisionPayload(long ExpectedRevision);
internal sealed record CatalogAvailabilityPayload(long ExpectedRevision, string? Availability);
internal sealed record CatalogConversionPayload([property: JsonRequired] long ExpectedRevision,
    [property: JsonRequired] decimal Numerator, [property: JsonRequired] decimal Denominator);
internal sealed record CatalogSelectionPayload(Guid ItemId, Guid UnitId, decimal Quantity, long? ConversionRevision);
internal sealed record CatalogUnitResponse(Guid UnitId, string Code, string Name, int Precision, string Status, long Revision, DateTimeOffset CreatedAt, DateTimeOffset? RetiredAt);
internal sealed record CatalogItemResponse(Guid ItemId, string? Code, string Name, string? Description, string Kind, string Status, Guid BaseUnitId,
    string StockMode, string? Availability, long Revision, DateTimeOffset CreatedAt, DateTimeOffset? RetiredAt, DateTimeOffset? AvailabilityChangedAt);
internal sealed record CatalogMutationResponse(Guid Id, long Revision, string Status, DateTimeOffset? ChangedAt);
internal sealed record CatalogConversionResponse(Guid SourceUnitId, Guid TargetUnitId, long Revision, decimal Numerator, decimal Denominator, DateTimeOffset PublishedAt);
internal sealed record CatalogUnitPageResponse(IReadOnlyList<CatalogUnitResponse> Items, string? NextCursor);
internal sealed record CatalogItemPageResponse(IReadOnlyList<CatalogItemResponse> Items, string? NextCursor);
