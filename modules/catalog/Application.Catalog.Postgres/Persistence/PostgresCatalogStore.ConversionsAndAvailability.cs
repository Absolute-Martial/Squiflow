using Application.Tenancy;
using Npgsql;

namespace Application.Catalog.Postgres;

public sealed partial class PostgresCatalogStore
{
    private const string PublishConversionOperation = "publish-catalog-conversion";
    private const string ChangeAvailabilityOperation = "change-catalog-availability";

    public async Task<PublishCatalogConversionResult> PublishConversionAsync(TenantContext context, CatalogConversionIntent intent,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(intent);
        idempotencyKey = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        var canonical = CatalogConversionIntent.Create(new PublishCatalogConversionRequest(intent.SourceUnitId,
            intent.TargetUnitId, intent.ExpectedRevision, intent.Numerator, intent.Denominator));
        if (intent != canonical) throw new CatalogValidationException("intent_invalid", "The conversion intent must be canonical.");

        await using var session = await CatalogTenantDbSession.OpenPublicationAsync(_dataSource, context.TenantId, cancellationToken)
            .ConfigureAwait(false);
        var receipt = await FindReceiptAsync(session, context, PublishConversionOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
            return receipt.Value.Fingerprint == intent.Fingerprint
                ? new(PublishCatalogConversionStatus.Replayed, DeserializeSnapshot<CatalogConversionSnapshot>(receipt.Value.ResponseJson))
                : new(PublishCatalogConversionStatus.IdempotencyKeyConflict, null);

        await LockScopeAsync(session, $"catalog-pair:{context.TenantId:N}:{intent.SourceUnitId:N}:{intent.TargetUnitId:N}", cancellationToken)
            .ConfigureAwait(false);
        foreach (var unitId in new[] { intent.SourceUnitId, intent.TargetUnitId }.Order())
        {
            var unit = await ReadUnitAsync(session, context.TenantId, unitId, cancellationToken, forSelection: true)
                .ConfigureAwait(false);
            if (unit is null) return new(PublishCatalogConversionStatus.UnitNotFound, null);
            if (unit.Status == CatalogEntityStatus.Retired) return new(PublishCatalogConversionStatus.UnitRetired, null);
        }
        await using (var latest = session.CreateCommand(CatalogSql.FindConversionRevision))
        {
            AddConversionPairParameters(latest, context.TenantId, intent.SourceUnitId, intent.TargetUnitId);
            var revision = (long)(await latest.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The catalog conversion revision is missing."));
            if (revision != intent.ExpectedRevision) return new(PublishCatalogConversionStatus.RevisionConflict, null);
        }

        var conversion = new CatalogConversionSnapshot(context.TenantId, intent.SourceUnitId, intent.TargetUnitId,
            intent.ExpectedRevision + 1, intent.Numerator, intent.Denominator, context.AccountId, _timeProvider.GetUtcNow());
        await using (var insert = session.CreateCommand(CatalogSql.InsertConversion))
        {
            AddConversionPairParameters(insert, context.TenantId, intent.SourceUnitId, intent.TargetUnitId);
            insert.Parameters.AddWithValue("revision", conversion.Revision);
            insert.Parameters.AddWithValue("numerator", conversion.Numerator);
            insert.Parameters.AddWithValue("denominator", conversion.Denominator);
            insert.Parameters.AddWithValue("account_id", context.AccountId);
            insert.Parameters.AddWithValue("published_at", conversion.PublishedAt);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        if (!await TryInsertReceiptAsync(session, context, PublishConversionOperation, idempotencyKey, intent.Fingerprint,
            SerializeReceipt((int)PublishCatalogConversionStatus.Published, conversion), conversion.PublishedAt, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("The serialized catalog conversion command lost its receipt.");
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(PublishCatalogConversionStatus.Published, conversion);
    }

    public async Task<CatalogConversionSnapshot?> FindConversionAsync(TenantContext context, Guid sourceUnitId, Guid targetUnitId,
        long revision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        CatalogRules.RequireIdentity(sourceUnitId, "source_unit_id_invalid");
        CatalogRules.RequireIdentity(targetUnitId, "target_unit_id_invalid");
        CatalogRules.RequireRevision(revision);
        await using var session = await CatalogTenantDbSession.OpenAsync(_dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        var conversion = await ReadConversionAsync(session, context.TenantId, sourceUnitId, targetUnitId, revision, cancellationToken)
            .ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return conversion;
    }

    public async Task<ChangeCatalogAvailabilityResult> ChangeAvailabilityAsync(TenantContext context,
        ChangeCatalogAvailabilityRequest request, string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequireIdentity(request.ItemId, "item_id_invalid");
        CatalogRules.RequireRevision(request.ExpectedRevision);
        if (!Enum.IsDefined(request.Availability)) throw new CatalogValidationException("availability_invalid", "Availability is unsupported.");
        idempotencyKey = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        RequireFingerprint(fingerprint);
        await using var session = await CatalogTenantDbSession.OpenPublicationAsync(_dataSource, context.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindReceiptAsync(session, context, ChangeAvailabilityOperation, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
            return receipt.Value.Fingerprint == fingerprint
                ? new(ChangeCatalogAvailabilityStatus.Replayed, DeserializeSnapshot<CatalogItemSnapshot>(receipt.Value.ResponseJson))
                : new(ChangeCatalogAvailabilityStatus.IdempotencyKeyConflict, null);

        var changedAt = _timeProvider.GetUtcNow();
        await using (var update = session.CreateCommand(CatalogSql.ChangeAvailability))
        {
            update.Parameters.AddWithValue("tenant_id", context.TenantId);
            update.Parameters.AddWithValue("item_id", request.ItemId);
            update.Parameters.AddWithValue("expected_revision", request.ExpectedRevision);
            update.Parameters.AddWithValue("availability", WireAvailability(request.Availability));
            update.Parameters.AddWithValue("account_id", context.AccountId);
            update.Parameters.AddWithValue("changed_at", changedAt);
            if (await update.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                var current = await ReadItemAsync(session, context.TenantId, request.ItemId, cancellationToken).ConfigureAwait(false);
                var status = current switch
                {
                    null => ChangeCatalogAvailabilityStatus.NotFound,
                    { Status: CatalogEntityStatus.Retired } => ChangeCatalogAvailabilityStatus.AlreadyRetired,
                    { StockMode: not CatalogStockMode.AvailabilityOnly } => ChangeCatalogAvailabilityStatus.StockModeMismatch,
                    _ when current.Revision != request.ExpectedRevision => ChangeCatalogAvailabilityStatus.RevisionConflict,
                    _ => ChangeCatalogAvailabilityStatus.AlreadyInState,
                };
                return new(status, null);
            }
        }
        var item = await ReadItemAsync(session, context.TenantId, request.ItemId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The changed catalog item disappeared.");
        if (!await TryInsertReceiptAsync(session, context, ChangeAvailabilityOperation, idempotencyKey, fingerprint,
            SerializeReceipt((int)ChangeCatalogAvailabilityStatus.Changed, item), changedAt, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("The serialized catalog availability command lost its receipt.");
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(ChangeCatalogAvailabilityStatus.Changed, item);
    }

    private static async Task<CatalogConversionSnapshot?> ReadConversionAsync(CatalogTenantDbSession session, Guid tenantId,
        Guid sourceUnitId, Guid targetUnitId, long revision, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CatalogSql.FindConversion);
        AddConversionPairParameters(command, tenantId, sourceUnitId, targetUnitId);
        command.Parameters.AddWithValue("revision", revision);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new(tenantId, reader.GetGuid(0), reader.GetGuid(1), reader.GetInt64(2), reader.GetDecimal(3), reader.GetDecimal(4),
                reader.GetGuid(5), reader.GetFieldValue<DateTimeOffset>(6))
            : null;
    }

    private static void AddConversionPairParameters(NpgsqlCommand command, Guid tenantId, Guid sourceUnitId, Guid targetUnitId)
    {
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("source_unit_id", sourceUnitId);
        command.Parameters.AddWithValue("target_unit_id", targetUnitId);
    }

    private static async Task LockScopeAsync(CatalogTenantDbSession session, string scope, CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CatalogSql.LockScope);
        command.Parameters.AddWithValue("scope", scope);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void RequireFingerprint(string fingerprint)
    {
        if (fingerprint is null || fingerprint.Length != 64 || fingerprint.Any(character => !char.IsAsciiHexDigitLower(character)))
            throw new CatalogValidationException("fingerprint_invalid", "The command fingerprint is invalid.");
    }

    private static string WireAvailability(CatalogAvailability availability) => availability switch
    {
        CatalogAvailability.Available => "available",
        CatalogAvailability.Unavailable => "unavailable",
        _ => throw new InvalidOperationException("Catalog availability is unsupported."),
    };

    private static CatalogAvailability ParseAvailability(string availability) => availability switch
    {
        "available" => CatalogAvailability.Available,
        "unavailable" => CatalogAvailability.Unavailable,
        _ => throw new InvalidOperationException("The catalog database returned unsupported availability."),
    };
}
