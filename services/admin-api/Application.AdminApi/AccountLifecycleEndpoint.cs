using Application.IdentityAccess;

namespace Application.AdminApi;

internal static class AccountLifecycleEndpoint
{
    internal static async Task<IResult> PostAsync(Guid accountId, string operation, HttpContext context,
        ManageAccountAvailability manager)
    {
        var access = AdminApiPlatformAuthorization.GetRequiredAccess(context);
        if (accountId == Guid.Empty) return InvalidRequest();
        var parsed = operation switch
        {
            "suspend" => AccountLifecycleOperation.Suspend,
            "reactivate" => AccountLifecycleOperation.Reactivate,
            _ => (AccountLifecycleOperation?)null,
        };
        if (parsed is null)
            return BoundedAdminJson.Problem(404, "account_operation_not_found", "The account operation was not found.");
        var keys = context.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1)
            return BoundedAdminJson.Problem(400, "idempotency_key_required", "Exactly one Idempotency-Key header is required.");
        var payload = await BoundedAdminJson.ReadObjectAsync(context.Request, context.RequestAborted).ConfigureAwait(false);
        using var document = payload.Document;
        if (payload.Failure is not null) return payload.Failure;
        if (!TryRequest(document!.RootElement, out var revision, out var reason))
            return InvalidRequest();
        AccountLifecycleIntent intent;
        try { intent = AccountLifecycleIntent.Create(parsed.Value, accountId, revision, reason!, keys[0]!); }
        catch (ArgumentException) { return InvalidRequest(); }
        var result = await manager.ExecuteAsync(IdentityAdministrationActor.Create(access.PrincipalId, access.DeviceId),
            intent, context.RequestAborted).ConfigureAwait(false);
        return result.Status switch
        {
            AccountLifecycleStatus.Changed => TypedResults.Ok(new
            {
                result.Account!.AccountId,
                availability = result.Account.Availability.ToString().ToLowerInvariant(),
                result.Account.Revision,
                result.Account.DisabledAt,
                result.Account.ChangedAt,
                result.Replayed,
            }),
            AccountLifecycleStatus.AccountNotFound => BoundedAdminJson.Problem(404, "account_not_found", "The account was not found."),
            AccountLifecycleStatus.IdempotencyKeyConflict => BoundedAdminJson.Problem(409, "idempotency_key_conflict", "The Idempotency-Key was already used for another request."),
            AccountLifecycleStatus.RevisionConflict => BoundedAdminJson.Problem(409, "revision_conflict", "The expected account revision is stale."),
            AccountLifecycleStatus.InvalidTransition => BoundedAdminJson.Problem(409, "invalid_account_transition", "The account transition is invalid."),
            AccountLifecycleStatus.RevisionLimitReached => BoundedAdminJson.Problem(409, "revision_limit_reached", "The account revision limit was reached."),
            _ => throw new InvalidOperationException("Account lifecycle returned an invalid result."),
        };
    }

    private static bool TryRequest(System.Text.Json.JsonElement root, out int revision, out string? reason)
    {
        revision = 0;
        reason = null;
        var count = 0;
        foreach (var property in root.EnumerateObject())
        {
            count++;
            if (property.NameEquals("expectedRevision") && revision == 0 &&
                property.Value.ValueKind == System.Text.Json.JsonValueKind.Number &&
                property.Value.TryGetInt32(out var parsed) && parsed > 0) revision = parsed;
            else if (property.NameEquals("reason") && reason is null &&
                     property.Value.ValueKind == System.Text.Json.JsonValueKind.String) reason = property.Value.GetString();
            else return false;
        }
        return count == 2 && revision > 0 && reason is not null;
    }

    private static IResult InvalidRequest() =>
        BoundedAdminJson.Problem(400, "invalid_account_lifecycle_request", "The account lifecycle request is invalid.");
}
