using Application.Tenancy;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Exceptions;
using OpenFga.Sdk.Model;

namespace Application.CoreApi.Authorization;

internal enum TenantAuthorizationProviderOutcome
{
    Applied = 1,
    Uncertain = 2,
    Failed = 3,
}

internal sealed record TenantAuthorizationProviderResult(
    TenantAuthorizationProviderOutcome Outcome,
    string? FailureCode = null);

internal interface ITenantAuthorizationAdministrationProvider
{
    Task<TenantAuthorizationProviderResult> EnsureAsync(
        TenantAuthorizationProposal proposal,
        IReadOnlyList<TenantRoleAssignment> roleAssignments,
        CancellationToken cancellationToken);
}

internal sealed class OpenFgaTenantAuthorizationAdministrationProvider(
    IOpenFgaClient client,
    OpenFgaAuthorizationConfiguration configuration) : ITenantAuthorizationAdministrationProvider
{
    private const string RoleAssigneeRelation = "assignee";

    public async Task<TenantAuthorizationProviderResult> EnsureAsync(
        TenantAuthorizationProposal proposal,
        IReadOnlyList<TenantRoleAssignment> roleAssignments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(roleAssignments);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(configuration.RequestTimeout);
        try
        {
            var mutations = BuildMutations(proposal, roleAssignments);
            if (mutations.Writes.Count > 0 || mutations.Deletes.Count > 0)
            {
                await client.Write(
                    new ClientWriteRequest
                    {
                        Writes = mutations.Writes,
                        Deletes = mutations.Deletes,
                    },
                    new ClientWriteOptions
                    {
                        StoreId = configuration.StoreId,
                        AuthorizationModelId = configuration.AuthorizationModelId,
                        Conflict = new ConflictOptions
                        {
                            OnDuplicateWrites = OnDuplicateWrites.Ignore,
                            OnMissingDeletes = OnMissingDeletes.Ignore,
                        },
                    },
                    timeout.Token).ConfigureAwait(false);
            }

            foreach (var revoked in mutations.RevokedTuples)
            {
                if (await RevokedTupleStillPresentAsync(revoked, timeout.Token).ConfigureAwait(false))
                {
                    return new TenantAuthorizationProviderResult(
                        TenantAuthorizationProviderOutcome.Uncertain,
                        "provider_state_not_observed");
                }
            }

            foreach (var expected in mutations.Expected)
            {
                var observed = await CheckAsync(expected.User, expected.Relation, expected.Object, timeout.Token)
                    .ConfigureAwait(false);
                if (observed != expected.Allowed)
                {
                    return new TenantAuthorizationProviderResult(
                        TenantAuthorizationProviderOutcome.Uncertain,
                        "provider_state_not_observed");
                }
            }

            return new TenantAuthorizationProviderResult(TenantAuthorizationProviderOutcome.Applied);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new TenantAuthorizationProviderResult(
                TenantAuthorizationProviderOutcome.Uncertain,
                "provider_timeout");
        }
        catch (Exception exception) when (exception is FgaApiValidationError or FgaApiNotFoundError)
        {
            return new TenantAuthorizationProviderResult(
                TenantAuthorizationProviderOutcome.Failed,
                "authorization_model_rejected");
        }
        catch (Exception exception) when (IsCredentialDenied(exception))
        {
            return new TenantAuthorizationProviderResult(
                TenantAuthorizationProviderOutcome.Uncertain,
                "provider_denied_credential");
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException)
        {
            return new TenantAuthorizationProviderResult(
                TenantAuthorizationProviderOutcome.Uncertain,
                "provider_unavailable");
        }
    }

    private async Task<bool> CheckAsync(
        string user,
        string relation,
        string objectId,
        CancellationToken cancellationToken)
    {
        var result = await client.Check(
            new ClientCheckRequest
            {
                User = user,
                Relation = relation,
                Object = objectId,
            },
            new ClientCheckOptions
            {
                StoreId = configuration.StoreId,
                AuthorizationModelId = configuration.AuthorizationModelId,
                Consistency = ConsistencyPreference.HIGHERCONSISTENCY,
            },
            cancellationToken).ConfigureAwait(false);
        return result.Allowed is true;
    }

    // Proves a revoked direct grant is gone. Reading tuples is a distinct authorization from
    // checking a permission, so a credential that may write and check but not read fails
    // here. That is reported as its own outcome code rather than a generic outage, because
    // the difference between "the provider is briefly unavailable" and "this credential can
    // never revoke a direct grant" decides whether retrying can ever help.
    private async Task<bool> RevokedTupleStillPresentAsync(
        ExpectedTuple revoked,
        CancellationToken cancellationToken)
    {
        var response = await client.Read(
            new ClientReadRequest
            {
                User = revoked.User,
                Relation = revoked.Relation,
                Object = revoked.Object,
            },
            new ClientReadOptions
            {
                StoreId = configuration.StoreId,
                Consistency = OpenFga.Sdk.Model.ConsistencyPreference.HIGHERCONSISTENCY,
            },
            cancellationToken).ConfigureAwait(false);
        return response.Tuples is { Count: > 0 };
    }

    private static MutationSet BuildMutations(
        TenantAuthorizationProposal proposal,
        IReadOnlyList<TenantRoleAssignment> roleAssignments)
    {
        var writes = new List<ClientTupleKey>();
        var deletes = new List<ClientTupleKeyWithoutCondition>();
        var expected = new List<ExpectedTuple>();
        var revokedTuples = new List<ExpectedTuple>();
        var tenantObject = $"tenant:{proposal.TenantId:N}";

        switch (proposal.Kind)
        {
            case TenantAuthorizationProposalKind.GrantPermission:
            case TenantAuthorizationProposalKind.RevokePermission:
                {
                    var definition = Permission(proposal.PermissionId!);
                    var user = $"user:{proposal.TargetAccountId!.Value:N}";
                    var allowed = proposal.Kind == TenantAuthorizationProposalKind.GrantPermission;
                    // Grant and revoke are verified differently, because the aggregate
                    // permission answer is only meaningful in one direction.
                    //
                    // After a grant the account is expected to hold the permission, so the
                    // ordinary permission check is the correct and sufficient verification.
                    // Using it keeps the grant path dependent only on write and check.
                    //
                    // After a revoke the account may still hold the permission through a
                    // custom role, so expecting the aggregate check to become false would
                    // strand the proposal as Uncertain forever. Proving the direct tuple is
                    // gone requires reading tuples, which is a different authorization.
                    if (allowed)
                    {
                        writes.Add(new ClientTupleKey
                        {
                            User = user,
                            Relation = definition.Relation,
                            Object = tenantObject,
                        });
                    }
                    else
                    {
                        deletes.Add(new ClientTupleKeyWithoutCondition
                        {
                            User = user,
                            Relation = definition.Relation,
                            Object = tenantObject,
                        });
                    }

                    (allowed ? expected : revokedTuples).Add(
                        new ExpectedTuple(user, definition.Relation, tenantObject, allowed));
                    break;
                }
            case TenantAuthorizationProposalKind.CreateRole:
            case TenantAuthorizationProposalKind.ReviseRole:
                {
                    var roleUser = RoleUserset(proposal.TenantId, proposal.RoleId!.Value);
                    var applied = proposal.AppliedPermissions.ToHashSet(StringComparer.Ordinal);
                    var requested = proposal.RequestedPermissions.ToHashSet(StringComparer.Ordinal);
                    foreach (var permission in applied.Except(requested, StringComparer.Ordinal))
                    {
                        AddMutation(writes, deletes, expected, roleUser, Permission(permission).Relation, tenantObject, false);
                    }
                    foreach (var permission in requested.Except(applied, StringComparer.Ordinal))
                    {
                        AddMutation(writes, deletes, expected, roleUser, Permission(permission).Relation, tenantObject, true);
                    }
                    foreach (var permission in requested.Intersect(applied, StringComparer.Ordinal))
                    {
                        expected.Add(new ExpectedTuple(roleUser, Permission(permission).Relation, tenantObject, true));
                    }
                    break;
                }
            case TenantAuthorizationProposalKind.RetireRole:
                {
                    var roleUser = RoleUserset(proposal.TenantId, proposal.RoleId!.Value);
                    foreach (var permission in proposal.AppliedPermissions.Distinct(StringComparer.Ordinal))
                    {
                        AddMutation(writes, deletes, expected, roleUser, Permission(permission).Relation, tenantObject, false);
                    }
                    var roleObject = RoleObject(proposal.TenantId, proposal.RoleId.Value);
                    foreach (var assignment in roleAssignments.Where(candidate =>
                                 candidate.Availability == TenantRoleAssignmentAvailability.Active))
                    {
                        AddMutation(
                            writes, deletes, expected,
                            $"user:{assignment.AccountId:N}", RoleAssigneeRelation, roleObject, false);
                    }
                    break;
                }
            case TenantAuthorizationProposalKind.AssignRole:
            case TenantAuthorizationProposalKind.UnassignRole:
                {
                    var allowed = proposal.Kind == TenantAuthorizationProposalKind.AssignRole;
                    AddMutation(
                        writes, deletes, expected,
                        $"user:{proposal.TargetAccountId!.Value:N}",
                        RoleAssigneeRelation,
                        RoleObject(proposal.TenantId, proposal.RoleId!.Value),
                        allowed);
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(proposal));
        }

        return new MutationSet(writes, deletes, expected, revokedTuples);
    }

    private static TenantPermissionDefinition Permission(string permissionId) =>
        TenantPermissionCatalog.TryGet(permissionId, out var definition)
            ? definition
            : throw new InvalidOperationException("A durable proposal referenced an unsupported permission identifier.");

    private static string RoleObject(Guid tenantId, Guid roleId) => $"role:{tenantId:N}_{roleId:N}";

    private static string RoleUserset(Guid tenantId, Guid roleId) => $"{RoleObject(tenantId, roleId)}#{RoleAssigneeRelation}";

    private static void AddMutation(
        List<ClientTupleKey> writes,
        List<ClientTupleKeyWithoutCondition> deletes,
        List<ExpectedTuple> expected,
        string user,
        string relation,
        string objectId,
        bool allowed)
    {
        if (allowed)
        {
            writes.Add(new ClientTupleKey { User = user, Relation = relation, Object = objectId });
        }
        else
        {
            deletes.Add(new ClientTupleKeyWithoutCondition { User = user, Relation = relation, Object = objectId });
        }
        expected.Add(new ExpectedTuple(user, relation, objectId, allowed));
    }

    // A rejected credential is distinguishable from a transient outage, and it matters:
    // a transient outage resolves on retry, while a credential that may write and check but
    // not read tuples can never revoke a direct grant, so the proposal stays unresolved
    // indefinitely no matter how often it is retried.
    private static bool IsCredentialDenied(Exception exception) => exception switch
    {
        FgaApiAuthenticationError => true,
        FgaApiError fga => fga.StatusCode is System.Net.HttpStatusCode.Unauthorized
            or System.Net.HttpStatusCode.Forbidden,
        _ => false,
    };

    private sealed record MutationSet(
        List<ClientTupleKey> Writes,
        List<ClientTupleKeyWithoutCondition> Deletes,
        List<ExpectedTuple> Expected,
        List<ExpectedTuple> RevokedTuples);

    private sealed record ExpectedTuple(string User, string Relation, string Object, bool Allowed);
}

