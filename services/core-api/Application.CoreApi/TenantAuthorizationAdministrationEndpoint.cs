using Application.CoreApi.Authorization;
using Application.Tenancy;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Primitives;

namespace Application.CoreApi;

internal static class TenantAuthorizationAdministrationEndpoint
{
    internal const long MaximumRequestBodyBytes = 32 * 1024;

    internal static async Task<IResult> GetAsync(
        Guid tenantId,
        HttpContext httpContext,
        ITenantAuthorizationAdministrationStore store)
    {
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, httpContext);
        var revision = await store.GetAuthorizationRevisionAsync(tenantId, httpContext.RequestAborted).ConfigureAwait(false) ?? 1;
        var roles = await store.ListRolesAsync(tenantId, httpContext.RequestAborted).ConfigureAwait(false);
        return TypedResults.Ok(new TenantAuthorizationAdministrationResponse(
            context.TenantId,
            revision,
            TenantPermissionCatalog.All,
            roles));
    }

    internal static Task<IResult> GrantPermissionAsync(
        Guid tenantId,
        PermissionChangePayload payload,
        HttpContext httpContext,
        TenantAuthorizationAdministration administration) =>
        ProposePermissionAsync(
            TenantAuthorizationProposalKind.GrantPermission,
            tenantId,
            payload,
            httpContext,
            administration);

    internal static Task<IResult> RevokePermissionAsync(
        Guid tenantId,
        PermissionChangePayload payload,
        HttpContext httpContext,
        TenantAuthorizationAdministration administration) =>
        ProposePermissionAsync(
            TenantAuthorizationProposalKind.RevokePermission,
            tenantId,
            payload,
            httpContext,
            administration);

    internal static async Task<IResult> CreateRoleAsync(
        Guid tenantId,
        CreateRolePayload payload,
        HttpContext httpContext,
        TenantAuthorizationAdministration administration)
    {
        if (!TryIdempotencyKey(httpContext.Request.Headers, out var key)) return InvalidIdempotencyKey();
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, httpContext);
        try
        {
            var intent = TenantAuthorizationProposalIntent.CreateRole(
                tenantId,
                payload.RoleId,
                payload.Name ?? string.Empty,
                payload.PermissionIds ?? [],
                payload.ExpectedAuthorizationRevision,
                key!);
            return ProposalResult(await administration.ProposeAsync(
                TenantAuthorizationActor.Create(context.AccountId),
                intent,
                httpContext.RequestAborted).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Invalid(exception.Message);
        }
    }

    internal static async Task<IResult> ReviseRoleAsync(
        Guid tenantId,
        Guid roleId,
        ReviseRolePayload payload,
        HttpContext httpContext,
        TenantAuthorizationAdministration administration)
    {
        if (!TryIdempotencyKey(httpContext.Request.Headers, out var key)) return InvalidIdempotencyKey();
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, httpContext);
        try
        {
            var intent = TenantAuthorizationProposalIntent.ReviseRole(
                tenantId,
                roleId,
                payload.ExpectedRoleRevision,
                payload.Name ?? string.Empty,
                payload.PermissionIds ?? [],
                payload.ExpectedAuthorizationRevision,
                key!);
            return ProposalResult(await administration.ProposeAsync(
                TenantAuthorizationActor.Create(context.AccountId), intent, httpContext.RequestAborted).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Invalid(exception.Message);
        }
    }

    internal static async Task<IResult> RetireRoleAsync(
        Guid tenantId,
        Guid roleId,
        RetireRolePayload payload,
        HttpContext httpContext,
        TenantAuthorizationAdministration administration)
    {
        if (!TryIdempotencyKey(httpContext.Request.Headers, out var key)) return InvalidIdempotencyKey();
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, httpContext);
        try
        {
            var intent = TenantAuthorizationProposalIntent.RetireRole(
                tenantId,
                roleId,
                payload.ExpectedRoleRevision,
                payload.ExpectedAuthorizationRevision,
                key!);
            return ProposalResult(await administration.ProposeAsync(
                TenantAuthorizationActor.Create(context.AccountId), intent, httpContext.RequestAborted).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Invalid(exception.Message);
        }
    }

    internal static Task<IResult> AssignRoleAsync(
        Guid tenantId,
        Guid roleId,
        Guid accountId,
        RoleAssignmentPayload payload,
        HttpContext httpContext,
        TenantAuthorizationAdministration administration) =>
        ProposeRoleAssignmentAsync(
            TenantAuthorizationProposalKind.AssignRole,
            tenantId,
            roleId,
            accountId,
            payload,
            httpContext,
            administration);

    internal static Task<IResult> UnassignRoleAsync(
        Guid tenantId,
        Guid roleId,
        Guid accountId,
        RoleAssignmentPayload payload,
        HttpContext httpContext,
        TenantAuthorizationAdministration administration) =>
        ProposeRoleAssignmentAsync(
            TenantAuthorizationProposalKind.UnassignRole,
            tenantId,
            roleId,
            accountId,
            payload,
            httpContext,
            administration);

    internal static async Task<IResult> GetProposalAsync(
        Guid tenantId,
        Guid proposalId,
        HttpContext httpContext,
        ITenantAuthorizationAdministrationStore store)
    {
        _ = TenantRequestAccess.RequireResolvedContext(tenantId, httpContext);
        var proposal = await store.FindProposalAsync(tenantId, proposalId, httpContext.RequestAborted).ConfigureAwait(false);
        return proposal is null
            ? TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Authorization proposal not found.",
                extensions: new Dictionary<string, object?> { ["code"] = "authorization_proposal_not_found" })
            : TypedResults.Ok(proposal);
    }

    internal static async Task<IResult> ReconcileProposalAsync(
        Guid tenantId,
        Guid proposalId,
        HttpContext httpContext,
        TenantAuthorizationReconciler reconciler)
    {
        _ = TenantRequestAccess.RequireResolvedContext(tenantId, httpContext);
        var proposal = await reconciler.ReconcileAsync(tenantId, proposalId, httpContext.RequestAborted).ConfigureAwait(false);
        if (proposal is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Authorization proposal not found.",
                extensions: new Dictionary<string, object?> { ["code"] = "authorization_proposal_not_found" });
        }

        return proposal.Status switch
        {
            TenantAuthorizationProposalStatus.Applied => TypedResults.Ok(proposal),
            TenantAuthorizationProposalStatus.Pending or TenantAuthorizationProposalStatus.Uncertain =>
                Results.Json(proposal, statusCode: StatusCodes.Status202Accepted),
            TenantAuthorizationProposalStatus.Failed => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Authorization reconciliation failed.",
                detail: "The requested authorization change was not applied.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = proposal.FailureCode ?? "authorization_reconciliation_failed",
                    ["proposalId"] = proposal.ProposalId,
                }),
            _ => throw new InvalidOperationException("Unknown authorization proposal status."),
        };
    }

    internal static async Task<IResult> TransferOwnerAsync(
        Guid tenantId,
        OwnerTransferPayload payload,
        HttpContext httpContext,
        TenantAuthorizationAdministration administration,
        HighRiskActionAdmission highRiskAdmission)
    {
        if (!TryIdempotencyKey(httpContext.Request.Headers, out var key)) return InvalidIdempotencyKey();
        var highRiskFailure = highRiskAdmission.Require(httpContext.User, HighRiskAction.TransferInitialOwner);
        if (highRiskFailure is not null) return highRiskFailure;
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, httpContext);
        try
        {
            var result = await administration.TransferOwnerAsync(
                TenantAuthorizationActor.Create(context.AccountId),
                TenantOwnerTransferIntent.Create(
                    tenantId,
                    payload.TargetAccountId,
                    payload.ExpectedTenantRevision,
                    key!),
                httpContext.RequestAborted).ConfigureAwait(false);
            return result.Status switch
            {
                TenantOwnerTransferStatus.Transferred or TenantOwnerTransferStatus.Replayed => TypedResults.Ok(result),
                TenantOwnerTransferStatus.ActorNotInitialOwner => TenantRequestAccess.Problem(
                    "tenant_role_administration_denied", "Current initial-Owner authority is required."),
                TenantOwnerTransferStatus.TenantNotFound => TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Tenant not found.",
                    extensions: new Dictionary<string, object?> { ["code"] = "tenant_not_found" }),
                _ => Conflict("owner_transfer_conflict", "The Owner transfer no longer matches current tenant or membership state."),
            };
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Invalid(exception.Message);
        }
    }

    private static async Task<IResult> ProposePermissionAsync(
        TenantAuthorizationProposalKind kind,
        Guid tenantId,
        PermissionChangePayload payload,
        HttpContext httpContext,
        TenantAuthorizationAdministration administration)
    {
        if (!TryIdempotencyKey(httpContext.Request.Headers, out var key)) return InvalidIdempotencyKey();
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, httpContext);
        try
        {
            var intent = TenantAuthorizationProposalIntent.PermissionChange(
                kind,
                tenantId,
                payload.TargetAccountId,
                payload.PermissionId ?? string.Empty,
                payload.ExpectedAuthorizationRevision,
                key!);
            return ProposalResult(await administration.ProposeAsync(
                TenantAuthorizationActor.Create(context.AccountId), intent, httpContext.RequestAborted).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Invalid(exception.Message);
        }
    }

    private static async Task<IResult> ProposeRoleAssignmentAsync(
        TenantAuthorizationProposalKind kind,
        Guid tenantId,
        Guid roleId,
        Guid accountId,
        RoleAssignmentPayload payload,
        HttpContext httpContext,
        TenantAuthorizationAdministration administration)
    {
        if (!TryIdempotencyKey(httpContext.Request.Headers, out var key)) return InvalidIdempotencyKey();
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, httpContext);
        try
        {
            var intent = TenantAuthorizationProposalIntent.RoleAssignment(
                kind,
                tenantId,
                roleId,
                accountId,
                payload.ExpectedAuthorizationRevision,
                key!);
            return ProposalResult(await administration.ProposeAsync(
                TenantAuthorizationActor.Create(context.AccountId), intent, httpContext.RequestAborted).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return Invalid(exception.Message);
        }
    }

    private static IResult ProposalResult(TenantAuthorizationProposalResult result) => result.Status switch
    {
        TenantAuthorizationProposalResultStatus.Created =>
            Results.Json(result.Proposal, statusCode: StatusCodes.Status202Accepted),
        TenantAuthorizationProposalResultStatus.Replayed =>
            result.Proposal?.Status == TenantAuthorizationProposalStatus.Applied
                ? TypedResults.Ok(result.Proposal)
                : Results.Json(result.Proposal, statusCode: StatusCodes.Status202Accepted),
        TenantAuthorizationProposalResultStatus.ActorNotInitialOwner =>
            TenantRequestAccess.Problem("tenant_role_administration_denied", "Current initial-Owner authority is required."),
        TenantAuthorizationProposalResultStatus.TenantNotFound => TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Tenant not found.",
            extensions: new Dictionary<string, object?> { ["code"] = "tenant_not_found" }),
        TenantAuthorizationProposalResultStatus.IdempotencyKeyConflict =>
            Conflict("idempotency_key_conflict", "The Idempotency-Key was already used for a different authorization request."),
        TenantAuthorizationProposalResultStatus.AuthorizationRevisionConflict =>
            Conflict("authorization_revision_conflict", "The authorization revision changed or another reconciliation is still active."),
        TenantAuthorizationProposalResultStatus.TargetMembershipUnavailable =>
            Conflict("target_membership_unavailable", "The target is not an active member for this operation."),
        TenantAuthorizationProposalResultStatus.RoleNotFound =>
            Conflict("role_not_found", "The custom role is not available for this operation."),
        TenantAuthorizationProposalResultStatus.RoleRevisionConflict =>
            Conflict("role_revision_conflict", "The custom role revision changed."),
        TenantAuthorizationProposalResultStatus.RoleAlreadyExists =>
            Conflict("role_conflict", "The requested custom role identity or active name is unavailable."),
        TenantAuthorizationProposalResultStatus.RoleRetired =>
            Conflict("role_retired", "The custom role is retired or unavailable."),
        TenantAuthorizationProposalResultStatus.RoleAssignmentConflict =>
            Conflict("role_assignment_conflict", "The custom role assignment is already in the requested state or the bounded assignment ceiling was reached."),
        TenantAuthorizationProposalResultStatus.DirectPermissionConflict =>
            Conflict("permission_grant_conflict", "The direct permission grant is already in the requested state."),
        TenantAuthorizationProposalResultStatus.TenantUnavailable =>
            Conflict("tenant_unavailable", "The tenant is not active."),
        _ => throw new InvalidOperationException("Unknown authorization proposal result."),
    };

    private static bool TryIdempotencyKey(IHeaderDictionary headers, out string? key)
    {
        if (!headers.TryGetValue("Idempotency-Key", out StringValues values) || values.Count != 1)
        {
            key = null;
            return false;
        }
        key = values[0];
        return !string.IsNullOrWhiteSpace(key);
    }

    private static ProblemHttpResult InvalidIdempotencyKey() =>
        Invalid("Idempotency-Key is required and must contain exactly one non-empty value.");

    private static ProblemHttpResult Invalid(string detail) => TypedResults.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Authorization administration request is invalid.",
        detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = "authorization_request_invalid" });

    private static ProblemHttpResult Conflict(string code, string detail) => TypedResults.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Authorization administration conflict.",
        detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = code });

    internal sealed record PermissionChangePayload(
        Guid TargetAccountId,
        string? PermissionId,
        int ExpectedAuthorizationRevision);

    internal sealed record CreateRolePayload(
        Guid RoleId,
        string? Name,
        string[]? PermissionIds,
        int ExpectedAuthorizationRevision);

    internal sealed record ReviseRolePayload(
        string? Name,
        string[]? PermissionIds,
        int ExpectedRoleRevision,
        int ExpectedAuthorizationRevision);

    internal sealed record RetireRolePayload(
        int ExpectedRoleRevision,
        int ExpectedAuthorizationRevision);

    internal sealed record RoleAssignmentPayload(int ExpectedAuthorizationRevision);

    internal sealed record OwnerTransferPayload(Guid TargetAccountId, int ExpectedTenantRevision);
}

internal sealed record TenantAuthorizationAdministrationResponse(
    Guid TenantId,
    int AuthorizationRevision,
    IReadOnlyList<TenantPermissionDefinition> PermissionCatalog,
    IReadOnlyList<TenantCustomRole> Roles);
