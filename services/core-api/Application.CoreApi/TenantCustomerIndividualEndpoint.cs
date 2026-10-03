using System.Security.Claims;
using Application.CoreApi.Authorization;
using Application.Customers;
using Application.IdentityAccess;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json.Serialization;

namespace Application.CoreApi;

internal static class TenantCustomerIndividualEndpoint
{
    internal static async Task<IResult> CreateAsync(Guid tenantId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenant,
        IAuthorizationService authorization, CreateCustomerIndividual command,
        CoreApiMutationDiagnostics diagnostics, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant,
            authorization, CreateIndividualRequirement.Instance, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TenantCustomerEndpoint.TryKey(http.Request, out var key)) return TenantCustomerEndpoint.Invalid("idempotency_key_invalid", "One Idempotency-Key header is required.");
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<CreateIndividualPayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        try
        {
            var result = await command.ExecuteAsync(access.Context!,
                new CreateCustomerIndividualRequest(payload.Value!.DisplayName ?? string.Empty, payload.Value.Email, payload.Value.Phone), key!, ct);
            if (result.Status == CreateCustomerIndividualStatus.IdempotencyKeyConflict) return TenantCustomerEndpoint.Conflict();
            if (result.Status is not (CreateCustomerIndividualStatus.Created or CreateCustomerIndividualStatus.Replayed))
                throw new InvalidOperationException("Individual create returned an unsupported status.");
            var value = result.Individual ?? throw new InvalidOperationException("Individual create returned no record.");
            ValidateIdentity(value, access.Context!, value.IndividualId);
            diagnostics.RecordSuccess(CoreApiMutation.CustomerIndividualCreated, access.Context!, value.IndividualId,
                result.Status == CreateCustomerIndividualStatus.Replayed, http.TraceIdentifier);
            var response = ToResponse(value);
            if (result.Status == CreateCustomerIndividualStatus.Replayed)
            {
                http.Response.Headers.Append("Idempotency-Replayed", "true");
                return TypedResults.Ok(response);
            }
            return TypedResults.Created($"/api/v1/tenants/{tenantId:D}/customers/individuals/{value.IndividualId:D}", response);
        }
        catch (CustomerValidationException error) { return TenantCustomerEndpoint.Invalid(error.Code, error.Message); }
    }

    internal static async Task<IResult> GetAsync(Guid tenantId, Guid individualId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenant,
        IAuthorizationService authorization, GetCustomerIndividual query, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant,
            authorization, ViewIndividualsRequirement.Instance, ct);
        if (access.Failure is not null) return access.Failure;
        try
        {
            var value = await query.ExecuteAsync(access.Context!, individualId, ct);
            if (value is null) return TenantCustomerEndpoint.NotFound("individual_not_found", "Individual not found in this tenant.");
            ValidateIdentity(value, access.Context!, individualId);
            return TypedResults.Ok(ToResponse(value));
        }
        catch (CustomerValidationException error) { return TenantCustomerEndpoint.Invalid(error.Code, error.Message); }
    }

    internal static async Task<IResult> ChangeAvailabilityAsync(Guid tenantId, Guid individualId, HttpContext http,
        ClaimsPrincipal principal, ResolveAccountBinding account, ResolveTenantContext tenant,
        IAuthorizationService authorization, ChangeCustomerIndividualAvailability command,
        CoreApiMutationDiagnostics diagnostics, CancellationToken ct)
    {
        var access = await TenantCustomerEndpoint.ResolveAsync(tenantId, http, principal, account, tenant,
            authorization, ChangeIndividualAvailabilityRequirement.Instance, ct);
        if (access.Failure is not null) return access.Failure;
        if (!TenantCustomerEndpoint.TryKey(http.Request, out var key)) return TenantCustomerEndpoint.Invalid("idempotency_key_invalid", "One Idempotency-Key header is required.");
        var payload = await TenantCustomerEndpoint.ReadPayloadAsync<IndividualAvailabilityPayload>(http.Request, ct, strict: true);
        if (payload.Failure is not null) return payload.Failure;
        var requested = payload.Value!.Availability switch
        {
            "active" => CustomerIndividualAvailability.Active,
            "inactive" => CustomerIndividualAvailability.Inactive,
            _ => (CustomerIndividualAvailability)0,
        };
        try
        {
            var result = await command.ExecuteAsync(access.Context!,
                new ChangeCustomerIndividualAvailabilityRequest(individualId, payload.Value.ExpectedRevision, requested), key!, ct);
            if (result.Status == ChangeCustomerIndividualAvailabilityStatus.NotFound)
                return TenantCustomerEndpoint.NotFound("individual_not_found", "Individual not found in this tenant.");
            if (result.Status == ChangeCustomerIndividualAvailabilityStatus.IdempotencyKeyConflict) return TenantCustomerEndpoint.Conflict();
            if (result.Status is ChangeCustomerIndividualAvailabilityStatus.RevisionConflict or ChangeCustomerIndividualAvailabilityStatus.AlreadyInState)
                return TypedResults.Problem(statusCode: 409, title: "Individual availability conflict.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = result.Status == ChangeCustomerIndividualAvailabilityStatus.RevisionConflict
                        ? "revision_conflict" : "individual_already_in_state"
                    });
            if (result.Status is not (ChangeCustomerIndividualAvailabilityStatus.Changed or ChangeCustomerIndividualAvailabilityStatus.Replayed))
                throw new InvalidOperationException("Individual availability returned an unsupported status.");
            var value = result.Individual ?? throw new InvalidOperationException("Individual availability returned no record.");
            ValidateIdentity(value, access.Context!, individualId);
            diagnostics.RecordSuccess(CoreApiMutation.CustomerIndividualAvailabilityChanged, access.Context!, individualId,
                result.Status == ChangeCustomerIndividualAvailabilityStatus.Replayed, http.TraceIdentifier);
            if (result.Status == ChangeCustomerIndividualAvailabilityStatus.Replayed)
                http.Response.Headers.Append("Idempotency-Replayed", "true");
            // An availability editor need not hold the permission to disclose contact information.
            return TypedResults.Ok(new IndividualAvailabilityResponse(individualId, WireAvailability(value.Availability),
                value.Revision, value.AvailabilityChangedAt ?? throw new InvalidOperationException("The availability receipt has no change time.")));
        }
        catch (CustomerValidationException error) { return TenantCustomerEndpoint.Invalid(error.Code, error.Message); }
    }

    private static void ValidateIdentity(CustomerIndividualSnapshot value, TenantContext context, Guid individualId)
    {
        if (individualId == Guid.Empty || value.IndividualId != individualId || value.TenantId != context.TenantId)
            throw new InvalidOperationException("The individual operation returned an inconsistent identity.");
    }

    private static string WireAvailability(CustomerIndividualAvailability availability) => availability switch
    {
        CustomerIndividualAvailability.Active => "active",
        CustomerIndividualAvailability.Inactive => "inactive",
        _ => throw new InvalidOperationException("The individual operation returned an unsupported availability."),
    };

    private static CustomerIndividualResponse ToResponse(CustomerIndividualSnapshot value) =>
        new(value.IndividualId, value.DisplayName, value.Email, value.Phone, WireAvailability(value.Availability),
            value.Revision, value.CreatedAt, value.AvailabilityChangedAt);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record CreateIndividualPayload(string? DisplayName, string? Email = null, string? Phone = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record IndividualAvailabilityPayload(long ExpectedRevision, string? Availability);
internal sealed record CustomerIndividualResponse(Guid IndividualId, string DisplayName, string? Email, string? Phone,
    string Availability, long Revision, DateTimeOffset CreatedAt, DateTimeOffset? AvailabilityChangedAt);
internal sealed record IndividualAvailabilityResponse(Guid IndividualId, string Availability, long Revision, DateTimeOffset AvailabilityChangedAt);
