using System.Security.Claims;
using Application.CoreApi.Authorization;
using Application.IdentityAccess;
using Application.Orders;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi;

internal static class TenantOrderActionsEndpoint
{
    internal static async Task<IResult> GetAsync(
        Guid tenantId,
        Guid orderId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        ResolveTenantContext resolveTenantContext,
        IAuthorizationService authorization,
        GetOrderDraft getOrderDraft,
        CancellationToken cancellationToken)
    {
        var access = await TenantRequestAccess.ResolveAsync(
            tenantId, httpContext, principal, resolveAccount, resolveTenantContext, cancellationToken);
        if (access.Failure is not null)
            return access.Failure;
        var denied = await TenantOrderEndpoint.AuthorizeAsync(httpContext, principal, access.TenantContext!, authorization,
            "The account is not permitted to view order actions in this tenant.", cancellationToken);
        if (denied is not null)
            return denied;

        OrderDraftSnapshot? order;
        try
        {
            order = await getOrderDraft.ExecuteAsync(access.TenantContext!, orderId, cancellationToken);
        }
        catch (OrderDraftValidationException exception)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid order request.", detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
        }
        if (order is null)
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Order not found.", detail: "The requested order was not found in this tenant.",
                extensions: new Dictionary<string, object?> { ["code"] = "order_not_found" });
        if (order.OrderId != orderId || order.TenantId != access.TenantContext!.TenantId)
            throw new InvalidOperationException("The order query returned an inconsistent identity.");

        var quotationBound = order.QuotationOrigin is not null;
        var lifecycle = OrderDraftActionGuide.Explain(
            order.State, order.Revision, mayRevise: true, mayAbandon: true, mayCommit: true, quotationBound: quotationBound);
        var mayRevise = lifecycle.Actions.Single(action => action.Action == OrderDraftAction.Revise).Available;
        var mayAbandon = lifecycle.Actions.Single(action => action.Action == OrderDraftAction.Abandon).Available;
        var mayCommit = lifecycle.Actions.Single(action => action.Action == OrderDraftAction.Commit).Available;
        var resource = new TenantOrderResource(access.TenantContext!, cancellationToken);
        try
        {
            // A terminal lifecycle needs no write-permission lookup. Do not turn a
            // provider outage into a false permission denial or a partially usable guide.
            if (mayRevise)
                mayRevise = (await authorization.AuthorizeAsync(principal, resource, EditOrderRequirement.Instance)).Succeeded;
            if (mayRevise)
                mayRevise = (await authorization.AuthorizeAsync(principal, resource, ApplyManualPriceRequirement.Instance)).Succeeded;
            if (mayAbandon)
                mayAbandon = (await authorization.AuthorizeAsync(principal, resource, AbandonOrderRequirement.Instance)).Succeeded;
            if (mayCommit)
                mayCommit = (await authorization.AuthorizeAsync(principal, resource, CommitOrderRequirement.Instance)).Succeeded;
        }
        catch (AuthorizationProviderUnavailableException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Authorization is temporarily unavailable.", detail: "The request could not be authorized safely.",
                extensions: new Dictionary<string, object?> { ["code"] = "authorization_unavailable" });
        }

        var guide = OrderDraftActionGuide.Explain(order.State, order.Revision, mayRevise, mayAbandon, mayCommit, quotationBound: quotationBound);
        return TypedResults.Ok(new OrderDraftActionsResponse(orderId, guide.ObservedRevision,
            guide.Actions.Select(action => new OrderDraftActionResponse(action.Action switch
            {
                OrderDraftAction.Revise => "revise",
                OrderDraftAction.Abandon => "abandon",
                OrderDraftAction.Commit => "commit",
                _ => throw new InvalidOperationException("The order guide contains an unsupported action."),
            }, action.Available, action.Unavailability switch
            {
                null => null,
                OrderDraftActionUnavailability.PermissionRequired => "permission_required",
                OrderDraftActionUnavailability.AlreadyAbandoned => "order_already_abandoned",
                OrderDraftActionUnavailability.AlreadyCommitted => "order_already_committed",
                OrderDraftActionUnavailability.QuotationBound => "order_quotation_bound",
                _ => throw new InvalidOperationException("The order guide contains an unsupported reason."),
            })).ToArray()));
    }
}

internal sealed record OrderDraftActionsResponse(Guid OrderId, long ObservedRevision, IReadOnlyList<OrderDraftActionResponse> Actions);

internal sealed record OrderDraftActionResponse(string Action, bool Available, string? UnavailabilityCode);
