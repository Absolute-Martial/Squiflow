using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Customers;
using Application.CoreApi.Authorization;
using Application.CoreApi.ImportExecution;
using Application.IdentityAccess;
using Application.ObjectStorage;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi;

internal static class TenantCustomerDuplicateImportEndpoint
{
    private static readonly JsonSerializerOptions DecisionJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid WorkerInstanceId = Guid.CreateVersion7();
    internal static async Task<IResult> SearchAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        FindCustomerDuplicates query, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<DuplicateSearchPayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var input = payload.Value!;
            var result = await query.ExecuteAsync(access.Context!, new(CustomerDuplicateSignals.Create(input.Name,
                input.Email, input.Phone, input.ExternalId, input.OrganizationId, input.ProgramId), input.ExcludeCustomerId,
                input.Limit, input.IncludeFuzzyNameDiscovery), ct);
            return TypedResults.Ok(result);
        }
        catch (CustomerValidationException error) { return Invalid(error); }
    }

    internal static async Task<IResult> ResolutionsAsync(Guid tenantId, Guid customerId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        ICustomerDuplicateDiscoveryStore query, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TryPage(http, out var afterRow, out var limit) || afterRow != 0)
            return TenantCustomerEndpoint.Invalid("page_invalid", "Use a limit of 1 to 50 and afterResolutionId.");
        Guid? after = null;
        if (http.Request.Query.TryGetValue("afterResolutionId", out var value))
        {
            if (value.Count != 1 || !Guid.TryParse(value[0], out var id) || id == Guid.Empty)
                return TenantCustomerEndpoint.Invalid("cursor_invalid", "Resolution cursor is invalid.");
            after = id;
        }
        return TypedResults.Ok(await query.ReadResolutionsAsync(access.Context!, customerId, after, limit, ct));
    }

    internal static async Task<IResult> ResolveAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        ResolveCustomerDuplicate command, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TenantCustomerEndpoint.TryKey(http.Request, out var key)) return MissingKey();
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<DuplicateResolvePayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var input = payload.Value!;
            if (!Enum.TryParse<CustomerDuplicateOutcome>(input.Outcome, false, out var outcome))
                return TenantCustomerEndpoint.Invalid("duplicate_outcome_invalid", "Duplicate outcome is invalid.");
            var result = await command.ExecuteAsync(access.Context!, new(input.CustomerId, input.OtherCustomerId,
                input.ExpectedCustomerRevision, input.ExpectedOtherRevision, outcome, input.Reason), key!, ct);
            return result.Status switch
            {
                ResolveCustomerDuplicateStatus.Resolved => TypedResults.Ok(result),
                ResolveCustomerDuplicateStatus.Replayed => Replayed(http, result),
                ResolveCustomerDuplicateStatus.NotFound => Missing("customer_not_found"),
                _ => Conflict(result.Status.ToString()),
            };
        }
        catch (CustomerValidationException error) { return Invalid(error); }
    }

    internal static async Task<IResult> ConsolidateAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        ConsolidateCustomerDuplicate command, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TenantCustomerEndpoint.TryKey(http.Request, out var key)) return MissingKey();
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<DuplicateConsolidatePayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var input = payload.Value!;
            var result = await command.ExecuteAsync(access.Context!, new(input.SourceCustomerId, input.CanonicalCustomerId,
                input.ExpectedSourceRevision, input.ExpectedCanonicalRevision, input.Reason), key!, ct);
            return result.Status switch
            {
                ConsolidateCustomerDuplicateStatus.Consolidated => TypedResults.Ok(result),
                ConsolidateCustomerDuplicateStatus.Replayed => Replayed(http, result),
                ConsolidateCustomerDuplicateStatus.NotFound => Missing("customer_not_found"),
                _ => Conflict(result.Status.ToString()),
            };
        }
        catch (CustomerValidationException error) { return Invalid(error); }
    }

    internal static async Task<IResult> PlanAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        CreateCustomerImport command, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TenantCustomerEndpoint.TryKey(http.Request, out var key)) return MissingKey();
        if (!string.Equals(http.Request.ContentType?.Split(';')[0], "text/csv", StringComparison.OrdinalIgnoreCase))
            return TenantCustomerEndpoint.Invalid("import_content_type_invalid", "Import must use text/csv and UTF-8.");
        if (http.Request.ContentLength > CustomerImportCsv.MaxBytes) return TooLarge();
        if (!TryRetention(http.Request, out var retention, out var retentionFailure)) return retentionFailure!;
        try
        {
            var result = await command.ExecuteAsync(access.Context!, http.Request.Body, null, key!, ct, retention);
            if (result.IdempotencyKeyConflict) return TenantCustomerEndpoint.Conflict();
            // Only bounded metadata is returned; preview/results rows use the paged read API.
            var response = new CustomerImportPlanResponse(result.ImportId, result.Plan.ContractVersion,
                result.Plan.ManifestHash, result.Plan.Rows.Count, result.Plan.PendingRowCount, result.Plan.RejectedRowCount,
                result.Plan.Rows.Count(row => row.RequiresDecision));
            return result.Replayed ? Replayed(http, response)
                : TypedResults.Created($"/api/v1/tenants/{tenantId:D}/customers/imports/{result.ImportId:D}", response);
        }
        catch (CustomerValidationException error) when (error.Code == "import_too_large") { return TooLarge(); }
        catch (BadHttpRequestException error) when (error.StatusCode == 413) { return TooLarge(); }
        catch (CustomerValidationException error) when (error.Code == "import_source_unavailable")
        { return TypedResults.Problem(statusCode: 503, title: "The import source could not be retained safely.", extensions: new Dictionary<string, object?> { ["code"] = error.Code }); }
        catch (CustomerValidationException error) { return Invalid(error); }
    }

    internal static async Task<IResult> AcceptAsync(Guid tenantId, Guid importId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        ExecuteCustomerImport command, CustomerImportExecutionState execution, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        if (!execution.IsAcceptingWork) return ExecutorUnavailable();
        if (!TenantCustomerEndpoint.TryKey(http.Request, out var key)) return MissingKey();
        var payload = await ReadDecisionsAsync(http.Request, ct);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var decisions = new List<CustomerImportDecision>();
            foreach (var value in payload.Value!.Decisions)
            {
                if (!Enum.TryParse<CustomerImportDecisionKind>(value.Kind, false, out var kind))
                    return TenantCustomerEndpoint.Invalid("decision_invalid", "Decision kind is invalid.");
                decisions.Add(new(value.RowNumber, value.SourceRowHash, kind, value.CustomerId));
            }
            var result = await command.ExecuteAsync(access.Context!, new(importId, null, decisions), key!, ct);
            return TypedResults.Accepted($"/api/v1/tenants/{tenantId:D}/customers/imports/{importId:D}", result.Work);
        }
        catch (CustomerImportAuthorityException) { return TypedResults.Problem(statusCode: 403, title: "Current import authority denied."); }
        catch (AuthorizationProviderUnavailableException) { return AuthorizationUnavailable(); }
        catch (CustomerValidationException error) when (error.Code == "idempotency_key_conflict") { return Conflict(error.Code); }
        catch (CustomerValidationException error) when (error.Code == "import_not_found") { return Missing(error.Code); }
        catch (CustomerValidationException error) { return Invalid(error); }
    }

    internal static async Task<IResult> ReadImportAsync(Guid tenantId, Guid importId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        ICustomerImportStore query, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        var summary = await query.ReadImportSummaryAsync(access.Context!, importId, ct);
        return summary is null ? Missing("import_not_found") : TypedResults.Ok(summary);
    }

    internal static async Task<IResult> RunBatchAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        RunCustomerImportBatch runner, CustomerImportExecutionState execution, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        // Manual acceleration is only meaningful while the executor admits work. A disabled,
        // starting, failed-discovery or draining executor must not become a second entry point.
        if (!execution.IsAcceptingWork) return ExecutorUnavailable();
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<ImportBatchPayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var result = await runner.ExecuteAsync(access.Context!.TenantId, WorkerInstanceId, payload.Value!.Limit, ct);
            return result.Status == CustomerImportBatchStatus.AuthorityDenied
                ? TypedResults.Problem(statusCode: 403, title: "The accepted import actor's current authority changed.")
                : TypedResults.Ok(result);
        }
        catch (AuthorizationProviderUnavailableException) { return AuthorizationUnavailable(); }
        catch (CustomerValidationException error) { return Invalid(error); }
    }

    internal static async Task<IResult> RowsAsync(Guid tenantId, Guid importId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        ICustomerImportStore query, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TryPage(http, out var after, out var limit)) return TenantCustomerEndpoint.Invalid("page_invalid", "Limit must be 1 to 50; afterRowNumber must be nonnegative.");
        if (await query.ReadImportSummaryAsync(access.Context!, importId, ct) is null) return Missing("import_not_found");
        return TypedResults.Ok(await query.ReadImportRowsPageAsync(access.Context!, importId, after, limit, ct));
    }

    internal static async Task<IResult> SourceAsync(Guid tenantId, Guid importId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization,
        ReadCustomerImportSource source, IObjectStore objectStore, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        var sourceRead = await source.ExecuteAsync(access.Context!, importId, ct);
        if (sourceRead.Status is CustomerImportSourceReadStatus.NotFound)
            return Missing("import_source_not_found");
        if (sourceRead.Status is not CustomerImportSourceReadStatus.Readable || sourceRead.Source is null)
            return TypedResults.Problem(statusCode: 503, title: "The import source is not currently available.",
                extensions: new Dictionary<string, object?> { ["code"] = "import_source_unavailable" });
        var metadata = sourceRead.Source!;
        var read = await objectStore.OpenReadAsync(new(ObjectStoreKey.Create(metadata.ObjectKey), metadata.ByteLength, metadata.Sha256), ct);
        if (read.Outcome != ObjectStoreReadOutcome.Opened || read.Content is null)
        {
            await read.DisposeAsync();
            return TypedResults.Problem(statusCode: 503, title: "The import source is not currently available.",
                extensions: new Dictionary<string, object?> { ["code"] = "import_source_unavailable" });
        }
        return TypedResults.Stream(read.Content, metadata.ContentType, "customer-import.csv", enableRangeProcessing: false);
    }

    internal static async Task<IResult> TemplateAsync(Guid tenantId, HttpContext http, ClaimsPrincipal principal,
        ResolveAccountBinding account, ResolveTenantContext tenant, IAuthorizationService authorization, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        return access.Failure ?? TypedResults.File(Encoding.UTF8.GetBytes(CustomerImportCsv.Template), "text/csv; charset=utf-8", "customer-import-v1-template.csv");
    }

    private static bool TryPage(HttpContext http, out int after, out int limit)
    {
        after = 0; limit = 25;
        return (!http.Request.Query.TryGetValue("limit", out var values) || values.Count == 1
                && int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit is >= 1 and <= 50)
            && (!http.Request.Query.TryGetValue("afterRowNumber", out var cursor) || cursor.Count == 1
                && int.TryParse(cursor[0], NumberStyles.None, CultureInfo.InvariantCulture, out after) && after >= 0);
    }

    private static bool TryRetention(HttpRequest request, out CustomerImportRetention retention, out IResult? failure)
    {
        retention = CustomerImportRetention.DefaultSevenDays;
        failure = null;
        if (!request.Headers.TryGetValue("X-Tenant-Import-Retention", out var values)) return true;
        if (values.Count != 1 || values[0] is not ("default" or "archive"))
        {
            failure = TenantCustomerEndpoint.Invalid("import_retention_invalid", "Import retention must be default or archive.");
            return false;
        }
        retention = values[0] == "archive" ? CustomerImportRetention.TenantArchived : CustomerImportRetention.DefaultSevenDays;
        return true;
    }

    internal const int MaximumDecisionBytes = 2 * 1024 * 1024;
    private static async Task<(ImportAcceptPayload? Value, IResult? Failure)> ReadDecisionsAsync(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasJsonContentType()) return (null, TenantCustomerEndpoint.Invalid("request_invalid", "Use application/json."));
        if (request.ContentLength > MaximumDecisionBytes) return (null, TooLarge());
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        try
        {
            int read;
            while ((read = await request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > MaximumDecisionBytes) return (null, TooLarge());
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }
            using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object || HasDuplicateProperties(document.RootElement))
                return (null, TenantCustomerEndpoint.Invalid("request_invalid", "JSON properties must be unique."));
            var value = document.RootElement.Deserialize<ImportAcceptPayload>(DecisionJsonOptions);
            return value?.Decisions is null || value.Decisions.Count > CustomerImportCsv.MaxRows || value.Decisions.Any(item => item is null)
                ? (null, TenantCustomerEndpoint.Invalid("decision_invalid", "Decisions must be a bounded array.")) : (value, null);
        }
        catch (JsonException) { return (null, TenantCustomerEndpoint.Invalid("request_invalid", "Invalid import decisions.")); }
        catch (BadHttpRequestException error) when (error.StatusCode == 413) { return (null, TooLarge()); }
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array) return element.EnumerateArray().Any(HasDuplicateProperties);
        if (element.ValueKind != JsonValueKind.Object) return false;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return element.EnumerateObject().Any(property => !names.Add(property.Name) || HasDuplicateProperties(property.Value));
    }

    private static Microsoft.AspNetCore.Http.HttpResults.Ok<T> Replayed<T>(HttpContext http, T value)
    { http.Response.Headers.Append("Idempotency-Replayed", "true"); return TypedResults.Ok(value); }
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Invalid(CustomerValidationException error) => TenantCustomerEndpoint.Invalid(error.Code, error.Message);
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult MissingKey() => TenantCustomerEndpoint.Invalid("idempotency_key_invalid", "One Idempotency-Key is required.");
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Missing(string code) => TenantCustomerEndpoint.NotFound(code, "Customer resource not found in this tenant.");
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Conflict(string code) => TypedResults.Problem(statusCode: 409, title: "Customer operation conflict.", extensions: new Dictionary<string, object?> { ["code"] = code });
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult TooLarge() => TypedResults.Problem(statusCode: 413, title: "Customer import request exceeds the supported size.");
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult ExecutorUnavailable() => TypedResults.Problem(statusCode: 503, title: "Customer import execution is unavailable.",
        extensions: new Dictionary<string, object?> { ["code"] = "import_executor_unavailable" });
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult AuthorizationUnavailable() => TypedResults.Problem(statusCode: 503, title: "Authorization is temporarily unavailable.",
        extensions: new Dictionary<string, object?> { ["code"] = "authorization_unavailable" });
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DuplicateSearchPayload(string Name, string? Email = null, string? Phone = null, string? ExternalId = null,
    Guid? OrganizationId = null, Guid? ProgramId = null, Guid? ExcludeCustomerId = null, int Limit = 25, bool IncludeFuzzyNameDiscovery = false);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DuplicateResolvePayload(Guid CustomerId, Guid OtherCustomerId, long ExpectedCustomerRevision,
    long ExpectedOtherRevision, string Outcome, string? Reason = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DuplicateConsolidatePayload(Guid SourceCustomerId, Guid CanonicalCustomerId,
    long ExpectedSourceRevision, long ExpectedCanonicalRevision, string? Reason = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ImportAcceptPayload(IReadOnlyList<ImportDecisionPayload> Decisions);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ImportDecisionPayload(int RowNumber, string SourceRowHash, string Kind, Guid? CustomerId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ImportBatchPayload(int Limit = 25);
internal sealed record CustomerImportPlanResponse(Guid ImportId, string ContractVersion, string ManifestHash,
    int RowCount, int PendingRows, int RejectedRows, int RowsRequiringDecision);
