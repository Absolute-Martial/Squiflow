using Application.Orders;
using Application.Orders.Postgres;
using Application.Profiles;
using Application.Profiles.Postgres;
using Npgsql;

namespace Application.CoreApi.Composition;

internal sealed class OrderProfilePolicySource : IOrderProfilePolicySource
{
    public async Task<OrderProgramPolicyFacts?> ResolveActiveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, CancellationToken cancellationToken) => Facts(await PostgresProfileStore.ResolveActiveForOrderAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false));
    public async Task<OrderProgramPolicyFacts?> ResolveLegacyBaselineAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, CancellationToken cancellationToken) => Facts(await PostgresProfileStore.ResolveLegacyBaselineForOrderAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false));
    public async Task<bool> IsRetainedCompatibleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, OrderProgramPolicyFacts policy, CancellationToken cancellationToken) =>
        Facts(await PostgresProfileStore.ResolveRetainedForOrderAsync(connection, transaction, tenantId, policy.ProfileId, cancellationToken).ConfigureAwait(false)) == policy;
    private static OrderProgramPolicyFacts? Facts(TenantProfileSnapshot? snapshot) => snapshot is null ? null :
        new(snapshot.ProfileId, snapshot.Policy.PolicyRevisionId, snapshot.Policy.RequireReferenceForProgramOrders);
}
