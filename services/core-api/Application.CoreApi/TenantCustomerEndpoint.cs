using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Application.CoreApi.Authorization;
using Application.Customers;
using Application.IdentityAccess;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi;

internal static class TenantCustomerEndpoint
{
    internal const long MaximumCreateRequestBodyBytes = 4096;
    private const int DefaultPageSize = 25;
    private const int MaximumPageSize = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static async Task<IResult> CreateOrganizationAsync(Guid tenantId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenant,
        IAuthorizationService authorization, CreateCustomerOrganization command,
        CoreApiMutationDiagnostics diagnostics, CancellationToken ct)
    {
        var context = await ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (context.Failure is not null) return context.Failure;
        if (!TryKey(http.Request, out var key)) return Invalid("idempotency_key_invalid", "One Idempotency-Key header is required.");
        var payload = await ReadPayloadAsync<NamePayload>(http.Request, ct);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var result = await command.ExecuteAsync(context.Context!,
                new CreateCustomerOrganizationRequest(payload.Value!.DisplayName ?? string.Empty), key!, ct);
            if (result.Status == CreateCustomerOrganizationStatus.IdempotencyKeyConflict) return Conflict();
            if (result.Status is not (CreateCustomerOrganizationStatus.Created or CreateCustomerOrganizationStatus.Replayed))
                throw new InvalidOperationException("Customer organization create returned an unsupported status.");
            var value = result.Organization ?? throw new InvalidOperationException("Successful customer create returned no organization.");
            diagnostics.RecordSuccess(CoreApiMutation.CustomerOrganizationCreated, context.Context!,
                value.OrganizationId, result.Status == CreateCustomerOrganizationStatus.Replayed, http.TraceIdentifier);
            http.Response.Headers.CacheControl = "no-store";
            if (result.Status == CreateCustomerOrganizationStatus.Replayed)
            {
                http.Response.Headers.Append("Idempotency-Replayed", "true");
                return TypedResults.Ok(value);
            }
            return TypedResults.Created($"/api/v1/tenants/{tenantId:D}/customers/organizations/{value.OrganizationId:D}", value);
        }
        catch (CustomerValidationException error) { return Invalid(error.Code, error.Message); }
    }

    internal static async Task<IResult> GetOrganizationAsync(Guid tenantId, Guid organizationId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenant,
        IAuthorizationService authorization, GetCustomerOrganization query, CancellationToken ct)
    {
        var context = await ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (context.Failure is not null) return context.Failure;
        try
        {
            var value = await query.ExecuteAsync(context.Context!, organizationId, ct);
            if (value is null) return NotFound("organization_not_found", "Organization not found in this tenant.");
            http.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(value);
        }
        catch (CustomerValidationException error) { return Invalid(error.Code, error.Message); }
    }

    internal static async Task<IResult> ListOrganizationsAsync(Guid tenantId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenant,
        IAuthorizationService authorization, ListCustomerOrganizations query, CancellationToken ct)
    {
        var context = await ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (context.Failure is not null) return context.Failure;
        if (!TryPage(http.Request.Query, tenantId, null, out var limit, out var after, out var failure)) return failure!;
        try
        {
            var cursor = after is null ? null : new CustomerOrganizationPageCursor(after.Value.CreatedAt, after.Value.Id);
            var page = await query.ExecuteAsync(context.Context!, new ListCustomerOrganizationsRequest(limit, cursor), ct);
            http.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(new CustomerOrganizationPageResponse(page.Items,
                page.NextCursor is null ? null : EncodeCursor(tenantId, null, page.NextCursor.CreatedAt, page.NextCursor.OrganizationId)));
        }
        catch (CustomerValidationException error) { return Invalid(error.Code, error.Message); }
    }

    internal static async Task<IResult> CreateProgramAsync(Guid tenantId, Guid organizationId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenant,
        IAuthorizationService authorization, CreateCustomerProgram command,
        CoreApiMutationDiagnostics diagnostics, CancellationToken ct)
    {
        var context = await ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (context.Failure is not null) return context.Failure;
        if (!TryKey(http.Request, out var key)) return Invalid("idempotency_key_invalid", "One Idempotency-Key header is required.");
        var payload = await ReadPayloadAsync<NamePayload>(http.Request, ct);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var result = await command.ExecuteAsync(context.Context!,
                new CreateCustomerProgramRequest(organizationId, payload.Value!.DisplayName ?? string.Empty), key!, ct);
            if (result.Status == CreateCustomerProgramStatus.ParentNotFound) return NotFound("organization_not_found", "Organization not found in this tenant.");
            if (result.Status == CreateCustomerProgramStatus.IdempotencyKeyConflict) return Conflict();
            if (result.Status is not (CreateCustomerProgramStatus.Created or CreateCustomerProgramStatus.Replayed))
                throw new InvalidOperationException("Customer program create returned an unsupported status.");
            var value = result.Program ?? throw new InvalidOperationException("Successful customer create returned no program.");
            diagnostics.RecordSuccess(CoreApiMutation.CustomerProgramCreated, context.Context!,
                value.ProgramId, result.Status == CreateCustomerProgramStatus.Replayed, http.TraceIdentifier);
            http.Response.Headers.CacheControl = "no-store";
            if (result.Status == CreateCustomerProgramStatus.Replayed)
            {
                http.Response.Headers.Append("Idempotency-Replayed", "true");
                return TypedResults.Ok(value);
            }
            return TypedResults.Created($"/api/v1/tenants/{tenantId:D}/customers/organizations/{organizationId:D}/programs/{value.ProgramId:D}", value);
        }
        catch (CustomerValidationException error) { return Invalid(error.Code, error.Message); }
    }

    internal static async Task<IResult> GetProgramAsync(Guid tenantId, Guid organizationId, Guid programId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenant,
        IAuthorizationService authorization, GetCustomerProgram query, CancellationToken ct)
    {
        var context = await ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (context.Failure is not null) return context.Failure;
        try
        {
            var value = await query.ExecuteAsync(context.Context!, programId, ct);
            if (value is null || value.OrganizationId != organizationId)
                return NotFound("program_not_found", "Program not found under this organization.");
            http.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(value);
        }
        catch (CustomerValidationException error) { return Invalid(error.Code, error.Message); }
    }

    internal static async Task<IResult> ListProgramsAsync(Guid tenantId, Guid organizationId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenant,
        IAuthorizationService authorization, GetCustomerOrganization getOrganization,
        ListCustomerPrograms query, CancellationToken ct)
    {
        var context = await ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (context.Failure is not null) return context.Failure;
        if (!TryPage(http.Request.Query, tenantId, organizationId, out var limit, out var after, out var failure)) return failure!;
        try
        {
            if (await getOrganization.ExecuteAsync(context.Context!, organizationId, ct) is null)
                return NotFound("organization_not_found", "Organization not found in this tenant.");
            var cursor = after is null ? null : new CustomerProgramPageCursor(after.Value.CreatedAt, after.Value.Id);
            var page = await query.ExecuteAsync(context.Context!, new ListCustomerProgramsRequest(organizationId, limit, cursor), ct);
            http.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(new CustomerProgramPageResponse(page.Items,
                page.NextCursor is null ? null : EncodeCursor(tenantId, organizationId, page.NextCursor.CreatedAt, page.NextCursor.ProgramId)));
        }
        catch (CustomerValidationException error) { return Invalid(error.Code, error.Message); }
    }

    internal static async Task<(TenantContext? Context, IResult? Failure)> ResolveAsync(
        Guid tenantId, HttpContext http, ClaimsPrincipal principal, ResolveAccountBinding account,
        ResolveTenantContext tenant, IAuthorizationService authorization, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var access = await TenantRequestAccess.ResolveAsync(tenantId, http, principal, account, tenant, ct);
        if (access.Failure is not null) return (null, access.Failure);
        var denied = await CoreApiDeclaredAuthorization.AuthorizeAsync(
            http, principal, new TenantCustomerResource(access.TenantContext!, ct), authorization,
            "The account is not permitted to perform this customer operation in this tenant.").ConfigureAwait(false);
        return denied is null
            ? (access.TenantContext, null)
            : (null, denied);
    }

    internal static bool TryKey(HttpRequest request, out string? key)
    {
        var values = request.Headers["Idempotency-Key"];
        key = values.Count == 1 ? values[0] : null;
        return key is not null;
    }

    internal static async Task<(T? Value, IResult? Failure)> ReadPayloadAsync<T>(HttpRequest request, CancellationToken ct, bool strict = false)
    {
        if (request.ContentLength is > MaximumCreateRequestBodyBytes)
            return (default, TooLarge());
        if (!request.HasJsonContentType()) return (default, Invalid("request_invalid", "The request body must use application/json."));
        try
        {
            var buffer = new byte[MaximumCreateRequestBodyBytes + 1];
            var count = 0;
            while (count < buffer.Length)
            {
                var read = await request.Body.ReadAsync(buffer.AsMemory(count), ct);
                if (read == 0) break;
                count += read;
            }
            if (count > MaximumCreateRequestBodyBytes) return (default, TooLarge());
            if (strict)
            {
                using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 2 });
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return (default, Invalid("request_invalid", "The customer request must be a JSON object."));
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in document.RootElement.EnumerateObject())
                    if (!names.Add(property.Name))
                        return (default, Invalid("request_invalid", "The customer request must not repeat properties."));
            }
            var value = JsonSerializer.Deserialize<T>(buffer.AsSpan(0, count), JsonOptions);
            return value is null
                ? (default, Invalid("request_invalid", "The request body must be a valid JSON customer request."))
                : (value, null);
        }
        catch (BadHttpRequestException error) when (error.StatusCode == 413) { return (default, TooLarge()); }
        catch (BadHttpRequestException) { return (default, Invalid("request_invalid", "The request body must be a valid JSON customer request.")); }
        catch (JsonException) { return (default, Invalid("request_invalid", "The request body must be a valid JSON customer request.")); }
    }

    private static bool TryPage(IQueryCollection query, Guid tenantId, Guid? organizationId,
        out int limit, out (DateTimeOffset CreatedAt, Guid Id)? after, out IResult? failure)
    {
        limit = DefaultPageSize;
        after = null;
        failure = null;
        if (query.TryGetValue("limit", out var limits) &&
            (limits.Count != 1 || !int.TryParse(limits[0], NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > MaximumPageSize))
        {
            failure = Invalid("page_size_invalid", "Limit must occur once and be an integer from 1 to 50.");
            return false;
        }
        if (query.TryGetValue("after", out var cursors) &&
            (cursors.Count != 1 || !TryDecodeCursor(cursors[0], tenantId, organizationId, out after)))
        {
            failure = Invalid("cursor_invalid", "After must be one supported opaque cursor.");
            return false;
        }
        return true;
    }

    private static string EncodeCursor(Guid tenantId, Guid? parentId, DateTimeOffset createdAt, Guid id)
    {
        var payload = string.Create(CultureInfo.InvariantCulture,
            $"v1:{tenantId:N}:{(parentId.HasValue ? parentId.Value.ToString("N") : "organizations")}:{createdAt.ToUniversalTime().Ticks}:{id:N}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool TryDecodeCursor(string? encoded, Guid tenantId, Guid? parentId,
        out (DateTimeOffset CreatedAt, Guid Id)? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(encoded) || encoded.Length > 192 || encoded.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) return false;
        try
        {
            var padded = encoded.Replace('-', '+').Replace('_', '/').PadRight((encoded.Length + 3) / 4 * 4, '=');
            var values = StrictUtf8.GetString(Convert.FromBase64String(padded)).Split(':');
            if (values.Length != 5 || values[0] != "v1" || values[1] != tenantId.ToString("N") ||
                values[2] != (parentId.HasValue ? parentId.Value.ToString("N") : "organizations") ||
                !long.TryParse(values[3], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
                ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks ||
                !Guid.TryParseExact(values[4], "N", out var id) || id == Guid.Empty) return false;
            cursor = (new DateTimeOffset(ticks, TimeSpan.Zero), id);
            return string.Equals(EncodeCursor(tenantId, parentId, cursor.Value.CreatedAt, id), encoded, StringComparison.Ordinal);
        }
        catch (Exception error) when (error is FormatException or DecoderFallbackException) { return false; }
    }

    internal static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Invalid(string code, string detail) => TypedResults.Problem(statusCode: 400,
        title: "Invalid customer request.", detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = code });
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult TooLarge() => TypedResults.Problem(statusCode: 413,
        title: "Customer request is too large.", detail: "The request exceeds the supported size.",
        extensions: new Dictionary<string, object?> { ["code"] = "request_too_large" });
    // Each capability group reports its own Problem title. Sharing the Customers wording meant an
    // order 404 said "Customer resource not found." and a pricing conflict said the key was reused
    // for a different customer request. The machine-readable `code` values are unchanged.
    internal static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult NotFound(string title, string code, string detail) =>
        TypedResults.Problem(statusCode: 404, title: title, detail: detail, extensions: Extensions(code));

    internal static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Conflict(string title, string detail) =>
        TypedResults.Problem(statusCode: 409, title: title, detail: detail, extensions: Extensions("idempotency_key_conflict"));

    internal static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Invalid(string title, string code, string detail) =>
        TypedResults.Problem(statusCode: 400, title: title, detail: detail, extensions: Extensions(code));

    internal static Dictionary<string, object?> Extensions(string code) => new() { ["code"] = code };

    internal static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Conflict() => TypedResults.Problem(statusCode: 409,
        title: "Idempotency key conflict.", detail: "The Idempotency-Key was used for a different customer request.",
        extensions: new Dictionary<string, object?> { ["code"] = "idempotency_key_conflict" });
    internal static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult NotFound(string code, string detail) => TypedResults.Problem(statusCode: 404,
        title: "Customer resource not found.", detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = code });

    internal sealed record NamePayload(string? DisplayName);
}

internal sealed record CustomerOrganizationPageResponse(IReadOnlyList<CustomerOrganizationSnapshot> Items, string? NextCursor);
internal sealed record CustomerProgramPageResponse(IReadOnlyList<CustomerProgramSnapshot> Items, string? NextCursor);
