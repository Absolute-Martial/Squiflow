using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Application.Pricing;
using Npgsql;
using NpgsqlTypes;

namespace Application.Pricing.Postgres;

public sealed partial class PostgresPricingStore(
    NpgsqlDataSource dataSource,
     TimeProvider? timeProvider = null) : IPricingPublicationStore, IPricingCandidateReader, IPricingPolicyStore
{
    private const string CreateDraftOperation = "create-price-draft";
    private const string PublishOperation = "publish-price";
    private const string RetireOperation = "retire-price";
    private static readonly JsonSerializerOptions ReceiptJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<CreatePriceDraftResult> CreateDraftAsync(
        PricingActorContext actor,
        CreatePriceDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        RequireDraft(request);
        RequireIdempotencyKey(idempotencyKey);
        await using var session = await PricingTenantDbSession.OpenAsync(
                dataSource, actor.TenantId, cancellationToken)
             .ConfigureAwait(false);

        await LockAsync(session, $"receipt|{actor.TenantId:D}|{actor.AccountId:D}|{CreateDraftOperation}|{idempotencyKey}", cancellationToken).ConfigureAwait(false);

        var receipt = await FindReceiptAsync(
                session, actor, CreateDraftOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            var replay = await ReplayCreateAsync(
                    session, actor, receipt.Value, request.Fingerprint, cancellationToken)
                .ConfigureAwait(false);
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return replay;
        }

        var now = GetDatabaseInstant();
        var revisionNumber = await NextRevisionAsync(session, cancellationToken).ConfigureAwait(false);
        var draft = new PriceRevision(
            actor.TenantId,
            Guid.CreateVersion7(),
            revisionNumber,
            request.Key,
            request.BaseUnitPrice,
            request.Validity,
            PricePublicationState.Draft,
            actor.AccountId,
             now, priceId: request.PriceId ?? Guid.CreateVersion7());
        if (request.PriceId.HasValue)
        {
            await LockAsync(session, $"family|{actor.TenantId:D}|{request.PriceId:D}", cancellationToken).ConfigureAwait(false);
            await using var family = session.CreateCommand(PricingSql.FindFamily);
            family.Parameters.AddWithValue("tenant_id", actor.TenantId);
            family.Parameters.AddWithValue("price_id", request.PriceId.Value);
            var rows = await ReadRevisionsAsync(family, cancellationToken).ConfigureAwait(false);
            if (rows.Count == 0 || !rows[0].Key.HasSameDimensions(request.Key))
                throw new PricingValidationException("price_family_invalid", "Existing price family must match the immutable pricing key.");
        }
        await using (var insert = session.CreateCommand(PricingSql.InsertDraft))
        {
            AddRevisionParameters(insert, draft, actor.AccountId);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var responseJson = SerializeReceipt(CreatePriceDraftStatus.Created, draft);
        if (await TryInsertReceiptAsync(
                session,
                actor,
                CreateDraftOperation,
                idempotencyKey,
                request.Fingerprint,
                responseJson,
                now,
                cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(CreatePriceDraftStatus.Created, draft);
        }

        receipt = await FindReceiptAsync(
                session, actor, CreateDraftOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The pricing draft receipt disappeared after a conflict.");
        var replayAfterConflict = await ReplayCreateAsync(
                session, actor, receipt.Value, request.Fingerprint, cancellationToken)
            .ConfigureAwait(false);
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return replayAfterConflict;
    }

    public async Task<PublishPriceResult> PublishAsync(
        PricingActorContext actor,
        PublishPriceRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        RequireIdempotencyKey(idempotencyKey);
        if (request.RevisionId == Guid.Empty || request.SupersedeRevisionId == Guid.Empty)
            throw new ArgumentException("Price revision identity cannot be empty.", nameof(request));
        await using var session = await PricingTenantDbSession.OpenPublicationAsync(
                dataSource, actor.TenantId, cancellationToken)
             .ConfigureAwait(false);

        await LockAsync(session, $"receipt|{actor.TenantId:D}|{actor.AccountId:D}|{PublishOperation}|{idempotencyKey}", cancellationToken).ConfigureAwait(false);

        var receipt = await FindReceiptAsync(
                session, actor, PublishOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            var replay = await ReplayPublishAsync(
                    session, actor, receipt.Value, request.Fingerprint, cancellationToken)
                .ConfigureAwait(false);
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return replay;
        }

        var draft = await FindRevisionAsync(
                 session, actor.TenantId, request.RevisionId, forUpdate: false, cancellationToken)
             .ConfigureAwait(false);
        if (draft is not null)
        {
            await LockAsync(session, KeyLock(actor.TenantId, draft.Key), cancellationToken).ConfigureAwait(false);
            draft = await FindRevisionAsync(session, actor.TenantId, request.RevisionId, true, cancellationToken).ConfigureAwait(false);
            receipt = await FindReceiptAsync(session, actor, PublishOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (receipt.HasValue)
            {
                var replay = await ReplayPublishAsync(session, actor, receipt.Value, request.Fingerprint, cancellationToken).ConfigureAwait(false);
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return replay;
            }
        }
        if (draft is null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(PublishPriceStatus.NotFound, null);
        }
        if (draft.State != PricePublicationState.Draft)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(PublishPriceStatus.RevisionConflict, draft);
        }
        if (draft.Key.UnitId == Guid.Empty || draft.Key.Scope.Kind is PriceScopeKind.CommittedAgreement or PriceScopeKind.CommittedQuotation)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(PublishPriceStatus.RevisionConflict, draft);
        }

        try
        {
            if (request.SupersedeRevisionId.HasValue)
            {
                if (request.SupersedeRevisionId.Value == request.RevisionId)
                {
                    await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new(PublishPriceStatus.RevisionConflict, draft);
                }

                var superseded = await FindRevisionAsync(
                        session, actor.TenantId, request.SupersedeRevisionId.Value, forUpdate: true, cancellationToken)
                    .ConfigureAwait(false);
                if (superseded is null ||
                    superseded.State != PricePublicationState.Published ||
                    !superseded.Key.HasSameDimensions(draft.Key) || superseded.PriceId != draft.PriceId)
                {
                    await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new(PublishPriceStatus.RevisionConflict, draft);
                }

                await using var supersede = session.CreateCommand(PricingSql.SupersedeRevision);
                supersede.Parameters.AddWithValue("tenant_id", actor.TenantId);
                supersede.Parameters.AddWithValue("revision_id", superseded.RevisionId);
                await supersede.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var overlap = session.CreateCommand(PricingSql.FindOverlap))
            {
                AddKeyParameters(overlap, actor.TenantId, draft.Key, draft.Validity, draft.RevisionId);
                if (await overlap.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
                {
                    await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new(PublishPriceStatus.PublicationConflict, draft);
                }
            }

            var publishedAt = GetDatabaseInstant();
            await using (var publish = session.CreateCommand(PricingSql.PublishRevision))
            {
                publish.Parameters.AddWithValue("tenant_id", actor.TenantId);
                publish.Parameters.AddWithValue("revision_id", draft.RevisionId);
                publish.Parameters.AddWithValue("published_at", publishedAt);
                if (await publish.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                {
                    await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new(PublishPriceStatus.RevisionConflict, draft);
                }
            }

            var published = draft.Publish(publishedAt);
            var responseJson = SerializeReceipt(PublishPriceStatus.Published, published);
            if (await TryInsertReceiptAsync(
                    session,
                    actor,
                    PublishOperation,
                    idempotencyKey,
                    request.Fingerprint,
                    responseJson,
                    publishedAt,
                    cancellationToken).ConfigureAwait(false))
            {
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(PublishPriceStatus.Published, published);
            }

            receipt = await FindReceiptAsync(
                    session, actor, PublishOperation, idempotencyKey, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("The pricing publication receipt disappeared after a conflict.");
            var replayAfterConflict = await ReplayPublishAsync(
                    session, actor, receipt.Value, request.Fingerprint, cancellationToken)
                .ConfigureAwait(false);
            await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return replayAfterConflict;
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.ExclusionViolation ||
            exception.SqlState == PostgresErrorCodes.DeadlockDetected)
        {
            await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new(PublishPriceStatus.PublicationConflict, draft);
        }
    }

    public async Task<RetirePriceResult> RetireAsync(
        PricingActorContext actor,
        RetirePriceRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        RequireIdempotencyKey(idempotencyKey);
        if (request.RevisionId == Guid.Empty) throw new ArgumentException("Price revision identity cannot be empty.", nameof(request));
        await using var session = await PricingTenantDbSession.OpenPublicationAsync(
                dataSource, actor.TenantId, cancellationToken)
             .ConfigureAwait(false);
        await LockAsync(session, $"receipt|{actor.TenantId:D}|{actor.AccountId:D}|{RetireOperation}|{idempotencyKey}", cancellationToken).ConfigureAwait(false);
        var receipt = await FindReceiptAsync(
                session, actor, RetireOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            var replay = await ReplayRetireAsync(
                    session, actor, receipt.Value, request.Fingerprint, cancellationToken)
                .ConfigureAwait(false);
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return replay;
        }

        var revision = await FindRevisionAsync(
                 session, actor.TenantId, request.RevisionId, forUpdate: false, cancellationToken)
             .ConfigureAwait(false);
        if (revision is not null)
        {
            await LockAsync(session, KeyLock(actor.TenantId, revision.Key), cancellationToken).ConfigureAwait(false);
            revision = await FindRevisionAsync(session, actor.TenantId, request.RevisionId, true, cancellationToken).ConfigureAwait(false);
            receipt = await FindReceiptAsync(session, actor, RetireOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (receipt.HasValue)
            {
                var replay = await ReplayRetireAsync(session, actor, receipt.Value, request.Fingerprint, cancellationToken).ConfigureAwait(false);
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return replay;
            }
        }
        if (revision is null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(RetirePriceStatus.NotFound, null);
        }
        if (revision.State != PricePublicationState.Published)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(RetirePriceStatus.RevisionConflict, revision);
        }

        var retiredAt = GetDatabaseInstant();
        await using (var retire = session.CreateCommand(PricingSql.RetireRevision))
        {
            retire.Parameters.AddWithValue("tenant_id", actor.TenantId);
            retire.Parameters.AddWithValue("revision_id", request.RevisionId);
            retire.Parameters.AddWithValue("retired_at", retiredAt);
            if (await retire.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            {
                await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new(RetirePriceStatus.RevisionConflict, revision);
            }
        }

        var retired = revision.Retire(retiredAt);
        var responseJson = SerializeReceipt(RetirePriceStatus.Retired, retired);
        if (await TryInsertReceiptAsync(
                session,
                actor,
                RetireOperation,
                idempotencyKey,
                request.Fingerprint,
                responseJson,
                retiredAt,
                cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(RetirePriceStatus.Retired, retired);
        }

        receipt = await FindReceiptAsync(
                session, actor, RetireOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The pricing retirement receipt disappeared after a conflict.");
        var replayAfterConflict = await ReplayRetireAsync(
                session, actor, receipt.Value, request.Fingerprint, cancellationToken)
            .ConfigureAwait(false);
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return replayAfterConflict;
    }

    public async Task<IReadOnlyList<PriceRevision>> GetPublishedCandidatesAsync(
        PricingActorContext actor,
        PriceLookupRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        await using var session = await PricingTenantDbSession.OpenSnapshotAsync(
                dataSource, actor.TenantId, cancellationToken)
            .ConfigureAwait(false);
        await using var command = session.CreateCommand(PricingSql.FindCandidates);
        AddLookupParameters(command, actor.TenantId, request);
        command.Parameters.AddWithValue("at", request.At ?? _timeProvider.GetUtcNow());
        var revisions = await ReadRevisionsAsync(command, cancellationToken).ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return revisions;
    }

    public async Task<IReadOnlyList<PriceRevision>> ListRevisionsAsync(
        PricingActorContext actor,
        PriceLookupRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        await using var session = await PricingTenantDbSession.OpenSnapshotAsync(
                dataSource, actor.TenantId, cancellationToken)
            .ConfigureAwait(false);
        await using var command = session.CreateCommand(PricingSql.ListRevisions);
        AddLookupParameters(command, actor.TenantId, request);
        var revisions = await ReadRevisionsAsync(command, cancellationToken).ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return revisions;
    }

    private static async Task<long> NextRevisionAsync(
        PricingTenantDbSession session,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(PricingSql.NextRevision);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private static async Task<PriceRevision?> FindRevisionAsync(
        PricingTenantDbSession session,
        Guid tenantId,
        Guid revisionId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(
            forUpdate ? PricingSql.FindRevisionForUpdate : PricingSql.FindRevision);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("revision_id", revisionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadRevision(reader)
            : null;
    }

    private static async Task<Receipt?> FindReceiptAsync(
        PricingTenantDbSession session,
        PricingActorContext actor,
        string operation,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(PricingSql.FindReceipt);
        command.Parameters.AddWithValue("tenant_id", actor.TenantId);
        command.Parameters.AddWithValue("account_id", actor.AccountId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new Receipt(reader.GetString(0), reader.GetString(1))
            : null;
    }

    private static async Task<bool> TryInsertReceiptAsync(
        PricingTenantDbSession session,
        PricingActorContext actor,
        string operation,
        string idempotencyKey,
        string fingerprint,
        string responseJson,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(PricingSql.InsertReceipt);
        command.Parameters.AddWithValue("tenant_id", actor.TenantId);
        command.Parameters.AddWithValue("account_id", actor.AccountId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("response_json", responseJson);
        command.Parameters.AddWithValue("created_at", createdAt);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    private static Task<CreatePriceDraftResult> ReplayCreateAsync(
       PricingTenantDbSession session,
       PricingActorContext actor,
       Receipt receipt,
       string fingerprint,
       CancellationToken cancellationToken)
    {
        if (!string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal))
            return Task.FromResult(new CreatePriceDraftResult(CreatePriceDraftStatus.IdempotencyKeyConflict, null));
        var envelope = DeserializeReceipt<CreatePriceDraftStatus>(receipt.ResponseJson);
        return Task.FromResult(new CreatePriceDraftResult(CreatePriceDraftStatus.Replayed, envelope.Snapshot));
    }

    private static Task<PublishPriceResult> ReplayPublishAsync(
       PricingTenantDbSession session,
       PricingActorContext actor,
       Receipt receipt,
       string fingerprint,
       CancellationToken cancellationToken)
    {
        if (!string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal))
            return Task.FromResult(new PublishPriceResult(PublishPriceStatus.IdempotencyKeyConflict, null));
        var envelope = DeserializeReceipt<PublishPriceStatus>(receipt.ResponseJson);
        return Task.FromResult(new PublishPriceResult(PublishPriceStatus.Replayed, envelope.Snapshot));
    }

    private static Task<RetirePriceResult> ReplayRetireAsync(
       PricingTenantDbSession session,
       PricingActorContext actor,
       Receipt receipt,
       string fingerprint,
       CancellationToken cancellationToken)
    {
        if (!string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal))
            return Task.FromResult(new RetirePriceResult(RetirePriceStatus.IdempotencyKeyConflict, null));
        var envelope = DeserializeReceipt<RetirePriceStatus>(receipt.ResponseJson);
        return Task.FromResult(new RetirePriceResult(RetirePriceStatus.Replayed, envelope.Snapshot));
    }

    private static async Task<List<PriceRevision>> ReadRevisionsAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var revisions = new List<PriceRevision>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            revisions.Add(ReadRevision(reader));
        return revisions;
    }

    private static PriceRevision ReadRevision(DbDataReader reader)
    {
        var tenantId = reader.GetGuid(0);
        var revisionId = reader.GetGuid(1);
        var revisionNumber = reader.GetInt64(2);
        var itemId = reader.GetGuid(3);
        var unitCode = reader.GetString(4);
        var currencyCode = reader.GetString(5);
        var scopeKind = ParseScopeKind(reader.GetString(6));
        var scopeId = reader.GetGuid(7);
        var scope = new PriceScope(scopeKind, scopeId == Guid.Empty ? null : scopeId);
        var baseUnitPrice = reader.GetDecimal(8);
        var validFrom = reader.GetFieldValue<DateTimeOffset>(9);
        DateTimeOffset? validTo = reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10);
        var state = ParseState(reader.GetString(11));
        var createdByAccountId = reader.GetGuid(12);
        var createdAt = reader.GetFieldValue<DateTimeOffset>(13);
        DateTimeOffset? publishedAt = reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14);
        DateTimeOffset? retiredAt = reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15);
        return new PriceRevision(
            tenantId,
            revisionId,
            revisionNumber,
            new PriceKey(itemId, unitCode, currencyCode, scope, reader.IsDBNull(16) ? Guid.Empty : reader.GetGuid(16), reader.IsDBNull(18) ? null : reader.GetInt64(18)),
            baseUnitPrice,
            new PriceValidity(validFrom, validTo),
            state,
            createdByAccountId,
            createdAt,
            publishedAt,
             retiredAt, reader.GetGuid(17));
    }

    private static void AddRevisionParameters(NpgsqlCommand command, PriceRevision revision, Guid accountId)
    {
        command.Parameters.AddWithValue("tenant_id", revision.TenantId);
        command.Parameters.AddWithValue("revision_id", revision.RevisionId);
        command.Parameters.AddWithValue("price_id", revision.PriceId);
        command.Parameters.AddWithValue("unit_id", revision.Key.UnitId);
        command.Parameters.Add("unit_conversion_revision", NpgsqlDbType.Bigint).Value = (object?)revision.Key.UnitConversionRevision ?? DBNull.Value;
        command.Parameters.AddWithValue("revision_number", revision.RevisionNumber);
        command.Parameters.AddWithValue("item_id", revision.Key.ItemId);
        command.Parameters.AddWithValue("unit_code", revision.Key.UnitCode);
        command.Parameters.AddWithValue("currency_code", revision.Key.CurrencyCode);
        command.Parameters.AddWithValue("scope_kind", ScopeValue(revision.Key.Scope.Kind));
        command.Parameters.AddWithValue("scope_id", revision.Key.Scope.TargetId ?? Guid.Empty);
        command.Parameters.AddWithValue("base_unit_price", revision.BaseUnitPrice);
        command.Parameters.AddWithValue("valid_from", revision.Validity.ValidFrom);
        command.Parameters.Add("valid_to", NpgsqlDbType.TimestampTz).Value =
            (object?)revision.Validity.ValidTo ?? DBNull.Value;
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("created_at", revision.CreatedAt);
    }

    private static void AddKeyParameters(
        NpgsqlCommand command,
        Guid tenantId,
        PriceKey key,
        PriceValidity validity,
        Guid revisionId)
    {
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("item_id", key.ItemId);
        command.Parameters.AddWithValue("unit_code", key.UnitCode);
        command.Parameters.AddWithValue("unit_id", key.UnitId);
        command.Parameters.AddWithValue("currency_code", key.CurrencyCode);
        command.Parameters.AddWithValue("scope_kind", ScopeValue(key.Scope.Kind));
        command.Parameters.AddWithValue("scope_id", key.Scope.TargetId ?? Guid.Empty);
        command.Parameters.AddWithValue("revision_id", revisionId);
        command.Parameters.AddWithValue("valid_from", validity.ValidFrom);
        command.Parameters.Add("valid_to", NpgsqlDbType.TimestampTz).Value =
            (object?)validity.ValidTo ?? DBNull.Value;
    }

    private static void AddLookupParameters(
        NpgsqlCommand command,
        Guid tenantId,
        PriceLookupRequest request)
    {
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("item_id", request.ItemId);
        command.Parameters.AddWithValue("unit_code", request.UnitCode);
        if (request.UnitId == Guid.Empty) throw new ArgumentException("Stable unit identity is required.", nameof(request));
        PricingApplication.RequirePublicContext(request.Context);
        command.Parameters.AddWithValue("unit_id", request.UnitId);
        command.Parameters.Add("conversion_revision", NpgsqlDbType.Bigint).Value = (object?)request.Context.UnitConversionRevision ?? DBNull.Value;
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = (object?)request.Context.CustomerId ?? DBNull.Value;
        command.Parameters.Add("program_id", NpgsqlDbType.Uuid).Value = (object?)request.Context.ProgramId ?? DBNull.Value;
        command.Parameters.Add("organization_id", NpgsqlDbType.Uuid).Value = (object?)request.Context.OrganizationId ?? DBNull.Value;
        command.Parameters.AddWithValue("wholesale", request.Context.WholesaleApplicable);
        command.Parameters.AddWithValue("limit", request.Limit);
        command.Parameters.Add("after_revision", NpgsqlDbType.Bigint).Value = (object?)request.AfterRevision ?? DBNull.Value;
        command.Parameters.AddWithValue("currency_code", request.CurrencyCode);
    }

    private static string SerializeReceipt<TStatus>(TStatus status, PriceRevision snapshot) =>
         JsonSerializer.Serialize(new ReceiptEnvelope<TStatus>(2, status, snapshot), ReceiptJsonOptions);

    private static ReceiptEnvelope<TStatus> DeserializeReceipt<TStatus>(string json)
    {
        var receipt = JsonSerializer.Deserialize<ReceiptEnvelope<TStatus>>(json, ReceiptJsonOptions)
            ?? throw new InvalidOperationException("The pricing command receipt is malformed.");
        if (receipt.Version != 2 || receipt.Snapshot is null)
            throw new PricingValidationException("pricing_receipt_version_unsupported", "This retained receipt predates the immutable stable-unit contract.");
        return receipt;
    }

    private static string ScopeValue(PriceScopeKind kind) => kind switch
    {
        PriceScopeKind.CommittedQuotation => "committed_quotation",
        PriceScopeKind.CommittedAgreement => "committed_agreement",
        PriceScopeKind.Customer => "customer",
        PriceScopeKind.Program => "program",
        PriceScopeKind.Organization => "organization",
        PriceScopeKind.Wholesale => "wholesale",
        PriceScopeKind.Default => "default",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static PriceScopeKind ParseScopeKind(string value) => value switch
    {
        "committed_quotation" => PriceScopeKind.CommittedQuotation,
        "committed_agreement" => PriceScopeKind.CommittedAgreement,
        "customer" => PriceScopeKind.Customer,
        "program" => PriceScopeKind.Program,
        "organization" => PriceScopeKind.Organization,
        "wholesale" => PriceScopeKind.Wholesale,
        "default" => PriceScopeKind.Default,
        _ => throw new InvalidOperationException("The pricing scope kind is not supported."),
    };

    private static PricePublicationState ParseState(string value) => value switch
    {
        "draft" => PricePublicationState.Draft,
        "published" => PricePublicationState.Published,
        "superseded" => PricePublicationState.Superseded,
        "retired" => PricePublicationState.Retired,
        _ => throw new InvalidOperationException("The pricing publication state is not supported."),
    };

    private static void RequireIdempotencyKey(string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (idempotencyKey.Length > 128 || idempotencyKey.Any(char.IsControl))
            throw new ArgumentException("The pricing idempotency key cannot exceed 128 characters.", nameof(idempotencyKey));
    }

    private readonly record struct Receipt(string Fingerprint, string ResponseJson);

    private sealed record ReceiptEnvelope<TStatus>(int Version, TStatus Status, PriceRevision Snapshot);

    private static void RequireDraft(CreatePriceDraftRequest request)
    {
        PricingApplication.RequireBookScope(request.Scope);
        if (request.Validity.ValidFrom == default || request.Validity.ValidFrom.Ticks % 10 != 0 ||
            (request.Validity.ValidTo.HasValue && request.Validity.ValidTo.Value.Ticks % 10 != 0))
            throw new PricingValidationException("pricing_validity_invalid", "Validity requires explicit microsecond-precision timestamps.");
        if (request.UnitId == Guid.Empty || request.PriceId == Guid.Empty)
            throw new PricingValidationException("unit_id_invalid", "Stable unit and price family identities are required.");
        _ = request.Key;
        if (request.BaseUnitPrice < 0 || request.BaseUnitPrice > 999999999999999.9999m || decimal.Round(request.BaseUnitPrice, 4) != request.BaseUnitPrice)
            throw new PricingValidationException("base_unit_price_invalid", "Price must be a nonnegative decimal(19,4) value.");
    }

    private DateTimeOffset GetDatabaseInstant()
    {
        var utc = _timeProvider.GetUtcNow().ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }

    private static string KeyLock(Guid tenant, PriceKey key) =>
        $"price-key|{tenant:D}|{key.ItemId:D}|{key.UnitId:D}|{key.CurrencyCode}|{key.Scope.Kind}|{key.Scope.TargetId:D}";

    private static async Task LockAsync(PricingTenantDbSession session, string key, CancellationToken ct)
    {
        await using var command = session.CreateCommand(PricingSql.Lock);
        command.Parameters.AddWithValue("lock_key", key);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<PriceRevision?> FindAsync(PricingActorContext actor, Guid revisionId, CancellationToken cancellationToken)
    {
        if (revisionId == Guid.Empty) throw new ArgumentException("Revision identity is required.", nameof(revisionId));
        await using var session = await PricingTenantDbSession.OpenSnapshotAsync(dataSource, actor.TenantId, cancellationToken).ConfigureAwait(false);
        var result = await FindRevisionAsync(session, actor.TenantId, revisionId, false, cancellationToken).ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
