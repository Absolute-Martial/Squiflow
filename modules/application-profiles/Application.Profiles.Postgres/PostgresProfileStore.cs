using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.PlatformAdministration;
using Application.Tenancy;
using Npgsql;

namespace Application.Profiles.Postgres;

public sealed class PostgresProfileStore(NpgsqlDataSource source, TimeProvider clock) : IProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true };
    public async Task<TenantPolicyState?> GetPolicyAsync(TenantContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        return await ReadPolicyStateAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
    }
    public async Task<PublishedTenantPolicy?> GetPublishedPolicyAsync(TenantContext context, Guid policyRevisionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context); TenantProfileRules.RequireIdentity(policyRevisionId);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, context.TenantId, cancellationToken).ConfigureAwait(false);
        return await ReadPublishedPolicyAsync(connection, transaction, context.TenantId, policyRevisionId, cancellationToken).ConfigureAwait(false);
    }
    public Task<ProfileCommandResult> EditPolicyAsync(TenantContext context, EditTenantPolicyRequest request, long observedAuthorizationRevision,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(request);
        TenantProfileRules.RequireExpectedRevision(request.ExpectedRevision); TenantProfileRules.RequireAuthorizationRevision(observedAuthorizationRevision);
        var fingerprint = TenantProfileRules.Fingerprint("edit-policy", Revision(request.ExpectedRevision), request.RequireReferenceForProgramOrders ? "true" : "false");
        return MutateAsync(context.TenantId, context.AccountId, null, "edit-policy", idempotencyKey, fingerprint, observedAuthorizationRevision,
            async (connection, transaction, ct) =>
            {
                var previous = await ReadPolicyStateAsync(connection, transaction, context.TenantId, ct).ConfigureAwait(false);
                if ((previous?.Revision ?? 0) != request.ExpectedRevision) return new(ProfileCommandStatus.RevisionConflict);
                var state = new TenantPolicyState(context.TenantId, checked(request.ExpectedRevision + 1), request.RequireReferenceForProgramOrders, previous?.PublishedPolicyRevisionId);
                await using var command = Command(previous is null ?
                    ProfileSql.InsertPolicyHead : ProfileSql.UpdatePolicyHead, connection, transaction,
                    ("tenant", context.TenantId), ("revision", state.Revision), ("required", state.RequireReferenceForProgramOrders), ("expected", request.ExpectedRevision));
                RequireOne(await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false));
                return new(ProfileCommandStatus.Edited, state);
            }, result =>
            {
                if (result.PolicyState?.Revision != request.ExpectedRevision + 1 || result.PolicyState.RequireReferenceForProgramOrders != request.RequireReferenceForProgramOrders)
                    throw InvalidFacts();
            }, cancellationToken);
    }
    public Task<ProfileCommandResult> PublishPolicyAsync(TenantContext context, PublishTenantPolicyRequest request, long observedAuthorizationRevision,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(request);
        TenantProfileRules.RequireExpectedRevision(request.ExpectedRevision); TenantProfileRules.RequireAuthorizationRevision(observedAuthorizationRevision);
        var fingerprint = TenantProfileRules.Fingerprint("publish-policy", Revision(request.ExpectedRevision));
        return MutateAsync(context.TenantId, context.AccountId, null, "publish-policy", idempotencyKey, fingerprint, observedAuthorizationRevision,
            async (connection, transaction, ct) =>
            {
                var previous = await ReadPolicyStateAsync(connection, transaction, context.TenantId, ct).ConfigureAwait(false);
                if (previous is null) return new(ProfileCommandStatus.NotFound);
                if (previous.Revision != request.ExpectedRevision) return new(ProfileCommandStatus.RevisionConflict);
                var policy = new PublishedTenantPolicy(Guid.CreateVersion7(), context.TenantId, checked(previous.Revision + 1),
                    previous.RequireReferenceForProgramOrders, context.AccountId, Now(), observedAuthorizationRevision);
                await using (var insert = Command(ProfileSql.InsertPolicyRevision,
                    connection, transaction, ("tenant", context.TenantId), ("id", policy.PolicyRevisionId), ("revision", policy.Revision),
                    ("required", policy.RequireReferenceForProgramOrders), ("facts", Serialize(policy, 8192))))
                    RequireOne(await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false));
                await using (var update = Command(ProfileSql.PublishPolicyHead, connection, transaction,
                    ("tenant", context.TenantId), ("revision", policy.Revision), ("id", policy.PolicyRevisionId), ("expected", request.ExpectedRevision)))
                    RequireOne(await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false));
                return new(ProfileCommandStatus.PolicyPublished, new(context.TenantId, policy.Revision, policy.RequireReferenceForProgramOrders, policy.PolicyRevisionId), policy);
            }, result =>
            {
                if (result.PublishedPolicy?.Revision != request.ExpectedRevision + 1 || result.PublishedPolicy.PublishedByAccountId != context.AccountId)
                    throw InvalidFacts();
            }, cancellationToken);
    }
    public async Task<TenantProfileAuthority?> GetAuthorityAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        TenantProfileRules.RequireIdentity(tenantId);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);
        return await ReadAuthorityAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);
    }
    public async Task<TenantProfileSnapshot?> GetProfileAsync(Guid tenantId, Guid profileId, CancellationToken cancellationToken)
    {
        TenantProfileRules.RequireIdentity(tenantId); TenantProfileRules.RequireIdentity(profileId);
        await using var connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);
        return await ReadProfileAsync(connection, transaction, tenantId, profileId, cancellationToken).ConfigureAwait(false);
    }
    public Task<ProfileCommandResult> PublishProfileAsync(PlatformAdminAccess access, PublishTenantProfileRequest request,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        ValidatePlatform(access); ArgumentNullException.ThrowIfNull(request); TenantProfileRules.RequireIdentity(request.TenantId);
        TenantProfileRules.RequireIdentity(request.PublishedPolicyRevisionId); TenantProfileRules.RequireExpectedRevision(request.ExpectedAuthorityRevision);
        TenantProfileRules.RequireAuthorizationRevision(request.ObservedAuthorizationRevision);
        var fingerprint = TenantProfileRules.Fingerprint("publish-profile", access.DeviceId.ToString("N"), Revision(request.ExpectedAuthorityRevision), request.PublishedPolicyRevisionId.ToString("N"),
            request.IsLegacyBaseline ? "true" : "false");
        return MutateAsync(request.TenantId, access.PrincipalId, access.DeviceId, "publish-profile", idempotencyKey, fingerprint, null,
            async (connection, transaction, ct) =>
            {
                var authority = await ReadAuthorityAsync(connection, transaction, request.TenantId, ct).ConfigureAwait(false);
                if ((authority?.Revision ?? 0) != request.ExpectedAuthorityRevision) return new(ProfileCommandStatus.RevisionConflict);
                var policy = await ReadPublishedPolicyAsync(connection, transaction, request.TenantId, request.PublishedPolicyRevisionId, ct).ConfigureAwait(false);
                if (policy is null) return new(ProfileCommandStatus.NotFound);
                if (request.IsLegacyBaseline && policy.RequireReferenceForProgramOrders) return new(ProfileCommandStatus.InvalidBaseline);
                var selection = CommercialFeatureCatalog.Catalog.Compile([]);
                var profile = new TenantProfileSnapshot(TenantProfileRules.SupportedVersion, Guid.CreateVersion7(), request.TenantId,
                    selection.CatalogFingerprint, selection.SelectionFingerprint, selection.EffectiveFeatureIds, policy, access.PrincipalId, access.DeviceId, Now(),
                    request.ObservedAuthorizationRevision, request.IsLegacyBaseline);
                TenantProfileRules.ValidateProfile(profile, request.TenantId);
                await using (var insert = Command(ProfileSql.InsertPublication,
                    connection, transaction, ("tenant", request.TenantId), ("id", profile.ProfileId), ("policy", policy.PolicyRevisionId),
                    ("baseline", request.IsLegacyBaseline), ("facts", Serialize(profile, 16384))))
                    RequireOne(await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false));
                var next = new TenantProfileAuthority(request.TenantId, checked(request.ExpectedAuthorityRevision + 1), authority?.ActiveProfileId, authority?.LegacyBaselineProfileId);
                await WriteAuthorityAsync(connection, transaction, next, authority is null, request.ExpectedAuthorityRevision, ct).ConfigureAwait(false);
                return new(ProfileCommandStatus.ProfilePublished, PublishedPolicy: policy, Profile: profile, Authority: next);
            }, result =>
            {
                if (result.Profile?.Policy.PolicyRevisionId != request.PublishedPolicyRevisionId || result.Profile.IsLegacyBaseline != request.IsLegacyBaseline ||
                    result.Profile.PublishedByPrincipalId != access.PrincipalId ||
                    result.Profile.PublishedByDeviceId != access.DeviceId || result.Authority?.Revision != request.ExpectedAuthorityRevision + 1) throw InvalidFacts();
            }, cancellationToken);
    }
    public Task<ProfileCommandResult> ActivateProfileAsync(PlatformAdminAccess access, ActivateTenantProfileRequest request,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        ValidatePlatform(access); ArgumentNullException.ThrowIfNull(request); TenantProfileRules.RequireIdentity(request.TenantId);
        TenantProfileRules.RequireIdentity(request.ProfileId); TenantProfileRules.RequireExpectedRevision(request.ExpectedAuthorityRevision);
        var operation = request.AsLegacyBaseline ? "select-legacy-baseline" : "activate-profile";
        var fingerprint = TenantProfileRules.Fingerprint(operation, access.DeviceId.ToString("N"), Revision(request.ExpectedAuthorityRevision), request.ProfileId.ToString("N"));
        return MutateAsync(request.TenantId, access.PrincipalId, access.DeviceId, operation, idempotencyKey, fingerprint, null,
            async (connection, transaction, ct) =>
            {
                var authority = await ReadAuthorityAsync(connection, transaction, request.TenantId, ct).ConfigureAwait(false);
                if ((authority?.Revision ?? 0) != request.ExpectedAuthorityRevision) return new(ProfileCommandStatus.RevisionConflict);
                var profile = await ReadProfileAsync(connection, transaction, request.TenantId, request.ProfileId, ct).ConfigureAwait(false);
                if (profile is null) return new(ProfileCommandStatus.NotFound);
                if (request.AsLegacyBaseline && (!profile.IsLegacyBaseline || profile.Policy.RequireReferenceForProgramOrders)) return new(ProfileCommandStatus.InvalidBaseline);
                if (request.AsLegacyBaseline && authority?.LegacyBaselineProfileId is not null) return new(ProfileCommandStatus.BaselineAlreadySelected);
                var next = new TenantProfileAuthority(request.TenantId, checked(request.ExpectedAuthorityRevision + 1),
                    request.AsLegacyBaseline ? authority?.ActiveProfileId : profile.ProfileId,
                    request.AsLegacyBaseline ? profile.ProfileId : authority?.LegacyBaselineProfileId);
                await WriteAuthorityAsync(connection, transaction, next, authority is null, request.ExpectedAuthorityRevision, ct).ConfigureAwait(false);
                return new(request.AsLegacyBaseline ? ProfileCommandStatus.LegacyBaselineSelected : ProfileCommandStatus.Activated, Profile: profile, Authority: next);
            }, result =>
            {
                if (result.Profile?.ProfileId != request.ProfileId || result.Authority?.Revision != request.ExpectedAuthorityRevision + 1 ||
                    (request.AsLegacyBaseline ? result.Authority.LegacyBaselineProfileId : result.Authority.ActiveProfileId) != request.ProfileId) throw InvalidFacts();
            }, cancellationToken);
    }

    public static async Task<TenantProfileSnapshot?> ResolveActiveForOrderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, CancellationToken cancellationToken)
    {
        await PrepareOrderResolutionAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);
        var authority = await ReadAuthorityAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);
        return authority?.ActiveProfileId is { } id ? await ReadProfileAsync(connection, transaction, tenantId, id, cancellationToken).ConfigureAwait(false) : null;
    }
    public static async Task<TenantProfileSnapshot?> ResolveLegacyBaselineForOrderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, CancellationToken cancellationToken)
    {
        await PrepareOrderResolutionAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);
        var authority = await ReadAuthorityAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);
        return authority?.LegacyBaselineProfileId is { } id ? await ReadProfileAsync(connection, transaction, tenantId, id, cancellationToken).ConfigureAwait(false) : null;
    }
    public static async Task<TenantProfileSnapshot?> ResolveRetainedForOrderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid tenantId, Guid profileId, CancellationToken cancellationToken)
    {
        TenantProfileRules.RequireIdentity(profileId);
        await PrepareOrderResolutionAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);
        return await ReadProfileAsync(connection, transaction, tenantId, profileId, cancellationToken).ConfigureAwait(false);
    }
    private static async Task PrepareOrderResolutionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection); ArgumentNullException.ThrowIfNull(transaction); TenantProfileRules.RequireIdentity(tenantId);
        if (connection.State != System.Data.ConnectionState.Open || transaction.Connection != connection)
            throw new InvalidOperationException("Profile resolution requires the caller's active transaction.");
        await SetTenantAsync(connection, transaction, tenantId, ct).ConfigureAwait(false);
        await LockTenantAsync(connection, transaction, tenantId, ct).ConfigureAwait(false);
    }
    private async Task<ProfileCommandResult> MutateAsync(Guid tenantId, Guid actorId, Guid? deviceId, string operation, string key, string fingerprint,
        long? observedAuthorizationRevision, Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<ProfileCommandResult>> mutate,
        Action<ProfileCommandResult> validateIntent, CancellationToken ct)
    {
        key = TenantProfileRules.Key(key); TenantProfileRules.RequireIdentity(tenantId); TenantProfileRules.RequireIdentity(actorId);
        await using var connection = await source.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, ct).ConfigureAwait(false);
        // One bounded tenant routing lock also fences Order creation and baseline assignment in their caller-owned transactions.
        await LockTenantAsync(connection, transaction, tenantId, ct).ConfigureAwait(false);
        var kind = deviceId.HasValue ? "platform" : "tenant";
        await using (var receipt = Command(ProfileSql.FindCommandReceipt,
            connection, transaction, ("tenant", tenantId), ("kind", kind), ("actor", actorId), ("operation", operation), ("key", key)))
        {
            await using var reader = await receipt.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                if (reader.GetString(0) != fingerprint) return new(ProfileCommandStatus.IdempotencyKeyConflict);
                if (reader.GetInt32(1) != 1 || (reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3)) != deviceId) throw InvalidFacts();
                TenantProfileRules.RequireTime(reader.GetFieldValue<DateTimeOffset>(4));
                var original = Deserialize<ProfileCommandResult>(reader.GetString(2), 32768);
                await reader.DisposeAsync().ConfigureAwait(false);
                await ValidateResultAsync(connection, transaction, tenantId, operation, original, ct).ConfigureAwait(false);
                validateIntent(original);
                return original with { Status = ProfileCommandStatus.Replayed };
            }
        }
        var result = await mutate(connection, transaction, ct).ConfigureAwait(false);
        if (result.Status is ProfileCommandStatus.NotFound or ProfileCommandStatus.RevisionConflict or ProfileCommandStatus.InvalidBaseline or ProfileCommandStatus.BaselineAlreadySelected) return result;
        await ValidateResultAsync(connection, transaction, tenantId, operation, result, ct).ConfigureAwait(false); validateIntent(result);
        await using (var insert = Command(ProfileSql.InsertCommandReceipt,
            connection, transaction, ("tenant", tenantId), ("actor", actorId), ("kind", kind), ("device", deviceId ?? (object)DBNull.Value),
            ("operation", operation), ("key", key), ("fingerprint", fingerprint), ("at", Now()), ("observed", observedAuthorizationRevision ?? (object)DBNull.Value),
            ("result", Serialize(result, 32768))))
            RequireOne(await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false));
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return result;
    }
    private static async Task<TenantPolicyState?> ReadPolicyStateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, CancellationToken ct)
    {
        await using var command = Command(ProfileSql.ReadPolicyHead, connection, transaction, ("tenant", tenantId));
        TenantPolicyState? state;
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
            state = new(tenantId, reader.GetInt64(0), reader.GetBoolean(1), reader.IsDBNull(2) ? null : reader.GetGuid(2));
        }
        if (state.Revision < 1) throw InvalidFacts();
        if (state.PublishedPolicyRevisionId is { } id)
        {
            var policy = await ReadPublishedPolicyAsync(connection, transaction, tenantId, id, ct).ConfigureAwait(false);
            if (policy is null || policy.Revision > state.Revision) throw InvalidFacts();
        }
        return state;
    }
    private static async Task<PublishedTenantPolicy?> ReadPublishedPolicyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid id, CancellationToken ct)
    {
        await using var command = Command(ProfileSql.ReadPolicyRevision, connection, transaction, ("tenant", tenantId), ("id", id));
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        var policy = Deserialize<PublishedTenantPolicy>(reader.GetString(3), 8192);
        TenantProfileRules.ValidatePolicy(policy, tenantId);
        if (policy.PolicyRevisionId != id || policy.Revision != reader.GetInt64(0) || policy.RequireReferenceForProgramOrders != reader.GetBoolean(1) || reader.GetInt32(2) != 1) throw InvalidFacts();
        return policy;
    }
    private static async Task<TenantProfileSnapshot?> ReadProfileAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid id, CancellationToken ct)
    {
        await using var command = Command(ProfileSql.ReadPublication, connection, transaction, ("tenant", tenantId), ("id", id));
        TenantProfileSnapshot profile;
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
            profile = Deserialize<TenantProfileSnapshot>(reader.GetString(3), 16384); TenantProfileRules.ValidateProfile(profile, tenantId);
            if (profile.ProfileId != id || profile.Policy.PolicyRevisionId != reader.GetGuid(0) || profile.IsLegacyBaseline != reader.GetBoolean(1) || reader.GetInt32(2) != 1) throw InvalidFacts();
        }
        var policy = await ReadPublishedPolicyAsync(connection, transaction, tenantId, profile.Policy.PolicyRevisionId, ct).ConfigureAwait(false);
        if (policy != profile.Policy) throw InvalidFacts();
        return profile with { FeatureIds = Array.AsReadOnly(profile.FeatureIds.ToArray()) };
    }
    private static async Task<TenantProfileAuthority?> ReadAuthorityAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, CancellationToken ct)
    {
        await using var command = Command(ProfileSql.ReadAuthority, connection, transaction, ("tenant", tenantId));
        TenantProfileAuthority authority;
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
            authority = new(tenantId, reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2));
        }
        await ValidateAuthorityAsync(connection, transaction, tenantId, authority, ct).ConfigureAwait(false);
        return authority;
    }
    private static async Task ValidateAuthorityAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, TenantProfileAuthority authority, CancellationToken ct)
    {
        if (authority.TenantId != tenantId || authority.Revision < 1) throw InvalidFacts();
        if (authority.ActiveProfileId is { } active && await ReadProfileAsync(connection, transaction, tenantId, active, ct).ConfigureAwait(false) is null) throw InvalidFacts();
        if (authority.LegacyBaselineProfileId is { } legacy)
        {
            var profile = await ReadProfileAsync(connection, transaction, tenantId, legacy, ct).ConfigureAwait(false);
            if (profile is null || !profile.IsLegacyBaseline || profile.Policy.RequireReferenceForProgramOrders) throw InvalidFacts();
        }
    }
    private static async Task WriteAuthorityAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantProfileAuthority authority, bool create, long expected, CancellationToken ct)
    {
        await using var command = Command(create ? ProfileSql.InsertAuthority : ProfileSql.UpdateAuthority,
            connection, transaction, ("tenant", authority.TenantId), ("revision", authority.Revision), ("active", authority.ActiveProfileId ?? (object)DBNull.Value),
            ("baseline", authority.LegacyBaselineProfileId ?? (object)DBNull.Value), ("expected", expected));
        RequireOne(await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false));
    }
    private static async Task ValidateResultAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, string operation, ProfileCommandResult result, CancellationToken ct)
    {
        var expectedStatus = operation switch
        {
            "edit-policy" => ProfileCommandStatus.Edited,
            "publish-policy" => ProfileCommandStatus.PolicyPublished,
            "publish-profile" => ProfileCommandStatus.ProfilePublished,
            "activate-profile" => ProfileCommandStatus.Activated,
            "select-legacy-baseline" => ProfileCommandStatus.LegacyBaselineSelected,
            _ => throw InvalidFacts()
        };
        if (result.Status != expectedStatus) throw InvalidFacts();
        if (result.PolicyState is { } state)
        {
            if (state.TenantId != tenantId || state.Revision < 1) throw InvalidFacts();
            if (state.PublishedPolicyRevisionId is { } publishedId)
            {
                var published = await ReadPublishedPolicyAsync(connection, transaction, tenantId, publishedId, ct).ConfigureAwait(false);
                if (published is null || published.Revision > state.Revision) throw InvalidFacts();
            }
        }
        if (result.PublishedPolicy is { } policy && policy != await ReadPublishedPolicyAsync(connection, transaction, tenantId, policy.PolicyRevisionId, ct).ConfigureAwait(false)) throw InvalidFacts();
        if (result.Profile is { } profile)
        {
            var trusted = await ReadProfileAsync(connection, transaction, tenantId, profile.ProfileId, ct).ConfigureAwait(false);
            if (trusted is null || Serialize(profile, 16384) != Serialize(trusted, 16384)) throw InvalidFacts();
        }
        if (result.Authority is { } authority) await ValidateAuthorityAsync(connection, transaction, tenantId, authority, ct).ConfigureAwait(false);
        if (operation == "edit-policy" && (result.PolicyState is null || result.PublishedPolicy is not null || result.Profile is not null || result.Authority is not null) ||
            operation == "publish-policy" && (result.PolicyState is null || result.PublishedPolicy is null || result.Profile is not null || result.Authority is not null ||
                result.PolicyState.PublishedPolicyRevisionId != result.PublishedPolicy.PolicyRevisionId || result.PolicyState.Revision != result.PublishedPolicy.Revision ||
                result.PolicyState.RequireReferenceForProgramOrders != result.PublishedPolicy.RequireReferenceForProgramOrders) ||
            operation is "publish-profile" or "activate-profile" or "select-legacy-baseline" && (result.Profile is null || result.Authority is null || result.PolicyState is not null) ||
            operation == "publish-profile" && result.PublishedPolicy != result.Profile?.Policy ||
            operation is "activate-profile" or "select-legacy-baseline" && result.PublishedPolicy is not null) throw InvalidFacts();
    }
    private static void ValidatePlatform(PlatformAdminAccess access)
    {
        ArgumentNullException.ThrowIfNull(access); TenantProfileRules.RequireIdentity(access.PrincipalId); TenantProfileRules.RequireIdentity(access.DeviceId);
    }
    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, CancellationToken ct)
    {
        await using var command = Command(ProfileSql.SetTenant, connection, transaction, ("tenant", tenantId.ToString("D")));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
    private static async Task LockTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, CancellationToken ct)
    {
        await using var command = Command(ProfileSql.LockTenant, connection, transaction, ("tenant", tenantId));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
    private static NpgsqlCommand Command(string sql, NpgsqlConnection connection, NpgsqlTransaction transaction, params (string Name, object Value)[] values)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return command;
    }
    private static void RequireOne(int count) { if (count != 1) throw new InvalidOperationException("A locked profile mutation changed unexpectedly."); }
    private static InvalidOperationException InvalidFacts() => new("Retained tenant profile facts are inconsistent or unsupported.");
    private static string Revision(long revision) => revision.ToString(CultureInfo.InvariantCulture);
    private DateTimeOffset Now() { var now = clock.GetUtcNow(); return new(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero); }
    private static T Deserialize<T>(string json, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(json) > maximumBytes) throw InvalidFacts();
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw InvalidFacts();
        }
        catch (JsonException)
        {
            throw InvalidFacts();
        }
    }
    private static string Serialize<T>(T value, int maximumBytes)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        if (bytes.Length > maximumBytes) throw InvalidFacts();
        return Encoding.UTF8.GetString(bytes);
    }
}
