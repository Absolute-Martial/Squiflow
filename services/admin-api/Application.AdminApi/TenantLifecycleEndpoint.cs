using Application.Tenancy;

namespace Application.AdminApi;

internal static class TenantLifecycleEndpoint
{
    internal static async Task<IResult> PostAsync(
        Guid tenantId,
        string operation,
        HttpContext context,
        PlatformAdminRequestAuthorizer authorizer,
        ManageTenantLifecycle manager)
    {
        var authorized = await authorizer.AuthorizeAsync(
            context, $"tenant_{operation}", PlatformAdminPermission.ManageTenantLifecycle).ConfigureAwait(false);
        if (authorized.Access is null) return authorized.Failure!;
        var keys = context.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1)
            return BoundedAdminJson.Problem(400, "idempotency_key_required", "Exactly one Idempotency-Key header is required.");
        var parsed = operation switch
        {
            "suspend" => TenantLifecycleOperation.Suspend,
            "reactivate" => TenantLifecycleOperation.Reactivate,
            _ => (TenantLifecycleOperation?)null,
        };
        if (parsed is null)
            return BoundedAdminJson.Problem(404, "tenant_operation_not_found", "The tenant operation was not found.");
        var payload = await BoundedAdminJson.ReadObjectAsync(context.Request, context.RequestAborted).ConfigureAwait(false);
        using var document = payload.Document;
        if (payload.Failure is not null) return payload.Failure;
        if (!BoundedAdminJson.TrySinglePositiveInt(document!.RootElement, "expectedRevision", out var revision))
            return BoundedAdminJson.Problem(400, "invalid_tenant_lifecycle_request", "The tenant lifecycle request is invalid.");
        TenantLifecycleIntent intent;
        try
        {
            intent = TenantLifecycleIntent.Create(parsed.Value, tenantId, revision, keys[0]!);
        }
        catch (ArgumentException)
        {
            return BoundedAdminJson.Problem(400, "invalid_tenant_lifecycle_request", "The tenant lifecycle request is invalid.");
        }
        var actor = TenantMembershipAdministrationActor.Create(
            authorized.Access.PrincipalId, authorized.Access.DeviceId);
        var result = await manager.ExecuteAsync(actor, intent, context.RequestAborted).ConfigureAwait(false);
        if (result.Status is TenantLifecycleStatus.Suspended or TenantLifecycleStatus.Reactivated)
        {
            var tenant = result.Tenant!;
            return TypedResults.Ok(new
            {
                tenant.TenantId,
                availability = tenant.Availability.ToString().ToLowerInvariant(),
                tenant.Revision,
                tenant.SuspendedAt,
                result.Replayed,
            });
        }
        return result.Status switch
        {
            TenantLifecycleStatus.TenantNotFound =>
                BoundedAdminJson.Problem(404, "tenant_not_found", "The tenant was not found."),
            TenantLifecycleStatus.IdempotencyKeyConflict =>
                BoundedAdminJson.Problem(409, "idempotency_key_conflict", "The Idempotency-Key was already used for another request."),
            TenantLifecycleStatus.RevisionConflict =>
                BoundedAdminJson.Problem(409, "revision_conflict", "The expected tenant revision is stale."),
            TenantLifecycleStatus.InvalidTransition =>
                BoundedAdminJson.Problem(409, "invalid_tenant_transition", "The tenant transition is invalid."),
            TenantLifecycleStatus.RevisionLimitReached =>
                BoundedAdminJson.Problem(409, "revision_limit_reached", "The tenant revision limit was reached."),
            _ => throw new InvalidOperationException("Tenant lifecycle returned an invalid result."),
        };
    }
}
