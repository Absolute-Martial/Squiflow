using Application.Tenancy;

namespace Application.Customers;

public sealed record CustomerImportAuthoritySnapshot(TenantContext Context, long AuthorizationRevision);
public sealed class CustomerImportAuthorityException() : InvalidOperationException("Current customer import authority is unavailable or denied.");
public sealed class CustomerImportClaimLostException() : InvalidOperationException("Import claim is no longer current.");

// The host adapter must check active account, active membership, active tenant and current
// Customers.Import permission. No token/saved acceptance alone grants future worker authority.
public interface ICustomerImportAuthority
{
    Task<CustomerImportAuthoritySnapshot?> CheckAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken);
}

public sealed record CustomerImportClaim(Guid TenantId, Guid WorkId, Guid ImportId, Guid AccountId,
    long AuthorizationRevision, Guid WorkerId, long Generation, DateTimeOffset LeaseExpiresAt);
public enum CustomerImportBatchStatus { Idle = 1, Progress = 2, Completed = 3, LostClaim = 4, AuthorityDenied = 5 }
public sealed record CustomerImportBatchResult(CustomerImportBatchStatus Status, Guid? WorkId, int RowsProcessed);
public enum CustomerImportRowExecutionStatus { Processed = 1, Complete = 2, WaitingForRetry = 3 }

public interface ICustomerImportWorkStore
{
    Task<CustomerImportClaim?> ClaimImportAsync(Guid tenantId, Guid workerId, TimeSpan lease, CancellationToken cancellationToken);
    Task<CustomerImportRowExecutionStatus> ProcessNextImportRowAsync(CustomerImportClaim claim, TenantContext currentContext, CancellationToken cancellationToken);
    Task ReleaseImportClaimAsync(CustomerImportClaim claim, bool authorityDenied, CancellationToken cancellationToken);
}

public sealed class RunCustomerImportBatch(ICustomerImportWorkStore store, ICustomerImportAuthority authority)
{
    public const int MaximumBatchRows = 50;
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(2);

    public async Task<CustomerImportBatchResult> ExecuteAsync(Guid tenantId, Guid workerId, int limit, CancellationToken drainToken)
    {
        CustomerRules.RequireIdentity(tenantId, "tenant_id_invalid");
        CustomerRules.RequireIdentity(workerId, "worker_id_invalid");
        CustomerRules.RequirePageSize(limit);
        drainToken.ThrowIfCancellationRequested();
        var claim = await store.ClaimImportAsync(tenantId, workerId, ClaimLease, drainToken).ConfigureAwait(false);
        if (claim is null) return new(CustomerImportBatchStatus.Idle, null, 0);
        var count = 0;
        var denied = false;
        try
        {
            while (count < limit)
            {
                drainToken.ThrowIfCancellationRequested();
                var current = await authority.CheckAsync(claim.TenantId, claim.AccountId, drainToken).ConfigureAwait(false);
                if (current is null || current.AuthorizationRevision != claim.AuthorizationRevision
                    || current.Context.AccountId != claim.AccountId || current.Context.TenantId != claim.TenantId)
                {
                    denied = true;
                    return new(CustomerImportBatchStatus.AuthorityDenied, claim.WorkId, count);
                }
                var result = await store.ProcessNextImportRowAsync(claim, current.Context, drainToken).ConfigureAwait(false);
                if (result == CustomerImportRowExecutionStatus.Complete) return new(CustomerImportBatchStatus.Completed, claim.WorkId, count);
                if (result == CustomerImportRowExecutionStatus.WaitingForRetry) return new(CustomerImportBatchStatus.Progress, claim.WorkId, count);
                count++;
            }
            return new(CustomerImportBatchStatus.Progress, claim.WorkId, count);
        }
        catch (CustomerImportClaimLostException)
        {
            return new(CustomerImportBatchStatus.LostClaim, claim.WorkId, count);
        }
        finally
        {
            // A short cleanup budget releases the claim on graceful drain. A killed process
            // needs no callback: the persisted lease expires and another generation recovers it.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await store.ReleaseImportClaimAsync(claim, denied, cleanup.Token).ConfigureAwait(false);
        }
    }
}
