using Application.Fulfillment;
using Application.Fulfillment.Postgres;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using Npgsql;

namespace Application.CoreApi.Authorization;

internal sealed class FulfillmentAuthority(
    ITenantMembershipDirectory memberships,
    ITenantAuthorizationAdministrationStore administration,
    ITenantOrderFulfillmentAuthorization fulfillment,
    IHttpContextAccessor httpContext,
    HighRiskActionAdmission highRiskAdmission) : IFulfillmentAuthorityReader, IFulfillmentTransactionalAuthorityReader
{
    public async Task<Application.Fulfillment.FulfillmentAuthority> ResolveAsync(
        TenantContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var before = await ObserveAsync(context, cancellationToken).ConfigureAwait(false);
        return await ResolveObservedAsync(context, before,
            () => ObserveAsync(context, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Application.Fulfillment.FulfillmentAuthority> ResolveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(context);

        var observed = await TenantAuthorityObservationReader.ReadAsync(connection, transaction, context, cancellationToken)
            .ConfigureAwait(false);
        var before = new Observation(observed.AuthorizationRevision, observed.IsActive, observed.IsInitialOwner);
        return await ResolveObservedAsync(context, before, async () =>
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

    private async Task<Application.Fulfillment.FulfillmentAuthority> ResolveObservedAsync(
        TenantContext context,
        Observation before,
        Func<Task<Observation>> observeAfter,
        CancellationToken cancellationToken)
    {
        var canView = false;
        var canRecordWork = false;
        var canRecordHandoff = false;
        var canCorrect = false;
        if (before.IsActive)
        {
            canView = await fulfillment.IsAllowedAsync(
                context.AccountId, context.TenantId, TenantOrderFulfillmentPermission.View, cancellationToken)
                .ConfigureAwait(false);
            canRecordWork = await fulfillment.IsAllowedAsync(
                context.AccountId, context.TenantId, TenantOrderFulfillmentPermission.RecordWork, cancellationToken)
                .ConfigureAwait(false);
            canRecordHandoff = await fulfillment.IsAllowedAsync(
                context.AccountId, context.TenantId, TenantOrderFulfillmentPermission.RecordHandoff, cancellationToken)
                .ConfigureAwait(false);
            canCorrect = await fulfillment.IsAllowedAsync(
                context.AccountId, context.TenantId, TenantOrderFulfillmentPermission.Correct, cancellationToken)
                .ConfigureAwait(false);
        }

        var isInitialOwner = canCorrect && before.IsInitialOwner;
        var principal = httpContext.HttpContext?.User;
        var hasRecentAuthentication = isInitialOwner && principal is not null &&
            highRiskAdmission.Require(principal, HighRiskAction.CorrectOrderFulfillment) is null;
        if (await observeAfter().ConfigureAwait(false) != before)
        {
            throw new AuthorizationProviderUnavailableException(
                "Fulfillment authority changed during verification.",
                new InvalidOperationException("Authorization observation changed."));
        }

        return new Application.Fulfillment.FulfillmentAuthority(
            before.AuthorizationRevision,
            canView,
            canRecordWork,
            canRecordHandoff,
            canCorrect,
            isInitialOwner,
            hasRecentAuthentication);
    }

    private sealed record Observation(int AuthorizationRevision, bool IsActive, bool IsInitialOwner);
}
