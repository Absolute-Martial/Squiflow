using System.Security.Claims;
using System.Text.Json.Serialization;
using Application.CoreApi.Authorization;
using Application.Customers;
using Application.IdentityAccess;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi;

internal static class TenantCustomerRepresentativeEndpoint
{
    internal static async Task<IResult> LinkAsync(Guid tenantId, Guid organizationId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenant,
        IAuthorizationService authorization, LinkCustomerRepresentative command,
        CoreApiMutationDiagnostics diagnostics, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TenantCustomerEndpoint.TryKey(http.Request, out var key))
            return TenantCustomerEndpoint.Invalid("idempotency_key_invalid", "One Idempotency-Key header is required.");
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<LinkRepresentativePayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var result = await command.ExecuteAsync(access.Context!,
                new LinkCustomerRepresentativeRequest(organizationId, payload.Value!.ProgramId, payload.Value.IndividualId), key!, ct);
            if (result.Status is LinkCustomerRepresentativeStatus.TargetNotFound or LinkCustomerRepresentativeStatus.IndividualNotFound)
                return TenantCustomerEndpoint.NotFound("representative_target_not_found", "Representative target not found in this tenant.");
            if (result.Status == LinkCustomerRepresentativeStatus.IndividualInactive)
                return Conflict("representative_individual_inactive", "An inactive individual cannot be linked as an active representative.");
            if (result.Status == LinkCustomerRepresentativeStatus.AlreadyLinked)
                return Conflict("representative_already_linked", "This individual already has an active representative relationship for the selected target.");
            if (result.Status == LinkCustomerRepresentativeStatus.IdempotencyKeyConflict) return TenantCustomerEndpoint.Conflict();
            if (result.Status is not (LinkCustomerRepresentativeStatus.Created or LinkCustomerRepresentativeStatus.Replayed))
                throw new InvalidOperationException("Representative link returned an unsupported status.");
            var value = result.Representative ?? throw new InvalidOperationException("Representative link returned no relationship.");
            ValidateIdentity(value, access.Context!, organizationId, value.RepresentativeId);
            diagnostics.RecordSuccess(CoreApiMutation.CustomerRepresentativeLinked, access.Context!, value.RepresentativeId,
                result.Status == LinkCustomerRepresentativeStatus.Replayed, http.TraceIdentifier);
            var response = ToResponse(value);
            if (result.Status == LinkCustomerRepresentativeStatus.Replayed)
            {
                http.Response.Headers.Append("Idempotency-Replayed", "true");
                return TypedResults.Ok(response);
            }
            return TypedResults.Created($"/api/v1/tenants/{tenantId:D}/customers/organizations/{organizationId:D}/representatives/{value.RepresentativeId:D}", response);
        }
        catch (CustomerValidationException error) { return TenantCustomerEndpoint.Invalid(error.Code, error.Message); }
    }

    internal static async Task<IResult> GetAsync(Guid tenantId, Guid organizationId, Guid representativeId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenant,
        IAuthorizationService authorization, GetCustomerRepresentative query, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        try
        {
            var value = await query.ExecuteAsync(access.Context!, representativeId, ct);
            if (value is null || value.OrganizationId != organizationId)
                return TenantCustomerEndpoint.NotFound("representative_not_found", "Representative relationship not found under this organization.");
            ValidateIdentity(value, access.Context!, organizationId, representativeId);
            return TypedResults.Ok(ToResponse(value));
        }
        catch (CustomerValidationException error) { return TenantCustomerEndpoint.Invalid(error.Code, error.Message); }
    }

    internal static async Task<IResult> UnlinkAsync(Guid tenantId, Guid organizationId, Guid representativeId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenant,
        IAuthorizationService authorization, UnlinkCustomerRepresentative command, CoreApiMutationDiagnostics diagnostics,
        CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant, authorization, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TenantCustomerEndpoint.TryKey(http.Request, out var key))
            return TenantCustomerEndpoint.Invalid("idempotency_key_invalid", "One Idempotency-Key header is required.");
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<UnlinkRepresentativePayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var result = await command.ExecuteAsync(access.Context!,
                new UnlinkCustomerRepresentativeRequest(organizationId, representativeId, payload.Value!.ExpectedRevision), key!, ct);
            if (result.Status == UnlinkCustomerRepresentativeStatus.NotFound)
                return TenantCustomerEndpoint.NotFound("representative_not_found", "Representative relationship not found under this organization.");
            if (result.Status == UnlinkCustomerRepresentativeStatus.IdempotencyKeyConflict) return TenantCustomerEndpoint.Conflict();
            if (result.Status is UnlinkCustomerRepresentativeStatus.RevisionConflict or UnlinkCustomerRepresentativeStatus.AlreadyInactive)
                return Conflict(result.Status == UnlinkCustomerRepresentativeStatus.RevisionConflict ? "revision_conflict" : "representative_already_inactive",
                    "Representative unlink conflict.");
            if (result.Status is not (UnlinkCustomerRepresentativeStatus.Changed or UnlinkCustomerRepresentativeStatus.Replayed))
                throw new InvalidOperationException("Representative unlink returned an unsupported status.");
            var value = result.Representative ?? throw new InvalidOperationException("Representative unlink returned no relationship.");
            ValidateIdentity(value, access.Context!, organizationId, representativeId);
            diagnostics.RecordSuccess(CoreApiMutation.CustomerRepresentativeUnlinked, access.Context!, representativeId,
                result.Status == UnlinkCustomerRepresentativeStatus.Replayed, http.TraceIdentifier);
            if (result.Status == UnlinkCustomerRepresentativeStatus.Replayed) http.Response.Headers.Append("Idempotency-Replayed", "true");
            return TypedResults.Ok(ToResponse(value));
        }
        catch (CustomerValidationException error) { return TenantCustomerEndpoint.Invalid(error.Code, error.Message); }
    }

    private static void ValidateIdentity(CustomerRepresentativeSnapshot value, TenantContext context, Guid organizationId, Guid representativeId)
    {
        if (representativeId == Guid.Empty || value.RepresentativeId != representativeId || value.TenantId != context.TenantId || value.OrganizationId != organizationId)
            throw new InvalidOperationException("The representative operation returned an inconsistent identity.");
    }

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Conflict(string code, string title) => TypedResults.Problem(
        statusCode: 409, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });

    private static CustomerRepresentativeResponse ToResponse(CustomerRepresentativeSnapshot value) => new(
        value.RepresentativeId, value.OrganizationId, value.ProgramId, value.IndividualId,
        value.Availability == CustomerRepresentativeAvailability.Active ? "active" : "inactive",
        value.Revision, value.CreatedAt, value.ChangedAt);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LinkRepresentativePayload(Guid IndividualId, Guid? ProgramId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record UnlinkRepresentativePayload(long ExpectedRevision);
internal sealed record CustomerRepresentativeResponse(Guid RepresentativeId, Guid OrganizationId, Guid? ProgramId,
    Guid IndividualId, string Availability, long Revision, DateTimeOffset CreatedAt, DateTimeOffset? ChangedAt);
