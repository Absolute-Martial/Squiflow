using System.Globalization;
using System.Security.Claims;
using Application.CoreApi.Authorization;
using Application.IdentityAccess;
using Application.Orders;
using Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Application.CoreApi;

internal static class TenantOrderHistoryEndpoint
{
    internal static async Task<IResult> GetAsync(
        Guid tenantId,
        Guid orderId,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        ResolveAccountBinding resolveAccount,
        ResolveTenantContext resolveTenantContext,
        IAuthorizationService authorization,
        GetOrderDraftHistory getHistory,
        CancellationToken cancellationToken)
    {
        var access = await TenantRequestAccess.ResolveAsync(
            tenantId, httpContext, principal, resolveAccount, resolveTenantContext, cancellationToken);
        if (access.Failure is not null)
            return access.Failure;
        var denied = await TenantOrderEndpoint.AuthorizeAsync(httpContext, principal, access.TenantContext!, authorization,
            "The account is not permitted to view order history in this tenant.", cancellationToken);
        if (denied is not null)
            return denied;

        var query = httpContext.Request.Query;
        var limit = 5;
        long? beforeRevision = null;
        if (query.TryGetValue("limit", out var limits) &&
            (limits.Count != 1 || !int.TryParse(limits[0], NumberStyles.None, CultureInfo.InvariantCulture, out limit)))
            return Invalid("limit_invalid", "History page size must be one integer between 1 and 10.");
        if (query.TryGetValue("beforeRevision", out var revisions))
        {
            if (revisions.Count != 1 || !long.TryParse(revisions[0], NumberStyles.None, CultureInfo.InvariantCulture, out var revision))
                return Invalid("history_revision_invalid", "Before revision must be one positive integer.");
            beforeRevision = revision;
        }

        OrderDraftHistoryPage? page;
        try
        {
            page = await getHistory.ExecuteAsync(access.TenantContext!,
                new GetOrderDraftHistoryRequest(orderId, limit, beforeRevision), cancellationToken);
        }
        catch (OrderDraftValidationException exception)
        {
            return Invalid(exception.Code, exception.Message);
        }

        if (page is null)
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Order not found.", detail: "The requested order was not found in this tenant.",
                extensions: new Dictionary<string, object?> { ["code"] = "order_not_found" });
        return TypedResults.Ok(new OrderDraftHistoryResponse(page.CurrentRevision,
            page.Items.Select(entry => new OrderDraftHistoryEntryResponse(entry.Change switch
            {
                OrderDraftChange.Created => "created",
                OrderDraftChange.Revised => "revised",
                OrderDraftChange.Abandoned => "abandoned",
                OrderDraftChange.Committed => "committed",
                OrderDraftChange.ProgramReferenceUpdated => "program-reference-updated",
                _ => throw new InvalidOperationException("The order history contains an unsupported change."),
            }, entry.ChangedByAccountId, entry.RecordedAt, TenantOrderEndpoint.ToResponse(entry.Order))).ToArray(),
            page.NextBeforeRevision));
    }

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Invalid(string code, string detail) =>
        TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid order history request.", detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}

internal sealed record OrderDraftHistoryResponse(
    long CurrentRevision,
    IReadOnlyList<OrderDraftHistoryEntryResponse> Items,
    long? NextBeforeRevision);

internal sealed record OrderDraftHistoryEntryResponse(
    string Change,
    Guid ChangedByAccountId,
    DateTimeOffset RecordedAt,
    OrderDraftResponse Order);
