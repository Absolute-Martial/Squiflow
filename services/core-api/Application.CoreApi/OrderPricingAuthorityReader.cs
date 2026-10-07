using Application.CoreApi.Authorization;
using Application.Orders;
using Application.Tenancy;

namespace Application.CoreApi;

internal sealed class OrderPricingAuthorityReader(ITenantPricingAuthorization permissions) : IOrderPricingAuthorityReader
{
    public async Task<OrderPricingAuthority> ReadAsync(TenantContext context, CancellationToken ct)
    {
        var canOverride = await permissions.CanOverrideAsync(context.AccountId, context.TenantId, ct).ConfigureAwait(false);
        return new(canOverride, false);
    }

    public Task<bool> CanOverrideBeyondPolicyAsync(TenantContext context, CancellationToken ct) =>
        permissions.CanOverrideBeyondPolicyAsync(context.AccountId, context.TenantId, ct);
}
