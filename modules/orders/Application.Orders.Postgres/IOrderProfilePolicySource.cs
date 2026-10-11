using Npgsql;

namespace Application.Orders.Postgres;

// The host composes profile-owned SQL with the caller's exact Orders transaction.
public interface IOrderProfilePolicySource
{
    Task<OrderProgramPolicyFacts?> ResolveActiveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, CancellationToken cancellationToken);
    Task<OrderProgramPolicyFacts?> ResolveLegacyBaselineAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, CancellationToken cancellationToken);
    Task<bool> IsRetainedCompatibleAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, OrderProgramPolicyFacts policy, CancellationToken cancellationToken);
}
