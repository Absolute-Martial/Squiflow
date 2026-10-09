using Application.Invoices;
using Application.Invoices.Postgres;
using Application.Tenancy;
using Application.Tenancy.Postgres;
using Npgsql;

namespace Application.CoreApi.Authorization;

internal sealed class InvoiceAuthority(
    ITenantMembershipDirectory memberships,
    ITenantAuthorizationAdministrationStore administration,
    ITenantInvoiceAuthorization authorization) : IInvoiceIssueAuthority, IInvoiceViewAuthority, IInvoiceIssueTransactionalAuthority
{
    Task<InvoiceIssueAuthorityStatus> IInvoiceIssueAuthority.CheckAsync(TenantContext context, CancellationToken cancellationToken) =>
        CheckAsync(context, TenantInvoicePermission.Issue, () => ObserveAsync(context, cancellationToken), cancellationToken);

    Task<InvoiceIssueAuthorityStatus> IInvoiceViewAuthority.CheckAsync(TenantContext context, CancellationToken cancellationToken) =>
        CheckAsync(context, TenantInvoicePermission.View, () => ObserveAsync(context, cancellationToken), cancellationToken);

    public Task<InvoiceIssueAuthorityStatus> CheckAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, CancellationToken cancellationToken) =>
        CheckAsync(context, TenantInvoicePermission.Issue,
            () => ObserveAsync(connection, transaction, context, cancellationToken), cancellationToken);

    public Task<InvoiceIssueAuthorityStatus> CheckViewAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, CancellationToken cancellationToken) =>
        CheckAsync(context, TenantInvoicePermission.View,
            () => ObserveAsync(connection, transaction, context, cancellationToken), cancellationToken);

    private async Task<Observation> ObserveAsync(TenantContext context, CancellationToken cancellationToken) => new(
        await administration.GetAuthorizationRevisionAsync(context.TenantId, cancellationToken).ConfigureAwait(false) ?? 1,
        await memberships.IsActiveAsync(context.AccountId, context.TenantId, cancellationToken).ConfigureAwait(false));

    private static async Task<Observation> ObserveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantContext context, CancellationToken cancellationToken)
    {
        var result = await TenantAuthorityObservationReader.ReadAsync(connection, transaction, context, cancellationToken)
            .ConfigureAwait(false);
        return new(result.AuthorizationRevision, result.IsActive);
    }

    private async Task<InvoiceIssueAuthorityStatus> CheckAsync(TenantContext context, TenantInvoicePermission permission,
        Func<Task<Observation>> observe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var before = await observe().ConfigureAwait(false);
            if (!before.IsActive) return InvoiceIssueAuthorityStatus.Denied;
            var allowed = await authorization.IsAllowedAsync(context.AccountId, context.TenantId, permission, cancellationToken)
                .ConfigureAwait(false);
            if (await observe().ConfigureAwait(false) != before) return InvoiceIssueAuthorityStatus.Unavailable;
            return allowed ? InvoiceIssueAuthorityStatus.Allowed : InvoiceIssueAuthorityStatus.Denied;
        }
        catch (Exception error) when (error is AuthorizationProviderUnavailableException or NpgsqlException or TimeoutException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return InvoiceIssueAuthorityStatus.Unavailable;
        }
    }

    private sealed record Observation(int AuthorizationRevision, bool IsActive);
}
