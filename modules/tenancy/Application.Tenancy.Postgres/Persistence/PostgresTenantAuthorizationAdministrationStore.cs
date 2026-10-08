using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace Application.Tenancy.Postgres;

public sealed class PostgresTenantAuthorizationAdministrationStore(NpgsqlDataSource dataSource)
    : ITenantAuthorizationAdministrationStore
{
    /// <summary>Reconciliation attempts allowed before an unresolved proposal is terminally failed.</summary>
    private const int MaximumReconciliationAttempts = 20;

    public async Task<int?> GetAuthorizationRevisionAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        await using var command = dataSource.CreateCommand(TenantAuthorizationAdministrationSql.GetAuthorizationRevision);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<bool> IsInitialOwnerAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        if (accountId == Guid.Empty) throw new ArgumentException("Account identity cannot be empty.", nameof(accountId));
        await using var command = dataSource.CreateCommand(TenantAuthorizationAdministrationSql.IsInitialOwner);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    public async Task<TenantAuthorizationProposalResult> ProposeAsync(
        TenantAuthorizationActor actor,
        TenantAuthorizationProposalIntent intent,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        const int maximumTransactionAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await ProposeTransactionAsync(actor, intent, requestedAt, cancellationToken).ConfigureAwait(false);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
            {
                // PostgreSQL has aborted the entire transaction; no provider mutation occurs here.
                if (attempt == maximumTransactionAttempts)
                    return new TenantAuthorizationProposalResult(TenantAuthorizationProposalResultStatus.AuthorizationRevisionConflict, null);
            }
        }
    }

    private async Task<TenantAuthorizationProposalResult> ProposeTransactionAsync(
        TenantAuthorizationActor actor,
        TenantAuthorizationProposalIntent intent,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);

        var replay = await FindByIdempotencyAsync(
            connection, transaction, intent.TenantId, actor.AccountId, intent.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (replay is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return string.Equals(replay.RequestFingerprint, intent.Fingerprint, StringComparison.Ordinal)
                ? new TenantAuthorizationProposalResult(TenantAuthorizationProposalResultStatus.Replayed, replay)
                : new TenantAuthorizationProposalResult(TenantAuthorizationProposalResultStatus.IdempotencyKeyConflict, null);
        }

        var tenant = await LockTenantAsync(connection, transaction, intent.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenantAuthorizationProposalResult(TenantAuthorizationProposalResultStatus.TenantNotFound, null);
        }
        if (tenant.Value.Availability != TenantAvailability.Active)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenantAuthorizationProposalResult(TenantAuthorizationProposalResultStatus.TenantUnavailable, null);
        }
        if (!await IsActiveInitialOwnerAsync(connection, transaction, intent.TenantId, actor.AccountId, cancellationToken)
                .ConfigureAwait(false))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenantAuthorizationProposalResult(TenantAuthorizationProposalResultStatus.ActorNotInitialOwner, null);
        }

        var authorizationRevision = await LockAuthorizationStateAsync(
            connection, transaction, intent.TenantId, requestedAt, cancellationToken).ConfigureAwait(false);
        if (authorizationRevision != intent.ExpectedAuthorizationRevision)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenantAuthorizationProposalResult(TenantAuthorizationProposalResultStatus.AuthorizationRevisionConflict, null);
        }

        if (await HasActiveProposalAsync(connection, transaction, intent.TenantId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenantAuthorizationProposalResult(TenantAuthorizationProposalResultStatus.AuthorizationRevisionConflict, null);
        }

        var role = intent.RoleId is { } roleId
            ? await FindRoleForUpdateAsync(connection, transaction, intent.TenantId, roleId, cancellationToken).ConfigureAwait(false)
            : null;

        var validation = await ValidateProposalAsync(connection, transaction, intent, role, cancellationToken)
            .ConfigureAwait(false);
        if (validation is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenantAuthorizationProposalResult(validation.Value, null);
        }

        var proposalId = Guid.CreateVersion7();
        var appliedPermissions = role?.PermissionIds.ToArray() ?? [];
        await using (var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.InsertAuthorizationProposal))
        {
            command.Parameters.AddWithValue("proposal_id", proposalId);
            command.Parameters.AddWithValue("tenant_id", intent.TenantId);
            command.Parameters.AddWithValue("actor_account_id", actor.AccountId);
            command.Parameters.AddWithValue("idempotency_key", intent.IdempotencyKey);
            command.Parameters.AddWithValue("fingerprint", intent.Fingerprint);
            command.Parameters.AddWithValue("kind", (short)intent.Kind);
            command.Parameters.AddWithValue("expected_revision", intent.ExpectedAuthorizationRevision);
            AddNullableGuid(command, "target_account_id", intent.TargetAccountId);
            AddNullableText(command, "permission_id", intent.PermissionId);
            AddNullableGuid(command, "role_id", intent.RoleId);
            AddNullableInt(command, "expected_role_revision", intent.ExpectedRoleRevision);
            AddNullableText(command, "role_name", intent.RoleName);
            command.Parameters.Add(new NpgsqlParameter<string[]>("requested_permissions", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                TypedValue = intent.RequestedPermissions.ToArray(),
            });
            command.Parameters.Add(new NpgsqlParameter<string[]>("applied_permissions", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                TypedValue = appliedPermissions,
            });
            command.Parameters.AddWithValue("requested_at", requestedAt);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await AppendEventAsync(connection, transaction, proposalId, intent.TenantId, actor.AccountId,
            TenantAuthorizationProposalStatus.Pending, "proposal_created", authorizationRevision, requestedAt, cancellationToken)
            .ConfigureAwait(false);

        var created = await FindProposalForUpdateAsync(connection, transaction, intent.TenantId, proposalId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The authorization proposal was not retained.");
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new TenantAuthorizationProposalResult(TenantAuthorizationProposalResultStatus.Created, created);
    }

    public async Task<TenantAuthorizationProposal?> FindProposalAsync(
        Guid tenantId,
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        ValidateIds(tenantId, proposalId);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = TenantAuthorizationAdministrationSql.FindAuthorizationProposal;
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("proposal_id", proposalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadProposal(reader) : null;
    }

    public Task<TenantAuthorizationProposal?> MarkAttemptAsync(
        Guid tenantId,
        Guid proposalId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken) =>
        UpdateProposalStatusAsync(tenantId, proposalId, null, null, occurredAt, incrementAttempt: true, cancellationToken);

    public Task<TenantAuthorizationProposal?> MarkUncertainAsync(
        Guid tenantId,
        Guid proposalId,
        string failureCode,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken) =>
        UpdateProposalStatusAsync(
            tenantId, proposalId, TenantAuthorizationProposalStatus.Uncertain,
            NormalizeFailureCode(failureCode), occurredAt, incrementAttempt: false, cancellationToken);

    public Task<TenantAuthorizationProposal?> MarkFailedAsync(
        Guid tenantId,
        Guid proposalId,
        string failureCode,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken) =>
        UpdateProposalStatusAsync(
            tenantId, proposalId, TenantAuthorizationProposalStatus.Failed,
            NormalizeFailureCode(failureCode), occurredAt, incrementAttempt: false, cancellationToken);

    public async Task<TenantAuthorizationProposal?> CompleteAsync(
        Guid tenantId,
        Guid proposalId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        ValidateIds(tenantId, proposalId);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        var proposal = await FindProposalForUpdateAsync(connection, transaction, tenantId, proposalId, cancellationToken)
            .ConfigureAwait(false);
        if (proposal is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        if (proposal.Status == TenantAuthorizationProposalStatus.Applied)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return proposal;
        }
        if (proposal.Status == TenantAuthorizationProposalStatus.Failed)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return proposal;
        }

        var revision = await LockAuthorizationStateAsync(connection, transaction, tenantId, occurredAt, cancellationToken)
            .ConfigureAwait(false);
        if (revision != proposal.ExpectedAuthorizationRevision)
        {
            throw new InvalidOperationException(
                "The provider effect was observed but the local authorization revision no longer matches the durable proposal.");
        }
        if (revision == int.MaxValue)
        {
            throw new InvalidOperationException("Tenant authorization revision limit reached.");
        }

        await ApplyLocalCompletionAsync(connection, transaction, proposal, occurredAt, cancellationToken).ConfigureAwait(false);
        var appliedRevision = checked(revision + 1);
        await using (var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.CompleteAuthorizationProposal))
        {
            command.Parameters.AddWithValue("tenant_id", tenantId);
            command.Parameters.AddWithValue("proposal_id", proposalId);
            command.Parameters.AddWithValue("revision", appliedRevision);
            command.Parameters.AddWithValue("occurred_at", occurredAt);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await AppendEventAsync(connection, transaction, proposalId, tenantId, proposal.RequestedByAccountId,
            TenantAuthorizationProposalStatus.Applied, "provider_state_observed", appliedRevision, occurredAt, cancellationToken)
            .ConfigureAwait(false);
        var completed = await FindProposalForUpdateAsync(connection, transaction, tenantId, proposalId, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return completed;
    }

    public async Task<IReadOnlyList<TenantCustomRole>> ListRolesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        await using var command = dataSource.CreateCommand(TenantAuthorizationAdministrationSql.ListCustomRoles);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<TenantCustomRole>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(ReadRole(reader));
        return result;
    }

    public async Task<TenantCustomRole?> FindRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        ValidateIds(tenantId, roleId);
        await using var command = dataSource.CreateCommand(TenantAuthorizationAdministrationSql.FindCustomRole);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("role_id", roleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRole(reader) : null;
    }

    public async Task<IReadOnlyList<TenantRoleAssignment>> ListRoleAssignmentsAsync(
        Guid tenantId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        ValidateIds(tenantId, roleId);
        await using var command = dataSource.CreateCommand(TenantAuthorizationAdministrationSql.ListCustomRoleAssignments);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("role_id", roleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<TenantRoleAssignment>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new TenantRoleAssignment(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
                (TenantRoleAssignmentAvailability)reader.GetInt16(3), reader.GetInt32(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)));
        }
        return result;
    }

    public async Task<TenantOwnerTransferResult> TransferInitialOwnerAsync(
        TenantAuthorizationActor actor,
        TenantOwnerTransferIntent intent,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intent);
        const int maximumTransactionAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await TransferInitialOwnerTransactionAsync(actor, intent, occurredAt, cancellationToken).ConfigureAwait(false);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
            {
                // This database-only handoff transaction was fully aborted; retry preserves the original receipt intent.
                if (attempt == maximumTransactionAttempts)
                    return new(TenantOwnerTransferStatus.TenantRevisionConflict, null, null, null, null);
            }
        }
    }

    private async Task<TenantOwnerTransferResult> TransferInitialOwnerTransactionAsync(
        TenantAuthorizationActor actor, TenantOwnerTransferIntent intent, DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);

        var replay = await FindOwnerTransferReceiptAsync(
            connection, transaction, intent.TenantId, actor.AccountId, intent.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (replay is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(replay.Value.Fingerprint, intent.Fingerprint, StringComparison.Ordinal))
                return new TenantOwnerTransferResult(TenantOwnerTransferStatus.IdempotencyKeyConflict, null, null, null, null);
            return new TenantOwnerTransferResult(
                TenantOwnerTransferStatus.Replayed,
                replay.Value.PreviousOwner,
                replay.Value.CurrentOwner,
                replay.Value.TenantRevision,
                replay.Value.AuthorizationRevision);
        }

        var tenant = await LockTenantAsync(connection, transaction, intent.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenantOwnerTransferResult(TenantOwnerTransferStatus.TenantNotFound, null, null, null, null);
        }
        if (tenant.Value.Revision != intent.ExpectedTenantRevision)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenantOwnerTransferResult(TenantOwnerTransferStatus.TenantRevisionConflict, null, null, tenant.Value.Revision, null);
        }
        if (tenant.Value.Revision == int.MaxValue)
            throw new InvalidOperationException("Tenant revision limit reached.");

        var owner = await FindInitialOwnerForUpdateAsync(connection, transaction, intent.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (owner is null || owner.Value.AccountId != actor.AccountId || owner.Value.Availability != MembershipAvailability.Active)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenantOwnerTransferResult(TenantOwnerTransferStatus.ActorNotInitialOwner, owner?.AccountId, null, tenant.Value.Revision, null);
        }
        if (owner.Value.AccountId == intent.TargetAccountId)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenantOwnerTransferResult(TenantOwnerTransferStatus.TargetAlreadyInitialOwner, owner.Value.AccountId, owner.Value.AccountId, tenant.Value.Revision, null);
        }

        var target = await FindMembershipForUpdateAsync(connection, transaction, intent.TenantId, intent.TargetAccountId, cancellationToken)
            .ConfigureAwait(false);
        if (target is null || target.Value.Availability != MembershipAvailability.Active)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenantOwnerTransferResult(TenantOwnerTransferStatus.TargetMembershipUnavailable, owner.Value.AccountId, null, tenant.Value.Revision, null);
        }
        if (owner.Value.Revision == int.MaxValue || target.Value.Revision == int.MaxValue)
            throw new InvalidOperationException("Membership revision limit reached.");

        var authorizationRevision = await LockAuthorizationStateAsync(
            connection, transaction, intent.TenantId, occurredAt, cancellationToken).ConfigureAwait(false);
        if (await HasActiveProposalAsync(connection, transaction, intent.TenantId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new TenantOwnerTransferResult(
                TenantOwnerTransferStatus.AuthorizationChangeInProgress,
                owner.Value.AccountId,
                intent.TargetAccountId,
                tenant.Value.Revision,
                authorizationRevision);
        }
        if (authorizationRevision == int.MaxValue)
            throw new InvalidOperationException("Tenant authorization revision limit reached.");
        var nextTenantRevision = checked(tenant.Value.Revision + 1);
        var nextAuthorizationRevision = checked(authorizationRevision + 1);

        await using (var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.TransferInitialOwner))
        {
            command.Parameters.AddWithValue("tenant_id", intent.TenantId);
            command.Parameters.AddWithValue("old_owner", owner.Value.AccountId);
            command.Parameters.AddWithValue("new_owner", intent.TargetAccountId);
            command.Parameters.AddWithValue("tenant_revision", nextTenantRevision);
            command.Parameters.AddWithValue("authorization_revision", nextAuthorizationRevision);
            command.Parameters.AddWithValue("occurred_at", occurredAt);
            command.Parameters.AddWithValue("actor_account_id", actor.AccountId);
            command.Parameters.AddWithValue("idempotency_key", intent.IdempotencyKey);
            command.Parameters.AddWithValue("fingerprint", intent.Fingerprint);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new TenantOwnerTransferResult(
            TenantOwnerTransferStatus.Transferred,
            owner.Value.AccountId,
            intent.TargetAccountId,
            nextTenantRevision,
            nextAuthorizationRevision);
    }

    private async Task<TenantAuthorizationProposal?> UpdateProposalStatusAsync(
        Guid tenantId,
        Guid proposalId,
        TenantAuthorizationProposalStatus? status,
        string? failureCode,
        DateTimeOffset occurredAt,
        bool incrementAttempt,
        CancellationToken cancellationToken)
    {
        ValidateIds(tenantId, proposalId);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        var proposal = await FindProposalForUpdateAsync(connection, transaction, tenantId, proposalId, cancellationToken)
            .ConfigureAwait(false);
        if (proposal is null || proposal.Status == TenantAuthorizationProposalStatus.Applied || proposal.Status == TenantAuthorizationProposalStatus.Failed)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return proposal;
        }
        // An unresolved provider outcome is not evidence that the change did not apply. A
        // timeout or a failed verification can hide a write that succeeded, so this store must
        // never turn Uncertain into a final Failed: Failed releases the proposal slot, which
        // would let a later proposal change the same authorization against provider state that
        // may already reflect this one, while the original change is never completed.
        //
        // Marking an attempt likewise only records that reconciliation is about to consult the
        // authorization provider, so the budget cannot terminalise the proposal from there
        // either. Once the budget is spent the proposal stays Uncertain, keeps holding its slot
        // and stays reconcilable, and only the automatic attempt counter stops advancing.
        if (proposal.AttemptCount >= MaximumReconciliationAttempts)
        {
            // The budget is spent, so stop consuming automatic attempts. An unresolved
            // outcome still stays Uncertain; a known rejection still becomes Failed with its
            // own code, and a confirmed application still becomes Applied.
            var unresolved = status is null || status == TenantAuthorizationProposalStatus.Uncertain;
            // Record why the outcome is unresolved. Stamp the exhausted budget only when no
            // more specific code is available, otherwise the real cause of a wedged proposal
            // (a provider outage, an unobserved write, a credential that cannot read tuples)
            // is discarded after twenty attempts and the operator is left with only
            // attempt_limit_reached.
            var budgetCode = failureCode is null ? "attempt_limit_reached" : failureCode;
            await SetProposalStatusAsync(connection, transaction, proposal,
                unresolved ? TenantAuthorizationProposalStatus.Uncertain : status!.Value,
                unresolved ? budgetCode : failureCode,
                occurredAt, incrementAttempt: false, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await SetProposalStatusAsync(connection, transaction, proposal,
                status ?? proposal.Status,
                incrementAttempt && failureCode is null ? proposal.FailureCode : failureCode,
                occurredAt, incrementAttempt, cancellationToken)
                .ConfigureAwait(false);
        }
        var updated = await FindProposalForUpdateAsync(connection, transaction, tenantId, proposalId, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private static async Task SetProposalStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantAuthorizationProposal proposal,
        TenantAuthorizationProposalStatus status,
        string? failureCode,
        DateTimeOffset occurredAt,
        bool incrementAttempt,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.UpdateAuthorizationProposalStatus);
        command.Parameters.AddWithValue("status", (short)status);
        command.Parameters.AddWithValue("attempt_increment", incrementAttempt ? 1 : 0);
        AddNullableText(command, "failure_code", failureCode);
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        command.Parameters.AddWithValue("tenant_id", proposal.TenantId);
        command.Parameters.AddWithValue("proposal_id", proposal.ProposalId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await AppendEventAsync(connection, transaction, proposal.ProposalId, proposal.TenantId, proposal.RequestedByAccountId,
            status,
            incrementAttempt ? "reconciliation_attempt" : failureCode ?? "status_changed",
            proposal.ExpectedAuthorizationRevision,
            occurredAt,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TenantAuthorizationProposalResultStatus?> ValidateProposalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantAuthorizationProposalIntent intent,
        TenantCustomRole? role,
        CancellationToken cancellationToken)
    {
        switch (intent.Kind)
        {
            case TenantAuthorizationProposalKind.GrantPermission:
                if (!await IsActiveMembershipAsync(connection, transaction, intent.TenantId, intent.TargetAccountId!.Value, cancellationToken).ConfigureAwait(false))
                    return TenantAuthorizationProposalResultStatus.TargetMembershipUnavailable;
                if (await IsDirectPermissionGrantActiveAsync(
                        connection, transaction, intent.TenantId, intent.TargetAccountId.Value, intent.PermissionId!, cancellationToken)
                    .ConfigureAwait(false))
                    return TenantAuthorizationProposalResultStatus.DirectPermissionConflict;
                break;
            case TenantAuthorizationProposalKind.RevokePermission:
                if (!await IsDirectPermissionGrantActiveAsync(
                        connection, transaction, intent.TenantId, intent.TargetAccountId!.Value, intent.PermissionId!, cancellationToken)
                    .ConfigureAwait(false))
                    return TenantAuthorizationProposalResultStatus.DirectPermissionConflict;
                break;
            case TenantAuthorizationProposalKind.CreateRole:
                if (role is not null) return TenantAuthorizationProposalResultStatus.RoleAlreadyExists;
                if (await CountActiveRolesAsync(connection, transaction, intent.TenantId, cancellationToken).ConfigureAwait(false)
                    >= TenantPermissionCatalog.MaximumCustomRolesPerTenant)
                    return TenantAuthorizationProposalResultStatus.RoleAlreadyExists;
                if (await ActiveRoleNameExistsAsync(connection, transaction, intent.TenantId, intent.RoleName!, cancellationToken).ConfigureAwait(false))
                    return TenantAuthorizationProposalResultStatus.RoleAlreadyExists;
                break;
            case TenantAuthorizationProposalKind.ReviseRole:
                if (role is null) return TenantAuthorizationProposalResultStatus.RoleNotFound;
                if (role.Availability == TenantRoleAvailability.Retired) return TenantAuthorizationProposalResultStatus.RoleRetired;
                if (role.Revision != intent.ExpectedRoleRevision) return TenantAuthorizationProposalResultStatus.RoleRevisionConflict;
                if (await ActiveRoleNameExistsAsync(connection, transaction, intent.TenantId, intent.RoleName!, cancellationToken, intent.RoleId).ConfigureAwait(false))
                    return TenantAuthorizationProposalResultStatus.RoleAlreadyExists;
                break;
            case TenantAuthorizationProposalKind.RetireRole:
                if (role is null) return TenantAuthorizationProposalResultStatus.RoleNotFound;
                if (role.Availability == TenantRoleAvailability.Retired) return TenantAuthorizationProposalResultStatus.RoleRetired;
                if (role.Revision != intent.ExpectedRoleRevision) return TenantAuthorizationProposalResultStatus.RoleRevisionConflict;
                break;
            case TenantAuthorizationProposalKind.AssignRole:
                if (role is null) return TenantAuthorizationProposalResultStatus.RoleNotFound;
                if (role.Availability != TenantRoleAvailability.Active) return TenantAuthorizationProposalResultStatus.RoleRetired;
                if (!await IsActiveMembershipAsync(connection, transaction, intent.TenantId, intent.TargetAccountId!.Value, cancellationToken).ConfigureAwait(false))
                    return TenantAuthorizationProposalResultStatus.TargetMembershipUnavailable;
                if (await CountActiveRoleAssignmentsAsync(connection, transaction, intent.TenantId, intent.RoleId!.Value, cancellationToken).ConfigureAwait(false)
                    >= TenantPermissionCatalog.MaximumAssignmentsPerRole)
                    return TenantAuthorizationProposalResultStatus.RoleAssignmentConflict;
                if (await IsRoleAssignmentActiveAsync(connection, transaction, intent.TenantId, intent.RoleId!.Value, intent.TargetAccountId.Value, cancellationToken).ConfigureAwait(false))
                    return TenantAuthorizationProposalResultStatus.RoleAssignmentConflict;
                break;
            case TenantAuthorizationProposalKind.UnassignRole:
                if (role is null) return TenantAuthorizationProposalResultStatus.RoleNotFound;
                if (!await IsRoleAssignmentActiveAsync(connection, transaction, intent.TenantId, intent.RoleId!.Value, intent.TargetAccountId!.Value, cancellationToken).ConfigureAwait(false))
                    return TenantAuthorizationProposalResultStatus.RoleAssignmentConflict;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(intent));
        }

        return null;
    }

    private static async Task ApplyLocalCompletionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantAuthorizationProposal proposal,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        switch (proposal.Kind)
        {
            case TenantAuthorizationProposalKind.GrantPermission:
            case TenantAuthorizationProposalKind.RevokePermission:
                await ApplyDirectPermissionAsync(connection, transaction, proposal, occurredAt, cancellationToken).ConfigureAwait(false);
                break;
            case TenantAuthorizationProposalKind.CreateRole:
                await CreateRoleAsync(connection, transaction, proposal, occurredAt, cancellationToken).ConfigureAwait(false);
                break;
            case TenantAuthorizationProposalKind.ReviseRole:
                await ReviseRoleAsync(connection, transaction, proposal, occurredAt, cancellationToken).ConfigureAwait(false);
                break;
            case TenantAuthorizationProposalKind.RetireRole:
                await RetireRoleAsync(connection, transaction, proposal, occurredAt, cancellationToken).ConfigureAwait(false);
                break;
            case TenantAuthorizationProposalKind.AssignRole:
            case TenantAuthorizationProposalKind.UnassignRole:
                await ApplyRoleAssignmentAsync(connection, transaction, proposal, occurredAt, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(proposal));
        }
    }

    private static async Task ApplyDirectPermissionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantAuthorizationProposal proposal,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var active = proposal.Kind == TenantAuthorizationProposalKind.GrantPermission;
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.UpsertDirectPermissionGrant);
        command.Parameters.AddWithValue("tenant_id", proposal.TenantId);
        command.Parameters.AddWithValue("account_id", proposal.TargetAccountId!.Value);
        command.Parameters.AddWithValue("permission_id", proposal.PermissionId!);
        command.Parameters.AddWithValue("is_active", active);
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantAuthorizationProposal proposal,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.InsertCustomRole);
        command.Parameters.AddWithValue("tenant_id", proposal.TenantId);
        command.Parameters.AddWithValue("role_id", proposal.RoleId!.Value);
        command.Parameters.AddWithValue("name", proposal.RoleName!);
        command.Parameters.Add(new NpgsqlParameter<string[]>("permissions", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            TypedValue = proposal.RequestedPermissions.ToArray(),
        });
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReviseRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantAuthorizationProposal proposal,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.ReviseCustomRole);
        command.Parameters.AddWithValue("name", proposal.RoleName!);
        command.Parameters.Add(new NpgsqlParameter<string[]>("permissions", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            TypedValue = proposal.RequestedPermissions.ToArray(),
        });
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        command.Parameters.AddWithValue("tenant_id", proposal.TenantId);
        command.Parameters.AddWithValue("role_id", proposal.RoleId!.Value);
        command.Parameters.AddWithValue("expected_role_revision", proposal.ExpectedRoleRevision!.Value);
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            throw new InvalidOperationException("The custom role changed before local completion.");
    }

    private static async Task RetireRoleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantAuthorizationProposal proposal,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.RetireCustomRole);
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        command.Parameters.AddWithValue("tenant_id", proposal.TenantId);
        command.Parameters.AddWithValue("role_id", proposal.RoleId!.Value);
        command.Parameters.AddWithValue("expected_role_revision", proposal.ExpectedRoleRevision!.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyRoleAssignmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TenantAuthorizationProposal proposal,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var active = proposal.Kind == TenantAuthorizationProposalKind.AssignRole;
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.UpsertCustomRoleAssignment);
        command.Parameters.AddWithValue("tenant_id", proposal.TenantId);
        command.Parameters.AddWithValue("role_id", proposal.RoleId!.Value);
        command.Parameters.AddWithValue("account_id", proposal.TargetAccountId!.Value);
        command.Parameters.AddWithValue("availability", active ? (short)1 : (short)2);
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TenantAuthorizationProposal?> FindByIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid actorAccountId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            TenantAuthorizationAdministrationSql.FindAuthorizationProposalByIdempotencyForUpdate);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("actor_account_id", actorAccountId);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadProposal(reader) : null;
    }

    private static async Task<TenantAuthorizationProposal?> FindProposalForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            TenantAuthorizationAdministrationSql.FindAuthorizationProposalForUpdate);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("proposal_id", proposalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadProposal(reader) : null;
    }

    private static async Task<(TenantAvailability Availability, int Revision)?> LockTenantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.LockTenant);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return ((TenantAvailability)reader.GetInt16(0), reader.GetInt32(1));
    }

    private static async Task<int> LockAuthorizationStateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using (var insert = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.EnsureAuthorizationState))
        {
            insert.Parameters.AddWithValue("tenant_id", tenantId);
            insert.Parameters.AddWithValue("occurred_at", occurredAt);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.LockAuthorizationState);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Tenant authorization state was not created."),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<bool> HasActiveProposalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.HasActiveAuthorizationProposal);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    private static async Task<bool> IsActiveInitialOwnerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.IsActiveInitialOwner);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    private static async Task<bool> IsActiveMembershipAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.IsActiveMembership);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }


    private static async Task<bool> IsDirectPermissionGrantActiveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid accountId,
        string permissionId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.IsDirectPermissionGrantActive);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("permission_id", permissionId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    private static async Task<int> CountActiveRolesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.CountActiveCustomRoles);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<int> CountActiveRoleAssignmentsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.CountActiveCustomRoleAssignments);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("role_id", roleId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<bool> IsRoleAssignmentActiveAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid roleId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.IsCustomRoleAssignmentActive);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("role_id", roleId);
        command.Parameters.AddWithValue("account_id", accountId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    private static async Task<bool> ActiveRoleNameExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        string roleName,
        CancellationToken cancellationToken,
        Guid? exceptRoleId = null)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.ActiveCustomRoleNameExists);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("role_name", roleName);
        AddNullableGuid(command, "except_role_id", exceptRoleId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    private static async Task<TenantCustomRole?> FindRoleForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.FindCustomRoleForUpdate);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("role_id", roleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRole(reader) : null;
    }

    private static async Task<(Guid AccountId, MembershipAvailability Availability, int Revision)?> FindInitialOwnerForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.FindInitialOwnerForUpdate);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return (reader.GetGuid(0), (MembershipAvailability)reader.GetInt16(1), reader.GetInt32(2));
    }

    private static async Task<(MembershipAvailability Availability, int Revision)?> FindMembershipForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.FindMembershipForUpdate);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("account_id", accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return ((MembershipAvailability)reader.GetInt16(0), reader.GetInt32(1));
    }

    private static async Task<(string Fingerprint, Guid PreviousOwner, Guid CurrentOwner, int TenantRevision, int AuthorizationRevision)?>
        FindOwnerTransferReceiptAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            Guid tenantId,
            Guid actorAccountId,
            string idempotencyKey,
            CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.FindOwnerTransferReceipt);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("actor_account_id", actorAccountId);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return (reader.GetString(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetInt32(3), reader.GetInt32(4));
    }

    private static async Task AppendEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid proposalId,
        Guid tenantId,
        Guid actorAccountId,
        TenantAuthorizationProposalStatus status,
        string reason,
        int authorizationRevision,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, TenantAuthorizationAdministrationSql.InsertAuthorizationEvent);
        command.Parameters.AddWithValue("event_id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("proposal_id", proposalId);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("actor_account_id", actorAccountId);
        command.Parameters.AddWithValue("status", (short)status);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("authorization_revision", authorizationRevision);
        command.Parameters.AddWithValue("occurred_at", occurredAt);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TenantAuthorizationProposal ReadProposal(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetGuid(2),
        reader.GetString(3),
        reader.GetString(4),
        (TenantAuthorizationProposalKind)reader.GetInt16(5),
        (TenantAuthorizationProposalStatus)reader.GetInt16(6),
        reader.GetInt32(7),
        reader.IsDBNull(8) ? null : reader.GetInt32(8),
        reader.IsDBNull(9) ? null : reader.GetGuid(9),
        reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.IsDBNull(11) ? null : reader.GetGuid(11),
        reader.IsDBNull(12) ? null : reader.GetInt32(12),
        reader.IsDBNull(13) ? null : reader.GetString(13),
        reader.GetFieldValue<string[]>(14),
        reader.GetFieldValue<string[]>(15),
        reader.GetInt32(16),
        reader.IsDBNull(17) ? null : reader.GetString(17),
        reader.GetFieldValue<DateTimeOffset>(18),
        reader.GetFieldValue<DateTimeOffset>(19));

    private static TenantCustomRole ReadRole(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
        (TenantRoleAvailability)reader.GetInt16(3), reader.GetInt32(4),
        reader.GetFieldValue<string[]>(5), reader.GetFieldValue<DateTimeOffset>(6),
        reader.GetFieldValue<DateTimeOffset>(7),
        reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8));

    private static NpgsqlCommand CreateCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql) => new(sql, connection, transaction);

    private static void AddNullableGuid(NpgsqlCommand command, string name, Guid? value) =>
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Uuid)
        {
            Value = value is { } actual ? actual : DBNull.Value,
        });

    private static void AddNullableInt(NpgsqlCommand command, string name, int? value) =>
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Integer)
        {
            Value = value is { } actual ? actual : DBNull.Value,
        });

    private static void AddNullableText(NpgsqlCommand command, string name, string? value) =>
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text)
        {
            Value = value is null ? DBNull.Value : value,
        });

    private static void ValidateIds(Guid tenantId, Guid value)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        if (value == Guid.Empty) throw new ArgumentException("Identity cannot be empty.", nameof(value));
    }

    private static string NormalizeFailureCode(string failureCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        var value = failureCode.Trim();
        if (value.Length > 120 || value.Any(char.IsControl))
            throw new ArgumentException("Failure code is invalid.", nameof(failureCode));
        return value;
    }
}
