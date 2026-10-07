using System.Text.Json;
using Application.Catalog;
using Application.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace Application.Catalog.Postgres;

public sealed partial class PostgresCatalogStore(
    NpgsqlDataSource dataSource,
    TimeProvider? timeProvider = null) : ICatalogStore, ICatalogConversionStore, ICatalogAvailabilityStore
{
    private const string CreateUnitOperation = "create-catalog-unit";
    private const string CreateItemOperation = "create-catalog-item";
    private const string RenameUnitOperation = "rename-catalog-unit";
    private const string RenameItemOperation = "rename-catalog-item";
    private const string RetireUnitOperation = "retire-catalog-unit";
    private const string RetireItemOperation = "retire-catalog-item";
    private static readonly JsonSerializerOptions ReceiptJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly NpgsqlDataSource _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<CreateCatalogUnitResult> CreateUnitAsync(
        TenantContext tenantContext,
        CatalogUnitIntent intent,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(intent);
        idempotencyKey = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        if (intent != CatalogUnitIntent.Create(new CreateCatalogUnitRequest(intent.Code, intent.Name, intent.Precision)))
            throw new CatalogValidationException("intent_invalid", "The unit intent must be canonical.");
        await using var session = await CatalogTenantDbSession.OpenPublicationAsync(
            _dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);

        var receipt = await FindReceiptAsync(session, tenantContext, CreateUnitOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ReplayCreateUnit(receipt.Value, intent.Fingerprint);
        }

        var unit = new CatalogUnitSnapshot(
            Guid.CreateVersion7(), tenantContext.TenantId, intent.Code, intent.Name, intent.Precision,
            CatalogEntityStatus.Active, 1, tenantContext.AccountId, _timeProvider.GetUtcNow());
        try
        {
            await using var insert = session.CreateCommand(CatalogSql.InsertUnit);
            AddUnitParameters(insert, unit, tenantContext.AccountId);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation &&
                                                  exception.ConstraintName == "ux_catalog_units_tenant_code")
        {
            await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new(CreateCatalogUnitStatus.CodeConflict, null);
        }

        var responseJson = SerializeReceipt((int)CreateCatalogUnitStatus.Created, unit);
        if (await TryInsertReceiptAsync(
                session, tenantContext, CreateUnitOperation, idempotencyKey, intent.Fingerprint,
                responseJson, unit.CreatedAt, cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(CreateCatalogUnitStatus.Created, unit);
        }

        receipt = await FindReceiptAsync(session, tenantContext, CreateUnitOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The catalog unit receipt disappeared after a conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ReplayCreateUnit(receipt.Value, intent.Fingerprint);
    }

    public async Task<CreateCatalogItemResult> CreateItemAsync(
        TenantContext tenantContext,
        CatalogItemIntent intent,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(intent);
        idempotencyKey = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        if (intent != CatalogItemIntent.Create(new CreateCatalogItemRequest(intent.Code, intent.Name, intent.Description, intent.Kind, intent.BaseUnitId, intent.StockMode)))
            throw new CatalogValidationException("intent_invalid", "The item intent must be canonical.");
        await using var session = await CatalogTenantDbSession.OpenPublicationAsync(
            _dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);

        var receipt = await FindReceiptAsync(session, tenantContext, CreateItemOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ReplayCreateItem(receipt.Value, intent.Fingerprint);
        }

        var baseUnit = await ReadUnitAsync(session, tenantContext.TenantId, intent.BaseUnitId, cancellationToken, forSelection: true)
            .ConfigureAwait(false);
        if (baseUnit is null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(CreateCatalogItemStatus.BaseUnitNotFound, null);
        }

        if (baseUnit.Status == CatalogEntityStatus.Retired)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(CreateCatalogItemStatus.BaseUnitRetired, null);
        }

        var item = new CatalogItemSnapshot(
            Guid.CreateVersion7(), tenantContext.TenantId, intent.Code, intent.Name, intent.Description,
            intent.Kind, CatalogEntityStatus.Active, intent.BaseUnitId, intent.StockMode, 1,
            tenantContext.AccountId, _timeProvider.GetUtcNow(), Availability:
                intent.StockMode == CatalogStockMode.AvailabilityOnly ? CatalogAvailability.Unavailable : null);
        try
        {
            await using var insert = session.CreateCommand(CatalogSql.InsertItem);
            AddItemParameters(insert, item, tenantContext.AccountId);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation &&
                                                  exception.ConstraintName == "ux_catalog_items_tenant_code")
        {
            await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new(CreateCatalogItemStatus.CodeConflict, null);
        }

        var responseJson = SerializeReceipt((int)CreateCatalogItemStatus.Created, item);
        if (await TryInsertReceiptAsync(
                session, tenantContext, CreateItemOperation, idempotencyKey, intent.Fingerprint,
                responseJson, item.CreatedAt, cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(CreateCatalogItemStatus.Created, item);
        }

        receipt = await FindReceiptAsync(session, tenantContext, CreateItemOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The catalog item receipt disappeared after a conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ReplayCreateItem(receipt.Value, intent.Fingerprint);
    }

    public async Task<CatalogUnitSnapshot?> FindUnitAsync(
        TenantContext tenantContext,
        Guid unitId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        CatalogRules.RequireIdentity(unitId, "unit_id_invalid");
        await using var session = await CatalogTenantDbSession.OpenAsync(
            _dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        var unit = await ReadUnitAsync(session, tenantContext.TenantId, unitId, cancellationToken).ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return unit;
    }

    public async Task<CatalogItemSnapshot?> FindItemAsync(
        TenantContext tenantContext,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        CatalogRules.RequireIdentity(itemId, "item_id_invalid");
        await using var session = await CatalogTenantDbSession.OpenAsync(
            _dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        var item = await ReadItemAsync(session, tenantContext.TenantId, itemId, cancellationToken).ConfigureAwait(false);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return item;
    }

    public async Task<CatalogUnitPage> ListUnitsAsync(
        TenantContext tenantContext,
        ListCatalogUnitsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequirePage(request.Limit);
        CatalogRules.RequireCursor(request.After?.CreatedAt, request.After?.UnitId, "unit_cursor_invalid");
        await using var session = await CatalogTenantDbSession.OpenSnapshotAsync(
            _dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        await using var command = session.CreateCommand(CatalogSql.ListUnits);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.Add("after_at", NpgsqlDbType.TimestampTz).Value =
            (object?)request.After?.CreatedAt ?? DBNull.Value;
        command.Parameters.Add("after_id", NpgsqlDbType.Uuid).Value =
            (object?)request.After?.UnitId ?? DBNull.Value;
        command.Parameters.AddWithValue("limit", request.Limit + 1);
        var units = new List<CatalogUnitSnapshot>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                units.Add(ReadUnit(reader, tenantContext.TenantId));
        }
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        var hasMore = units.Count > request.Limit;
        if (hasMore)
            units.RemoveAt(units.Count - 1);
        var last = hasMore ? units[^1] : null;
        return new(units, last is null ? null : new(last.CreatedAt, last.UnitId));
    }

    public async Task<CatalogItemPage> ListItemsAsync(
        TenantContext tenantContext,
        ListCatalogItemsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequirePage(request.Limit);
        CatalogRules.RequireCursor(request.After?.CreatedAt, request.After?.ItemId, "item_cursor_invalid");
        await using var session = await CatalogTenantDbSession.OpenSnapshotAsync(
            _dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        await using var command = session.CreateCommand(CatalogSql.ListItems);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.Add("after_at", NpgsqlDbType.TimestampTz).Value =
            (object?)request.After?.CreatedAt ?? DBNull.Value;
        command.Parameters.Add("after_id", NpgsqlDbType.Uuid).Value =
            (object?)request.After?.ItemId ?? DBNull.Value;
        command.Parameters.AddWithValue("limit", request.Limit + 1);
        var items = new List<CatalogItemSnapshot>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                items.Add(ReadItem(reader, tenantContext.TenantId));
        }
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        var hasMore = items.Count > request.Limit;
        if (hasMore)
            items.RemoveAt(items.Count - 1);
        var last = hasMore ? items[^1] : null;
        return new(items, last is null ? null : new(last.CreatedAt, last.ItemId));
    }

    public Task<RenameCatalogUnitResult> RenameUnitAsync(
        TenantContext tenantContext,
        RenameCatalogUnitRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken) =>
        RenameUnitCoreAsync(tenantContext, request, idempotencyKey, fingerprint, cancellationToken);

    public Task<RenameCatalogItemResult> RenameItemAsync(
        TenantContext tenantContext,
        RenameCatalogItemRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken) =>
        RenameItemCoreAsync(tenantContext, request, idempotencyKey, fingerprint, cancellationToken);

    public Task<RetireCatalogUnitResult> RetireUnitAsync(
        TenantContext tenantContext,
        RetireCatalogUnitRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken) =>
        RetireUnitCoreAsync(tenantContext, request, idempotencyKey, fingerprint, cancellationToken);

    public Task<RetireCatalogItemResult> RetireItemAsync(
        TenantContext tenantContext,
        RetireCatalogItemRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken) =>
        RetireItemCoreAsync(tenantContext, request, idempotencyKey, fingerprint, cancellationToken);

    public async Task<CatalogLineFactsResult> SelectLineFactsAsync(
        TenantContext tenantContext,
        CatalogLineSelection selection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(selection);
        CatalogRules.RequireIdentity(selection.ItemId, "item_id_invalid");
        CatalogRules.RequireIdentity(selection.UnitId, "unit_id_invalid");
        CatalogRules.RequireQuantity(selection.Quantity, 9);
        if (selection.ConversionRevision is { } revision) CatalogRules.RequireRevision(revision);
        await using var session = await CatalogTenantDbSession.OpenAsync(
            _dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        var item = await ReadItemAsync(session, tenantContext.TenantId, selection.ItemId, cancellationToken, forSelection: true).ConfigureAwait(false);
        if (item is null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(CatalogLineFactsStatus.ItemNotFound, null);
        }
        // Hold item availability/retirement and both unit meanings stable until
        // this selection transaction completes; lock units in canonical order.
        var units = new Dictionary<Guid, CatalogUnitSnapshot?>();
        foreach (var unitId in new[] { selection.UnitId, item.BaseUnitId }.Distinct().Order())
            units[unitId] = await ReadUnitAsync(session, tenantContext.TenantId, unitId, cancellationToken, forSelection: true)
                .ConfigureAwait(false);
        var conversion = selection.ConversionRevision is { } selectedRevision && selection.UnitId != item.BaseUnitId
            ? await ReadConversionAsync(session, tenantContext.TenantId, selection.UnitId, item.BaseUnitId, selectedRevision, cancellationToken)
                .ConfigureAwait(false)
            : null;
        var result = CatalogLineFactSelection.Assess(selection, item, units[selection.UnitId], units[item.BaseUnitId], conversion);
        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<RenameCatalogUnitResult> RenameUnitCoreAsync(
        TenantContext tenantContext,
        RenameCatalogUnitRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequireIdentity(request.UnitId, "unit_id_invalid");
        CatalogRules.RequireRevision(request.ExpectedRevision);
        request = request with { Name = CatalogRules.NormalizeName(request.Name, "unit_name_invalid") };
        idempotencyKey = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        RequireFingerprint(fingerprint);
        await using var session = await CatalogTenantDbSession.OpenPublicationAsync(
            _dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindReceiptAsync(session, tenantContext, RenameUnitOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ReplayRenameUnit(receipt.Value, fingerprint);
        }

        await using (var update = session.CreateCommand(CatalogSql.RenameUnit))
        {
            update.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
            update.Parameters.AddWithValue("unit_id", request.UnitId);
            update.Parameters.AddWithValue("expected_revision", request.ExpectedRevision);
            update.Parameters.AddWithValue("name", request.Name);
            if (await update.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                var current = await ReadUnitAsync(session, tenantContext.TenantId, request.UnitId, cancellationToken).ConfigureAwait(false);
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return current is null
                    ? new(RenameCatalogUnitStatus.NotFound, null)
                    : current.Status == CatalogEntityStatus.Retired
                        ? new(RenameCatalogUnitStatus.AlreadyRetired, current)
                        : new(RenameCatalogUnitStatus.RevisionConflict, current);
            }
        }
        var renamed = await ReadUnitAsync(session, tenantContext.TenantId, request.UnitId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The renamed catalog unit disappeared before receipt creation.");
        var responseJson = SerializeReceipt((int)RenameCatalogUnitStatus.Renamed, renamed);
        if (await TryInsertReceiptAsync(session, tenantContext, RenameUnitOperation, idempotencyKey, fingerprint,
                responseJson, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(RenameCatalogUnitStatus.Renamed, renamed);
        }
        receipt = await FindReceiptAsync(session, tenantContext, RenameUnitOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The catalog unit receipt disappeared after a conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ReplayRenameUnit(receipt.Value, fingerprint);
    }

    private async Task<RenameCatalogItemResult> RenameItemCoreAsync(
        TenantContext tenantContext,
        RenameCatalogItemRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequireIdentity(request.ItemId, "item_id_invalid");
        CatalogRules.RequireRevision(request.ExpectedRevision);
        request = request with
        {
            Name = CatalogRules.NormalizeName(request.Name, "item_name_invalid"),
            Description = CatalogRules.NormalizeOptionalDescription(request.Description)
        };
        idempotencyKey = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        RequireFingerprint(fingerprint);
        await using var session = await CatalogTenantDbSession.OpenPublicationAsync(
            _dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindReceiptAsync(session, tenantContext, RenameItemOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ReplayRenameItem(receipt.Value, fingerprint);
        }

        await using (var update = session.CreateCommand(CatalogSql.RenameItem))
        {
            update.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
            update.Parameters.AddWithValue("item_id", request.ItemId);
            update.Parameters.AddWithValue("expected_revision", request.ExpectedRevision);
            update.Parameters.AddWithValue("name", request.Name);
            update.Parameters.Add("description", NpgsqlDbType.Varchar).Value = (object?)request.Description ?? DBNull.Value;
            if (await update.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                var current = await ReadItemAsync(session, tenantContext.TenantId, request.ItemId, cancellationToken).ConfigureAwait(false);
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return current is null
                    ? new(RenameCatalogItemStatus.NotFound, null)
                    : current.Status == CatalogEntityStatus.Retired
                        ? new(RenameCatalogItemStatus.AlreadyRetired, current)
                        : new(RenameCatalogItemStatus.RevisionConflict, current);
            }
        }
        var renamed = await ReadItemAsync(session, tenantContext.TenantId, request.ItemId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The renamed catalog item disappeared before receipt creation.");
        var responseJson = SerializeReceipt((int)RenameCatalogItemStatus.Renamed, renamed);
        if (await TryInsertReceiptAsync(session, tenantContext, RenameItemOperation, idempotencyKey, fingerprint,
                responseJson, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(RenameCatalogItemStatus.Renamed, renamed);
        }
        receipt = await FindReceiptAsync(session, tenantContext, RenameItemOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The catalog item receipt disappeared after a conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ReplayRenameItem(receipt.Value, fingerprint);
    }

    private async Task<RetireCatalogUnitResult> RetireUnitCoreAsync(
        TenantContext tenantContext,
        RetireCatalogUnitRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequireIdentity(request.UnitId, "unit_id_invalid");
        CatalogRules.RequireRevision(request.ExpectedRevision);
        idempotencyKey = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        RequireFingerprint(fingerprint);
        await using var session = await CatalogTenantDbSession.OpenPublicationAsync(
            _dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindReceiptAsync(session, tenantContext, RetireUnitOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ReplayRetireUnit(receipt.Value, fingerprint);
        }

        var retiredAt = _timeProvider.GetUtcNow();
        await using (var update = session.CreateCommand(CatalogSql.RetireUnit))
        {
            update.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
            update.Parameters.AddWithValue("unit_id", request.UnitId);
            update.Parameters.AddWithValue("expected_revision", request.ExpectedRevision);
            update.Parameters.AddWithValue("retired_at", retiredAt);
            update.Parameters.AddWithValue("account_id", tenantContext.AccountId);
            if (await update.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                var current = await ReadUnitAsync(session, tenantContext.TenantId, request.UnitId, cancellationToken).ConfigureAwait(false);
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return current is null
                    ? new(RetireCatalogUnitStatus.NotFound, null)
                    : current.Status == CatalogEntityStatus.Retired
                        ? new(RetireCatalogUnitStatus.AlreadyRetired, current)
                        : new(RetireCatalogUnitStatus.RevisionConflict, current);
            }
        }
        var retired = await ReadUnitAsync(session, tenantContext.TenantId, request.UnitId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The retired catalog unit disappeared before receipt creation.");
        var responseJson = SerializeReceipt((int)RetireCatalogUnitStatus.Retired, retired);
        if (await TryInsertReceiptAsync(session, tenantContext, RetireUnitOperation, idempotencyKey, fingerprint,
                responseJson, retiredAt, cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(RetireCatalogUnitStatus.Retired, retired);
        }
        receipt = await FindReceiptAsync(session, tenantContext, RetireUnitOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The retired catalog unit receipt disappeared after a conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ReplayRetireUnit(receipt.Value, fingerprint);
    }

    private async Task<RetireCatalogItemResult> RetireItemCoreAsync(
        TenantContext tenantContext,
        RetireCatalogItemRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequireIdentity(request.ItemId, "item_id_invalid");
        CatalogRules.RequireRevision(request.ExpectedRevision);
        idempotencyKey = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        RequireFingerprint(fingerprint);
        await using var session = await CatalogTenantDbSession.OpenPublicationAsync(
            _dataSource, tenantContext.TenantId, cancellationToken).ConfigureAwait(false);
        var receipt = await FindReceiptAsync(session, tenantContext, RetireItemOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ReplayRetireItem(receipt.Value, fingerprint);
        }

        var retiredAt = _timeProvider.GetUtcNow();
        await using (var update = session.CreateCommand(CatalogSql.RetireItem))
        {
            update.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
            update.Parameters.AddWithValue("item_id", request.ItemId);
            update.Parameters.AddWithValue("expected_revision", request.ExpectedRevision);
            update.Parameters.AddWithValue("retired_at", retiredAt);
            update.Parameters.AddWithValue("account_id", tenantContext.AccountId);
            if (await update.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                var current = await ReadItemAsync(session, tenantContext.TenantId, request.ItemId, cancellationToken).ConfigureAwait(false);
                await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                return current is null
                    ? new(RetireCatalogItemStatus.NotFound, null)
                    : current.Status == CatalogEntityStatus.Retired
                        ? new(RetireCatalogItemStatus.AlreadyRetired, current)
                        : new(RetireCatalogItemStatus.RevisionConflict, current);
            }
        }
        var retired = await ReadItemAsync(session, tenantContext.TenantId, request.ItemId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The retired catalog item disappeared before receipt creation.");
        var responseJson = SerializeReceipt((int)RetireCatalogItemStatus.Retired, retired);
        if (await TryInsertReceiptAsync(session, tenantContext, RetireItemOperation, idempotencyKey, fingerprint,
                responseJson, retiredAt, cancellationToken).ConfigureAwait(false))
        {
            await session.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(RetireCatalogItemStatus.Retired, retired);
        }
        receipt = await FindReceiptAsync(session, tenantContext, RetireItemOperation, idempotencyKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The retired catalog item receipt disappeared after a conflict.");
        await session.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return ReplayRetireItem(receipt.Value, fingerprint);
    }

    private static async Task<CatalogUnitSnapshot?> ReadUnitAsync(
        CatalogTenantDbSession session,
        Guid tenantId,
        Guid unitId,
        CancellationToken cancellationToken, bool forSelection = false)
    {
        await using var command = session.CreateCommand(forSelection ? CatalogSql.FindUnitForSelection : CatalogSql.FindUnit);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("unit_id", unitId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadUnit(reader, tenantId) : null;
    }

    private static async Task<CatalogItemSnapshot?> ReadItemAsync(
        CatalogTenantDbSession session,
        Guid tenantId,
        Guid itemId,
        CancellationToken cancellationToken, bool forSelection = false)
    {
        await using var command = session.CreateCommand(forSelection ? CatalogSql.FindItemForSelection : CatalogSql.FindItem);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("item_id", itemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadItem(reader, tenantId) : null;
    }

    private static CatalogUnitSnapshot ReadUnit(NpgsqlDataReader reader, Guid tenantId) =>
        new(
            reader.GetGuid(0), tenantId, reader.GetString(1), reader.GetString(2), reader.GetInt16(3),
            ParseStatus(reader.GetString(4)), reader.GetInt64(5), reader.GetGuid(6), reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8), reader.IsDBNull(9) ? null : reader.GetGuid(9));

    private static CatalogItemSnapshot ReadItem(NpgsqlDataReader reader, Guid tenantId) =>
        new(
            reader.GetGuid(0), tenantId, reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3), ParseKind(reader.GetString(4)), ParseStatus(reader.GetString(5)),
            reader.GetGuid(6), ParseStockMode(reader.GetString(7)), reader.GetInt64(8), reader.GetGuid(9),
            reader.GetFieldValue<DateTimeOffset>(10), reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
            reader.IsDBNull(12) ? null : reader.GetGuid(12),
            reader.IsDBNull(13) ? null : ParseAvailability(reader.GetString(13)),
            reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
            reader.IsDBNull(15) ? null : reader.GetGuid(15));

    private static CatalogEntityStatus ParseStatus(string value) => value switch
    {
        "active" => CatalogEntityStatus.Active,
        "retired" => CatalogEntityStatus.Retired,
        _ => throw new InvalidOperationException("The catalog database returned an unsupported status.")
    };

    private static CatalogItemKind ParseKind(string value) => value switch
    {
        "product" => CatalogItemKind.Product,
        "service" => CatalogItemKind.Service,
        _ => throw new InvalidOperationException("The catalog database returned an unsupported item kind.")
    };

    private static CatalogStockMode ParseStockMode(string value) => value switch
    {
        "precise_stock" => CatalogStockMode.PreciseStock,
        "availability_only" => CatalogStockMode.AvailabilityOnly,
        "non_stock" => CatalogStockMode.NonStock,
        _ => throw new InvalidOperationException("The catalog database returned an unsupported stock mode.")
    };

    private static void AddUnitParameters(NpgsqlCommand command, CatalogUnitSnapshot unit, Guid accountId)
    {
        command.Parameters.AddWithValue("tenant_id", unit.TenantId);
        command.Parameters.AddWithValue("id", unit.UnitId);
        command.Parameters.AddWithValue("code", unit.Code);
        command.Parameters.AddWithValue("name", unit.Name);
        command.Parameters.AddWithValue("precision", unit.Precision);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("created_at", unit.CreatedAt);
    }

    private static void AddItemParameters(NpgsqlCommand command, CatalogItemSnapshot item, Guid accountId)
    {
        command.Parameters.AddWithValue("tenant_id", item.TenantId);
        command.Parameters.AddWithValue("id", item.ItemId);
        command.Parameters.Add("code", NpgsqlDbType.Varchar).Value = (object?)item.Code ?? DBNull.Value;
        command.Parameters.AddWithValue("name", item.Name);
        command.Parameters.Add("description", NpgsqlDbType.Varchar).Value = (object?)item.Description ?? DBNull.Value;
        command.Parameters.AddWithValue("kind", ToDbKind(item.Kind));
        command.Parameters.AddWithValue("base_unit_id", item.BaseUnitId);
        command.Parameters.AddWithValue("stock_mode", ToDbStockMode(item.StockMode));
        command.Parameters.Add("availability", NpgsqlDbType.Varchar).Value = item.Availability is { } availability
            ? WireAvailability(availability) : DBNull.Value;
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("created_at", item.CreatedAt);
    }

    private static string ToDbKind(CatalogItemKind kind) => kind switch
    {
        CatalogItemKind.Product => "product",
        CatalogItemKind.Service => "service",
        _ => throw new InvalidOperationException("The catalog item kind is unsupported.")
    };

    private static string ToDbStockMode(CatalogStockMode mode) => mode switch
    {
        CatalogStockMode.PreciseStock => "precise_stock",
        CatalogStockMode.AvailabilityOnly => "availability_only",
        CatalogStockMode.NonStock => "non_stock",
        _ => throw new InvalidOperationException("The catalog stock mode is unsupported.")
    };

    private static string SerializeReceipt<T>(int status, T snapshot) =>
        JsonSerializer.Serialize(new CatalogReceiptEnvelope<T>(1, status, snapshot), ReceiptJsonOptions);

    private static async Task<ReceiptRow?> FindReceiptAsync(
        CatalogTenantDbSession session,
        TenantContext tenantContext,
        string operation,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        // Serialize one tenant/caller/operation/key before touching master data.
        // A same-key create waits for the winner's receipt, not its code index.
        await LockScopeAsync(session,
            $"catalog-receipt:{tenantContext.TenantId:N}:{tenantContext.AccountId:N}:{operation}:{idempotencyKey}", cancellationToken)
            .ConfigureAwait(false);
        await using var command = session.CreateCommand(CatalogSql.FindReceipt);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.AddWithValue("account_id", tenantContext.AccountId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new ReceiptRow(reader.GetString(0), reader.GetString(1))
            : null;
    }

    private static async Task<bool> TryInsertReceiptAsync(
        CatalogTenantDbSession session,
        TenantContext tenantContext,
        string operation,
        string idempotencyKey,
        string fingerprint,
        string responseJson,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var command = session.CreateCommand(CatalogSql.InsertReceipt);
        command.Parameters.AddWithValue("tenant_id", tenantContext.TenantId);
        command.Parameters.AddWithValue("account_id", tenantContext.AccountId);
        command.Parameters.AddWithValue("operation", operation);
        command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.Add("response_json", NpgsqlDbType.Jsonb).Value = responseJson;
        command.Parameters.AddWithValue("created_at", createdAt);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static CreateCatalogUnitResult ReplayCreateUnit(ReceiptRow receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(CreateCatalogUnitStatus.Replayed, DeserializeSnapshot<CatalogUnitSnapshot>(receipt.ResponseJson))
            : new(CreateCatalogUnitStatus.IdempotencyKeyConflict, null);

    private static CreateCatalogItemResult ReplayCreateItem(ReceiptRow receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(CreateCatalogItemStatus.Replayed, DeserializeSnapshot<CatalogItemSnapshot>(receipt.ResponseJson))
            : new(CreateCatalogItemStatus.IdempotencyKeyConflict, null);

    private static RenameCatalogUnitResult ReplayRenameUnit(ReceiptRow receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(RenameCatalogUnitStatus.Replayed, DeserializeSnapshot<CatalogUnitSnapshot>(receipt.ResponseJson))
            : new(RenameCatalogUnitStatus.IdempotencyKeyConflict, null);

    private static RenameCatalogItemResult ReplayRenameItem(ReceiptRow receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(RenameCatalogItemStatus.Replayed, DeserializeSnapshot<CatalogItemSnapshot>(receipt.ResponseJson))
            : new(RenameCatalogItemStatus.IdempotencyKeyConflict, null);

    private static RetireCatalogUnitResult ReplayRetireUnit(ReceiptRow receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(RetireCatalogUnitStatus.Replayed, DeserializeSnapshot<CatalogUnitSnapshot>(receipt.ResponseJson))
            : new(RetireCatalogUnitStatus.IdempotencyKeyConflict, null);

    private static RetireCatalogItemResult ReplayRetireItem(ReceiptRow receipt, string fingerprint) =>
        string.Equals(receipt.Fingerprint, fingerprint, StringComparison.Ordinal)
            ? new(RetireCatalogItemStatus.Replayed, DeserializeSnapshot<CatalogItemSnapshot>(receipt.ResponseJson))
            : new(RetireCatalogItemStatus.IdempotencyKeyConflict, null);

    private static T DeserializeSnapshot<T>(string responseJson)
    {
        var envelope = JsonSerializer.Deserialize<CatalogReceiptEnvelope<T>>(responseJson, ReceiptJsonOptions)
            ?? throw new InvalidOperationException("The catalog receipt response is invalid.");
        if (envelope.SchemaVersion != 1 || envelope.Snapshot is null)
            throw new InvalidOperationException("The catalog receipt response schema is unsupported.");
        return envelope.Snapshot;
    }

    private readonly record struct ReceiptRow(string Fingerprint, string ResponseJson);

    private sealed record CatalogReceiptEnvelope<T>(int SchemaVersion, int Status, T Snapshot);
}
