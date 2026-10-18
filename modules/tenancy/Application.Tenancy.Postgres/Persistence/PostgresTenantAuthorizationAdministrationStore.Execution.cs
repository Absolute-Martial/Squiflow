using Npgsql;

namespace Application.Tenancy.Postgres;

public sealed partial class PostgresTenantAuthorizationAdministrationStore
{
    public async ValueTask<ITenantAuthorizationReconciliationSession> OpenAsync(
        Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        using var acquisition = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        acquisition.CancelAfter(TimeSpan.FromSeconds(10));
        var connection = await dataSource.OpenConnectionAsync(acquisition.Token).ConfigureAwait(false);
        try
        {
            await using var command = new NpgsqlCommand(TenantAuthorizationAdministrationSql.LockAuthorizationExecution, connection);
            command.CommandTimeout = 10;
            command.Parameters.AddWithValue("tenant_id", tenantId);
            await command.ExecuteNonQueryAsync(acquisition.Token).ConfigureAwait(false);
            return new ReconciliationSession(connection, tenantId);
        }
        catch
        {
            // An interrupted lock acquisition must not return a possibly locked session to the pool.
            NpgsqlConnection.ClearPool(connection);
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class ReconciliationSession(NpgsqlConnection connection, Guid tenantId)
        : ITenantAuthorizationReconciliationSession
    {
        private Guid? dispatchedProposal;
        private bool freshDispatch;
        internal NpgsqlConnection Connection => connection;

        public async Task<TenantAuthorizationProposal?> FindAsync(Guid proposalId, CancellationToken cancellationToken)
        {
            ValidateIds(tenantId, proposalId);
            await using var command = new NpgsqlCommand(TenantAuthorizationAdministrationSql.FindAuthorizationProposal, connection);
            command.Parameters.AddWithValue("tenant_id", tenantId);
            command.Parameters.AddWithValue("proposal_id", proposalId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadProposal(reader) : null;
        }

        public async Task<bool> IsInitialOwnerAsync(Guid accountId, CancellationToken cancellationToken)
        {
            ValidateIds(tenantId, accountId);
            await using var command = new NpgsqlCommand(TenantAuthorizationAdministrationSql.IsInitialOwner, connection);
            command.Parameters.AddWithValue("tenant_id", tenantId);
            command.Parameters.AddWithValue("account_id", accountId);
            return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
        }

        public async Task<IReadOnlyList<TenantRoleAssignment>> ListRoleAssignmentsAsync(Guid roleId, CancellationToken cancellationToken)
        {
            ValidateIds(tenantId, roleId);
            await using var command = new NpgsqlCommand(TenantAuthorizationAdministrationSql.ListCustomRoleAssignments, connection);
            command.Parameters.AddWithValue("tenant_id", tenantId);
            command.Parameters.AddWithValue("role_id", roleId);
            var result = new List<TenantRoleAssignment>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
                    (TenantRoleAssignmentAvailability)reader.GetInt16(3), reader.GetInt32(4),
                    reader.GetFieldValue<DateTimeOffset>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)));
            return result;
        }

        public Task<TenantAuthorizationProposal?> RejectUnattemptedAsync(Guid proposalId, DateTimeOffset occurredAt,
            CancellationToken cancellationToken) => UpdateProposalStatusAsync(connection, tenantId, proposalId,
                TenantAuthorizationProposalStatus.Failed, "delegator_authority_revoked", occurredAt, false, false, cancellationToken);

        public async Task<TenantAuthorizationProposal> BeginProviderCallAsync(Guid proposalId, DateTimeOffset occurredAt,
            CancellationToken cancellationToken)
        {
            var proposal = await FindAsync(proposalId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The authorization proposal is missing.");
            if (dispatchedProposal is not null || !TenantAuthorizationExecution.CanDispatch(proposal))
                throw new InvalidOperationException("The authorization proposal cannot dispatch a provider call.");
            freshDispatch = proposal.AttemptCount == 0 && proposal.Status == TenantAuthorizationProposalStatus.Pending;
            var begun = await UpdateProposalStatusAsync(connection, tenantId, proposalId,
                TenantAuthorizationProposalStatus.Uncertain, TenantAuthorizationExecution.InFlight,
                occurredAt, true, true, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The authorization proposal is missing.");
            dispatchedProposal = proposalId;
            return begun;
        }

        public async Task<TenantAuthorizationProposal> RecordProviderOutcomeAsync(Guid proposalId,
            TenantAuthorizationProviderEffect effect, string? failureCode, DateTimeOffset occurredAt,
            CancellationToken cancellationToken)
        {
            if (dispatchedProposal != proposalId || !Enum.IsDefined(effect))
                throw new InvalidOperationException("No provider call is owned by this session.");
            var code = effect switch
            {
                TenantAuthorizationProviderEffect.Observed => TenantAuthorizationExecution.Observed,
                TenantAuthorizationProviderEffect.FinishedWithoutObservation => TenantAuthorizationExecution.FinishedPrefix + failureCode,
                TenantAuthorizationProviderEffect.RejectedWithoutEffect when freshDispatch => failureCode ?? "provider_rejected",
                _ => TenantAuthorizationExecution.UnresolvedPrefix + (failureCode ?? "provider_outcome_unknown"),
            };
            var status = effect == TenantAuthorizationProviderEffect.RejectedWithoutEffect && freshDispatch
                ? TenantAuthorizationProposalStatus.Failed : TenantAuthorizationProposalStatus.Uncertain;
            var result = await UpdateProposalStatusAsync(connection, tenantId, proposalId, status,
                NormalizeFailureCode(code), occurredAt, false, true, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The authorization proposal is missing.");
            dispatchedProposal = null;
            return result;
        }

        public Task<TenantAuthorizationProposal?> CompleteAsync(Guid proposalId, DateTimeOffset occurredAt,
            CancellationToken cancellationToken) =>
            PostgresTenantAuthorizationAdministrationStore.CompleteAsync(connection, tenantId, proposalId, occurredAt, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await using var command = new NpgsqlCommand(TenantAuthorizationAdministrationSql.UnlockAuthorizationExecution, connection);
                command.CommandTimeout = 5;
                command.Parameters.AddWithValue("tenant_id", tenantId);
                if (await command.ExecuteScalarAsync(cleanup.Token).ConfigureAwait(false) is not true)
                    NpgsqlConnection.ClearPool(connection);
            }
            catch (Exception error) when (error is NpgsqlException or OperationCanceledException or InvalidOperationException)
            {
                NpgsqlConnection.ClearPool(connection);
            }
            finally { await connection.DisposeAsync().ConfigureAwait(false); }
        }
    }
}
