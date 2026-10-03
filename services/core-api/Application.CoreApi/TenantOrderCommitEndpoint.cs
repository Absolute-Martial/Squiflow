using System.Security.Claims;
using Application.CoreApi.Authorization;
using Application.IdentityAccess;
using Application.Orders;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi;

internal static class TenantOrderCommitEndpoint
{
    internal static async Task<IResult> CommitAsync(
        Guid tenantId,
        Guid orderId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        ResolveTenantContext resolveTenantContext,
        IAuthorizationService authorization,
        CommitOrderDraft commitOrderDraft,
        CoreApiMutationDiagnostics diagnostics,
        CancellationToken cancellationToken)
    {
        var access = await TenantRequestAccess.ResolveAsync(
            tenantId, httpContext, principal, resolveAccount, resolveTenantContext, cancellationToken);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        var authorizationFailure = await TenantOrderEndpoint.AuthorizeAsync(
            principal,
            access.TenantContext!,
            authorization,
            CommitOrderRequirement.Instance,
            "The account is not permitted to commit orders in this tenant.",
            cancellationToken);
        if (authorizationFailure is not null)
        {
            return authorizationFailure;
        }

        if (!TenantOrderEndpoint.TryGetIdempotencyKey(httpContext.Request.Headers, out var idempotencyKey))
        {
            return TenantOrderEndpoint.InvalidRequest(
                "idempotency_key_invalid",
                "Idempotency-Key is required and must contain one header value.");
        }

        var payload = await OrderDraftTransitionPayloadReader.ReadExpectedRevisionAsync(
            httpContext.Request,
            TenantOrderEndpoint.MaximumAbandonRequestBodyBytes,
            cancellationToken);
        if (payload.Failure is not null)
        {
            return payload.Failure;
        }

        CommitOrderDraftResult result;
        try
        {
            result = await commitOrderDraft.ExecuteAsync(
                access.TenantContext!,
                new CommitOrderDraftRequest(orderId, payload.ExpectedRevision!.Value),
                idempotencyKey!,
                cancellationToken);
        }
        catch (OrderDraftValidationException exception)
        {
            return TenantOrderEndpoint.InvalidRequest(exception.Code, exception.Message);
        }

        if (result.Status == CommitOrderDraftStatus.NotFound)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order not found.",
                detail: "The requested order was not found in this tenant.",
                extensions: new Dictionary<string, object?> { ["code"] = "order_not_found" });
        }

        if (result.Status is CommitOrderDraftStatus.RevisionConflict or
            CommitOrderDraftStatus.AlreadyCommitted or
            CommitOrderDraftStatus.AlreadyAbandoned or
            CommitOrderDraftStatus.IdempotencyKeyConflict)
        {
            var (code, detail) = result.Status switch
            {
                CommitOrderDraftStatus.RevisionConflict =>
                    ("revision_conflict", "The order revision does not match the expected revision."),
                CommitOrderDraftStatus.AlreadyCommitted =>
                    ("order_already_committed", "The order has already been committed."),
                CommitOrderDraftStatus.AlreadyAbandoned =>
                    ("order_already_abandoned", "The order draft has already been abandoned."),
                _ =>
                    ("idempotency_key_conflict", "The Idempotency-Key has already been used for a different commit request."),
            };
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Order conflict.",
                detail: detail,
                extensions: new Dictionary<string, object?> { ["code"] = code });
        }

        if (result.Status is not (CommitOrderDraftStatus.Committed or CommitOrderDraftStatus.Replayed))
        {
            throw new InvalidOperationException("The commit command returned an unsupported status.");
        }

        var order = result.Order
            ?? throw new InvalidOperationException("A successful commit command did not return an order receipt.");
        if (order.OrderId != orderId || order.TenantId != access.TenantContext!.TenantId ||
            order.State != OrderDraftState.Committed || order.CommittedAt is null ||
            order.CommittedByAccountId != access.TenantContext.AccountId)
        {
            throw new InvalidOperationException("A successful commit command returned an invalid commitment receipt.");
        }

        diagnostics.RecordSuccess(CoreApiMutation.OrderDraftCommitted, access.TenantContext!, order.OrderId,
            result.Status == CommitOrderDraftStatus.Replayed, httpContext.TraceIdentifier);
        httpContext.Response.Headers.CacheControl = "no-store";
        if (result.Status == CommitOrderDraftStatus.Replayed)
        {
            httpContext.Response.Headers.Append("Idempotency-Replayed", "true");
        }

        return TypedResults.Ok(new CommitOrderDraftResponse(
            order.OrderId,
            TenantOrderEndpoint.ToWireState(order.State),
            order.Revision,
            order.CommittedAt.Value));
    }
}

internal sealed record CommitOrderDraftPayload(long ExpectedRevision);

internal sealed record CommitOrderDraftResponse(
    Guid OrderId,
    string State,
    long Revision,
    DateTimeOffset CommittedAt);
