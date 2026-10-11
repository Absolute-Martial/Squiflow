using Application.Purchasing;
using Application.Purchasing.Postgres;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using Npgsql;

namespace Application.CoreApi.Authorization;

internal sealed class PurchasingAuthority(
    ITenantMembershipDirectory memberships,
    ITenantAuthorizationAdministrationStore administration,
    ITenantPurchasingAuthorization purchasing,
    IHttpContextAccessor httpContext,
    HighRiskActionAdmission highRiskAdmission) : IPurchasingAuthorityReader, IPurchasingTransactionalAuthorityReader
{
    public async Task<Application.Purchasing.PurchasingAuthority> ResolveAsync(TenantContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var before = await ObserveAsync(context, cancellationToken).ConfigureAwait(false);
        return await ResolveObservedAsync(context, before, () => ObserveAsync(context, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Application.Purchasing.PurchasingAuthority> ResolveAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, TenantContext context, CancellationToken cancellationToken)
    {
        var before = await TenantAuthorityObservationReader.ReadAsync(connection, transaction, context, cancellationToken)
            .ConfigureAwait(false);
        return await ResolveObservedAsync(context,
            new(before.AuthorizationRevision, before.IsActive, before.IsInitialOwner),
            async () =>
            {
                var after = await TenantAuthorityObservationReader.ReadAsync(connection, transaction, context, cancellationToken)
                    .ConfigureAwait(false);
                return new Observation(after.AuthorizationRevision, after.IsActive, after.IsInitialOwner);
            }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Observation> ObserveAsync(TenantContext context, CancellationToken cancellationToken) => new(
        await administration.GetAuthorizationRevisionAsync(context.TenantId, cancellationToken).ConfigureAwait(false) ?? 1,
        await memberships.IsActiveAsync(context.AccountId, context.TenantId, cancellationToken).ConfigureAwait(false),
        await administration.IsInitialOwnerAsync(context.TenantId, context.AccountId, cancellationToken).ConfigureAwait(false));

    private async Task<Application.Purchasing.PurchasingAuthority> ResolveObservedAsync(TenantContext context,
        Observation before, Func<Task<Observation>> observeAfter, CancellationToken cancellationToken)
    {
        var canView = false;
        var canManageSupplier = false;
        var canRecordRequest = false;
        var canRecordReceipt = false;
        var canRecordPayment = false;
        var canCorrect = false;
        if (before.IsActive)
        {
            canView = await purchasing.IsAllowedAsync(context.AccountId, context.TenantId,
                TenantPurchasingPermission.View, cancellationToken).ConfigureAwait(false);
            canManageSupplier = await purchasing.IsAllowedAsync(context.AccountId, context.TenantId,
                TenantPurchasingPermission.ManageSupplier, cancellationToken).ConfigureAwait(false);
            canRecordRequest = await purchasing.IsAllowedAsync(context.AccountId, context.TenantId,
                TenantPurchasingPermission.RecordRequest, cancellationToken).ConfigureAwait(false);
            canRecordReceipt = await purchasing.IsAllowedAsync(context.AccountId, context.TenantId,
                TenantPurchasingPermission.RecordReceipt, cancellationToken).ConfigureAwait(false);
            canRecordPayment = await purchasing.IsAllowedAsync(context.AccountId, context.TenantId,
                TenantPurchasingPermission.RecordPayment, cancellationToken).ConfigureAwait(false);
            canCorrect = await purchasing.IsAllowedAsync(context.AccountId, context.TenantId,
                TenantPurchasingPermission.Correct, cancellationToken).ConfigureAwait(false);
        }
        var isInitialOwner = canCorrect && before.IsInitialOwner;
        var principal = httpContext.HttpContext?.User;
        var hasRecentAuthentication = isInitialOwner && principal is not null &&
            highRiskAdmission.Require(principal, HighRiskAction.CorrectPurchasing) is null;
        if (await observeAfter().ConfigureAwait(false) != before)
            throw new AuthorizationProviderUnavailableException("Purchasing authority changed during verification.",
                new InvalidOperationException("Authorization observation changed."));
        return new(before.AuthorizationRevision, canView, canManageSupplier, canRecordRequest, canRecordReceipt,
            canRecordPayment, canCorrect, isInitialOwner, hasRecentAuthentication);
    }

    private sealed record Observation(long AuthorizationRevision, bool IsActive, bool IsInitialOwner);
}
