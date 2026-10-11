using Application.Orders;
using Application.Tenancy;

namespace Application.CoreApi.Authorization;

internal sealed class OrderApprovalAuthority(
    ITenantMembershipDirectory memberships,
    ITenantAuthorizationAdministrationStore administration,
    ITenantOrderApprovalAuthorization approvals,
    ITenantOrderAuthorization orders,
    IHttpContextAccessor httpContext,
    HighRiskActionAdmission highRiskAdmission) : IOrderApprovalAuthority
{
    public async Task<Application.Orders.OrderApprovalAuthority> ResolveAsync(TenantContext context, CancellationToken cancellationToken)
    {
        var revision = await administration.GetAuthorizationRevisionAsync(context.TenantId, cancellationToken).ConfigureAwait(false) ?? 1;
        if (!await memberships.IsActiveAsync(context.AccountId, context.TenantId, cancellationToken).ConfigureAwait(false))
            return new(revision, false, false, false, false, false, false);

        var mayView = await CheckAsync(context, TenantOrderApprovalPermission.View, cancellationToken).ConfigureAwait(false);
        var mayViewOrder = await orders.CanViewAsync(context.AccountId, context.TenantId, cancellationToken).ConfigureAwait(false);
        var maySubmit = await CheckAsync(context, TenantOrderApprovalPermission.Submit, cancellationToken).ConfigureAwait(false);
        var mayDecide = await CheckAsync(context, TenantOrderApprovalPermission.Decide, cancellationToken).ConfigureAwait(false);
        var mayRecover = await CheckAsync(context, TenantOrderApprovalPermission.Recover, cancellationToken).ConfigureAwait(false);
        var isOwner = mayRecover && await administration.IsInitialOwnerAsync(context.TenantId, context.AccountId, cancellationToken).ConfigureAwait(false);
        var principal = httpContext.HttpContext?.User;
        var recent = principal is not null && highRiskAdmission.Require(principal, HighRiskAction.RecoverOrderApproval) is null;
        var after = await administration.GetAuthorizationRevisionAsync(context.TenantId, cancellationToken).ConfigureAwait(false) ?? 1;
        if (after != revision)
            throw new AuthorizationProviderUnavailableException("Approval authority changed during verification.", new InvalidOperationException("Authorization observation changed."));
        return new(revision, maySubmit, mayView, mayDecide, mayRecover, isOwner, recent, mayViewOrder);
    }

    public async Task<bool> IsEligibleApproverAsync(TenantContext context, Guid accountId, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || !await memberships.IsActiveAsync(accountId, context.TenantId, cancellationToken).ConfigureAwait(false)) return false;
        return await approvals.IsAllowedAsync(accountId, context.TenantId, TenantOrderApprovalPermission.View, cancellationToken).ConfigureAwait(false) &&
            await approvals.IsAllowedAsync(accountId, context.TenantId, TenantOrderApprovalPermission.Decide, cancellationToken).ConfigureAwait(false) &&
            await orders.CanViewAsync(accountId, context.TenantId, cancellationToken).ConfigureAwait(false);
    }

    private Task<bool> CheckAsync(TenantContext context, TenantOrderApprovalPermission permission, CancellationToken cancellationToken) =>
        approvals.IsAllowedAsync(context.AccountId, context.TenantId, permission, cancellationToken);
}
