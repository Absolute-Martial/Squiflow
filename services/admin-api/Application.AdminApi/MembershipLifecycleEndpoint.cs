using Application.Tenancy;

namespace Application.AdminApi;

internal static class MembershipLifecycleEndpoint
{
    private const string IdempotencyHeader = "Idempotency-Key";

    internal static Task<IResult> InviteAsync(
        Guid tenantId,
        HttpContext context,
        ManageTenantMembership manager) =>
        CreateAsync(tenantId, false, context, manager);

    internal static Task<IResult> BootstrapOwnerAsync(
        Guid tenantId,
        HttpContext context,
        ManageTenantMembership manager) =>
        CreateAsync(tenantId, true, context, manager);

    internal static async Task<IResult> TransitionAsync(
        Guid tenantId,
        Guid accountId,
        string operation,
        HttpContext context,
        ManageTenantMembership manager)
    {
        var access = AdminApiPlatformAuthorization.GetRequiredAccess(context);
        if (!TryKey(context, out var key, out var keyFailure)) return keyFailure!;
        var parsedOperation = operation switch
        {
            "activate" => MembershipLifecycleOperation.Activate,
            "suspend" => MembershipLifecycleOperation.Suspend,
            "remove" => MembershipLifecycleOperation.Remove,
            _ => (MembershipLifecycleOperation?)null,
        };
        if (parsedOperation is null)
            return BoundedAdminJson.Problem(404, "membership_operation_not_found", "The membership operation was not found.");
        var payload = await BoundedAdminJson.ReadObjectAsync(context.Request, context.RequestAborted).ConfigureAwait(false);
        using var document = payload.Document;
        if (payload.Failure is not null) return payload.Failure;
        if (!BoundedAdminJson.TrySinglePositiveInt(document!.RootElement, "expectedRevision", out var revision))
            return BoundedAdminJson.Problem(400, "invalid_membership_request", "The membership request is invalid.");

        TenantMembershipLifecycleIntent intent;
        try
        {
            intent = TenantMembershipLifecycleIntent.Transition(
                parsedOperation.Value, tenantId, accountId, revision, key!);
        }
        catch (ArgumentException)
        {
            return BoundedAdminJson.Problem(400, "invalid_membership_request", "The membership request is invalid.");
        }
        return Result(await manager.ExecuteAsync(Actor(access), intent, context.RequestAborted).ConfigureAwait(false));
    }

    private static async Task<IResult> CreateAsync(
        Guid tenantId,
        bool owner,
        HttpContext context,
        ManageTenantMembership manager)
    {
        var access = AdminApiPlatformAuthorization.GetRequiredAccess(context);
        if (!TryKey(context, out var key, out var keyFailure)) return keyFailure!;
        var payload = await BoundedAdminJson.ReadObjectAsync(context.Request, context.RequestAborted).ConfigureAwait(false);
        using var document = payload.Document;
        if (payload.Failure is not null) return payload.Failure;
        if (!BoundedAdminJson.TrySingleGuid(document!.RootElement, "accountId", out var accountId))
            return BoundedAdminJson.Problem(400, "invalid_membership_request", "The membership request is invalid.");
        TenantMembershipLifecycleIntent intent;
        try
        {
            intent = owner
                ? TenantMembershipLifecycleIntent.BootstrapOwner(tenantId, accountId, key!)
                : TenantMembershipLifecycleIntent.Invite(tenantId, accountId, key!);
        }
        catch (ArgumentException)
        {
            return BoundedAdminJson.Problem(400, "invalid_membership_request", "The membership request is invalid.");
        }
        return Result(await manager.ExecuteAsync(Actor(access), intent, context.RequestAborted).ConfigureAwait(false));
    }

    private static TenantMembershipAdministrationActor Actor(PlatformAdminRequestAccess access) =>
        TenantMembershipAdministrationActor.Create(access.PrincipalId, access.DeviceId);

    private static IResult Result(TenantMembershipLifecycleResult result)
    {
        var response = result.Membership is null ? null : new
        {
            result.Membership.TenantId,
            result.Membership.AccountId,
            availability = result.Membership.Availability.ToString().ToLowerInvariant(),
            result.Membership.Revision,
            result.Membership.IsInitialOwner,
            result.Membership.InvitedAt,
            result.Membership.ActivatedAt,
            result.Membership.SuspendedAt,
            result.Membership.RemovedAt,
        };
        return result.Status switch
        {
            MembershipLifecycleStatus.Invited or MembershipLifecycleStatus.OwnerBootstrapped =>
                Results.Json(response, statusCode: result.Replayed ? 200 : 201),
            MembershipLifecycleStatus.Activated or MembershipLifecycleStatus.Suspended or MembershipLifecycleStatus.Removed =>
                TypedResults.Ok(response),
            MembershipLifecycleStatus.TenantNotFound or MembershipLifecycleStatus.AccountNotFound or
                MembershipLifecycleStatus.MembershipNotFound =>
                BoundedAdminJson.Problem(404, "membership_target_not_found", "The membership target was not found."),
            MembershipLifecycleStatus.IdempotencyKeyConflict or MembershipLifecycleStatus.MembershipAlreadyExists or
                MembershipLifecycleStatus.RevisionConflict or MembershipLifecycleStatus.InvalidTransition or
                MembershipLifecycleStatus.InitialOwnerAlreadyExists or MembershipLifecycleStatus.InitialOwnerProtected or
                MembershipLifecycleStatus.AccountUnavailable or MembershipLifecycleStatus.RevisionLimitReached =>
                BoundedAdminJson.Problem(409, StatusCode(result.Status), "The membership operation conflicts with current state."),
            _ => throw new InvalidOperationException("Membership lifecycle returned an invalid result."),
        };
    }

    private static string StatusCode(MembershipLifecycleStatus status) =>
        status.ToString().SelectMany((character, index) =>
            index > 0 && char.IsUpper(character) ? new[] { '_', char.ToLowerInvariant(character) } :
            new[] { char.ToLowerInvariant(character) }).Aggregate(string.Empty, (value, character) => value + character);

    private static bool TryKey(HttpContext context, out string? key, out IResult? failure)
    {
        var values = context.Request.Headers[IdempotencyHeader];
        key = values.Count == 1 ? values[0] : null;
        failure = key is null
            ? BoundedAdminJson.Problem(400, "idempotency_key_required", "Exactly one Idempotency-Key header is required.")
            : null;
        return failure is null;
    }
}