internal sealed class TenantAuthorizationReconciler(
    ITenantAuthorizationAdministrationStore store,
    ITenantAuthorizationAdministrationProvider provider,
    TimeProvider timeProvider)
{
    internal async Task<TenantAuthorizationProposal?> ReconcileAsync(
        Guid tenantId,
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        var proposal = await store.MarkAttemptAsync(
            tenantId, proposalId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (proposal is null || proposal.Status is TenantAuthorizationProposalStatus.Applied or TenantAuthorizationProposalStatus.Failed)
        {
            return proposal;
        }

        if (!await store.IsInitialOwnerAsync(
                proposal.TenantId,
                proposal.RequestedByAccountId,
                cancellationToken).ConfigureAwait(false))
        {
            return await store.MarkFailedAsync(
                proposal.TenantId,
                proposal.ProposalId,
                "delegator_authority_revoked",
                timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
        }

        var assignments = proposal.RoleId is { } roleId && proposal.Kind == TenantAuthorizationProposalKind.RetireRole
            ? await store.ListRoleAssignmentsAsync(proposal.TenantId, roleId, cancellationToken).ConfigureAwait(false)
            : [];
        var result = await provider.EnsureAsync(proposal, assignments, cancellationToken).ConfigureAwait(false);
        return result.Outcome switch
        {
            TenantAuthorizationProviderOutcome.Applied => await store.CompleteAsync(
                proposal.TenantId, proposal.ProposalId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false),
            TenantAuthorizationProviderOutcome.Uncertain => await store.MarkUncertainAsync(
                proposal.TenantId, proposal.ProposalId, result.FailureCode ?? "provider_outcome_uncertain",
                timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false),
            TenantAuthorizationProviderOutcome.Failed => await store.MarkFailedAsync(
                proposal.TenantId, proposal.ProposalId, result.FailureCode ?? "provider_rejected",
                timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unexpected tenant authorization provider outcome."),
        };
    }
}
