using Application.Invoices;
using Application.Invoices.Postgres;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using Npgsql;
using FinancialAuthorityResult = Application.Invoices.FinancialAuthority;

namespace Application.CoreApi.Authorization;

internal sealed class FinancialAuthority(ITenantMembershipDirectory memberships,
    ITenantAuthorizationAdministrationStore administration, ITenantFinancialAuthorization authorization,
    IHttpContextAccessor http, HighRiskActionAdmission highRisk) : IFinancialAuthorityReader, IFinancialTransactionalAuthorityReader
{
    public Task<FinancialAuthorityResult> CheckAsync(TenantContext context, FinancialCapability permission, CancellationToken cancellationToken) =>
        CheckAsync(context, permission, () => ObserveAsync(context, cancellationToken), cancellationToken);

    public Task<FinancialAuthorityResult> CheckAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, FinancialCapability permission, CancellationToken cancellationToken) =>
        CheckAsync(context, permission, async () =>
        {
            var value = await TenantAuthorityObservationReader.ReadAsync(connection, transaction, context, cancellationToken).ConfigureAwait(false);
            return new(value.AuthorizationRevision, value.IsActive, value.IsInitialOwner);
        }, cancellationToken);

    private async Task<Observation> ObserveAsync(TenantContext context, CancellationToken ct) => new(
        await administration.GetAuthorizationRevisionAsync(context.TenantId, ct).ConfigureAwait(false) ?? 1,
        await memberships.IsActiveAsync(context.AccountId, context.TenantId, ct).ConfigureAwait(false),
        await administration.IsInitialOwnerAsync(context.TenantId, context.AccountId, ct).ConfigureAwait(false));

    private async Task<FinancialAuthorityResult> CheckAsync(TenantContext context, FinancialCapability permission,
        Func<Task<Observation>> observe, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var before = await observe().ConfigureAwait(false);
            if (!before.IsActive || permission == FinancialCapability.Correct && !before.IsInitialOwner)
                return new(FinancialAccess.Denied);
            var allowed = await authorization.IsAllowedAsync(context.AccountId, context.TenantId, permission, ct).ConfigureAwait(false);
            if (!allowed) return new(FinancialAccess.Denied);
            if (permission == FinancialCapability.Correct)
            {
                var principal = http.HttpContext?.User;
                if (principal is null) return new(FinancialAccess.Denied);
                var admission = highRisk.Require(principal, HighRiskAction.CorrectCustomerSettlement);
                if (admission is not null)
                    return new(admission is IStatusCodeHttpResult { StatusCode: >= 500 } ? FinancialAccess.Unavailable : FinancialAccess.Denied);
            }
            if (await observe().ConfigureAwait(false) != before) return new(FinancialAccess.Unavailable);
            return new(FinancialAccess.Allowed, before.Revision);
        }
        catch (Exception error) when (error is AuthorizationProviderUnavailableException or NpgsqlException or TimeoutException)
        {
            ct.ThrowIfCancellationRequested();
            return new(FinancialAccess.Unavailable);
        }
    }

    private sealed record Observation(long Revision, bool IsActive, bool IsInitialOwner);
}
