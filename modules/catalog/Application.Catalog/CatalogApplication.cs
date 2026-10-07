using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.Tenancy;

namespace Application.Catalog;

public sealed record CatalogUnitIntent(string Code, string Name, int Precision, string Fingerprint)
{
    public static CatalogUnitIntent Create(CreateCatalogUnitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = CatalogRules.NormalizeCode(request.Code, "unit_code_invalid");
        var name = CatalogRules.NormalizeName(request.Name, "unit_name_invalid");
        CatalogRules.RequirePrecision(request.Precision);
        return new(code, name, request.Precision, CatalogRules.Fingerprint(
            "create-unit", code, name, request.Precision.ToString(CultureInfo.InvariantCulture)));
    }
}

public sealed record CatalogItemIntent(
    string? Code,
    string Name,
    string? Description,
    CatalogItemKind Kind,
    Guid BaseUnitId,
    CatalogStockMode StockMode,
    string Fingerprint)
{
    public static CatalogItemIntent Create(CreateCatalogItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = CatalogRules.NormalizeOptionalCode(request.Code, "item_code_invalid");
        var name = CatalogRules.NormalizeName(request.Name, "item_name_invalid");
        var description = CatalogRules.NormalizeOptionalDescription(request.Description);
        CatalogRules.RequireIdentity(request.BaseUnitId, "base_unit_id_invalid");
        CatalogRules.RequireItemKind(request.Kind);
        CatalogRules.RequireStockMode(request.StockMode);
        return new(
            code,
            name,
            description,
            request.Kind,
            request.BaseUnitId,
            request.StockMode,
            CatalogRules.Fingerprint(
                "create-item",
                code ?? string.Empty,
                name,
                description ?? string.Empty,
                ((int)request.Kind).ToString(CultureInfo.InvariantCulture),
                request.BaseUnitId.ToString("N"),
                ((int)request.StockMode).ToString(CultureInfo.InvariantCulture)));
    }
}

public interface ICatalogStore
{
    Task<CreateCatalogUnitResult> CreateUnitAsync(
        TenantContext tenantContext,
        CatalogUnitIntent intent,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<CreateCatalogItemResult> CreateItemAsync(
        TenantContext tenantContext,
        CatalogItemIntent intent,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<CatalogUnitSnapshot?> FindUnitAsync(
        TenantContext tenantContext,
        Guid unitId,
        CancellationToken cancellationToken);

    Task<CatalogItemSnapshot?> FindItemAsync(
        TenantContext tenantContext,
        Guid itemId,
        CancellationToken cancellationToken);

    Task<CatalogUnitPage> ListUnitsAsync(
        TenantContext tenantContext,
        ListCatalogUnitsRequest request,
        CancellationToken cancellationToken);

    Task<CatalogItemPage> ListItemsAsync(
        TenantContext tenantContext,
        ListCatalogItemsRequest request,
        CancellationToken cancellationToken);

    Task<RenameCatalogUnitResult> RenameUnitAsync(
        TenantContext tenantContext,
        RenameCatalogUnitRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken);

    Task<RenameCatalogItemResult> RenameItemAsync(
        TenantContext tenantContext,
        RenameCatalogItemRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken);

    Task<RetireCatalogUnitResult> RetireUnitAsync(
        TenantContext tenantContext,
        RetireCatalogUnitRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken);

    Task<RetireCatalogItemResult> RetireItemAsync(
        TenantContext tenantContext,
        RetireCatalogItemRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken);

    Task<CatalogLineFactsResult> SelectLineFactsAsync(
        TenantContext tenantContext,
        CatalogLineSelection selection,
        CancellationToken cancellationToken);
}

public sealed class CreateCatalogUnit(ICatalogStore store)
{
    public Task<CreateCatalogUnitResult> ExecuteAsync(
        TenantContext tenantContext,
        CreateCatalogUnitRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        var key = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        return store.CreateUnitAsync(tenantContext, CatalogUnitIntent.Create(request), key, cancellationToken);
    }
}

public sealed class CreateCatalogItem(ICatalogStore store)
{
    public Task<CreateCatalogItemResult> ExecuteAsync(
        TenantContext tenantContext,
        CreateCatalogItemRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        var key = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        return store.CreateItemAsync(tenantContext, CatalogItemIntent.Create(request), key, cancellationToken);
    }
}

public sealed class GetCatalogUnit(ICatalogStore store)
{
    public Task<CatalogUnitSnapshot?> ExecuteAsync(
        TenantContext tenantContext,
        Guid unitId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        CatalogRules.RequireIdentity(unitId, "unit_id_invalid");
        return store.FindUnitAsync(tenantContext, unitId, cancellationToken);
    }
}

public sealed class GetCatalogItem(ICatalogStore store)
{
    public Task<CatalogItemSnapshot?> ExecuteAsync(
        TenantContext tenantContext,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        CatalogRules.RequireIdentity(itemId, "item_id_invalid");
        return store.FindItemAsync(tenantContext, itemId, cancellationToken);
    }
}

public sealed class ListCatalogUnits(ICatalogStore store)
{
    public Task<CatalogUnitPage> ExecuteAsync(
        TenantContext tenantContext,
        ListCatalogUnitsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequirePage(request.Limit);
        CatalogRules.RequireCursor(request.After?.CreatedAt, request.After?.UnitId, "unit_cursor_invalid");
        return store.ListUnitsAsync(tenantContext, request, cancellationToken);
    }
}

public sealed class ListCatalogItems(ICatalogStore store)
{
    public Task<CatalogItemPage> ExecuteAsync(
        TenantContext tenantContext,
        ListCatalogItemsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequirePage(request.Limit);
        CatalogRules.RequireCursor(request.After?.CreatedAt, request.After?.ItemId, "item_cursor_invalid");
        return store.ListItemsAsync(tenantContext, request, cancellationToken);
    }
}

public sealed class RenameCatalogUnit(ICatalogStore store)
{
    public Task<RenameCatalogUnitResult> ExecuteAsync(
        TenantContext tenantContext,
        RenameCatalogUnitRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequireIdentity(request.UnitId, "unit_id_invalid");
        CatalogRules.RequireRevision(request.ExpectedRevision);
        var name = CatalogRules.NormalizeName(request.Name, "unit_name_invalid");
        var key = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        var fingerprint = CatalogRules.Fingerprint(
            "rename-unit", request.UnitId.ToString("N"), request.ExpectedRevision.ToString(CultureInfo.InvariantCulture), name);
        return store.RenameUnitAsync(
            tenantContext, request with { Name = name }, key, fingerprint, cancellationToken);
    }
}

public sealed class RenameCatalogItem(ICatalogStore store)
{
    public Task<RenameCatalogItemResult> ExecuteAsync(
        TenantContext tenantContext,
        RenameCatalogItemRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequireIdentity(request.ItemId, "item_id_invalid");
        CatalogRules.RequireRevision(request.ExpectedRevision);
        var name = CatalogRules.NormalizeName(request.Name, "item_name_invalid");
        var description = CatalogRules.NormalizeOptionalDescription(request.Description);
        var key = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        var fingerprint = CatalogRules.Fingerprint(
            "rename-item",
            request.ItemId.ToString("N"),
            request.ExpectedRevision.ToString(CultureInfo.InvariantCulture),
            name,
            description ?? string.Empty);
        return store.RenameItemAsync(
            tenantContext,
            request with { Name = name, Description = description },
            key,
            fingerprint,
            cancellationToken);
    }
}

public sealed class RetireCatalogUnit(ICatalogStore store)
{
    public Task<RetireCatalogUnitResult> ExecuteAsync(
        TenantContext tenantContext,
        RetireCatalogUnitRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequireIdentity(request.UnitId, "unit_id_invalid");
        CatalogRules.RequireRevision(request.ExpectedRevision);
        var key = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        var fingerprint = CatalogRules.Fingerprint(
            "retire-unit", request.UnitId.ToString("N"), request.ExpectedRevision.ToString(CultureInfo.InvariantCulture));
        return store.RetireUnitAsync(tenantContext, request, key, fingerprint, cancellationToken);
    }
}

public sealed class RetireCatalogItem(ICatalogStore store)
{
    public Task<RetireCatalogItemResult> ExecuteAsync(
        TenantContext tenantContext,
        RetireCatalogItemRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequireIdentity(request.ItemId, "item_id_invalid");
        CatalogRules.RequireRevision(request.ExpectedRevision);
        var key = CatalogRules.NormalizeIdempotencyKey(idempotencyKey);
        var fingerprint = CatalogRules.Fingerprint(
            "retire-item", request.ItemId.ToString("N"), request.ExpectedRevision.ToString(CultureInfo.InvariantCulture));
        return store.RetireItemAsync(tenantContext, request, key, fingerprint, cancellationToken);
    }
}

public sealed class SelectCatalogLineFacts(ICatalogStore store)
{
    public Task<CatalogLineFactsResult> ExecuteAsync(
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
        return store.SelectLineFactsAsync(tenantContext, selection, cancellationToken);
    }
}

public static class CatalogRules
{
    private const decimal MaximumQuantity = 9_999_999_999_999_999_999.999999999m;

    internal static string NormalizeCode(string value, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new CatalogValidationException(code, "Code is required and must be 1 to 64 ASCII characters.");
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length is < 1 or > 64 || !HasWellFormedUtf16(normalized) ||
            normalized.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
            throw new CatalogValidationException(code, "Code is required and must be 1 to 64 ASCII characters.");
        return normalized;
    }

    internal static string? NormalizeOptionalCode(string? value, string code) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeCode(value, code);

    public static string NormalizeName(string value, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new CatalogValidationException(code, "Name is required and cannot exceed 200 characters.");
        if (!HasWellFormedUtf16(value))
            throw new CatalogValidationException(code, "Name contains unsupported characters.");
        var normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length > 200 || normalized.Any(char.IsControl))
            throw new CatalogValidationException(code, "Name is required and cannot exceed 200 characters.");
        return normalized;
    }

    public static string? NormalizeOptionalDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!HasWellFormedUtf16(value))
            throw new CatalogValidationException("description_invalid", "Description contains unsupported characters.");
        var normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length > 1000 || normalized.Any(char.IsControl))
            throw new CatalogValidationException("description_invalid", "Description cannot exceed 1000 characters.");
        return normalized;
    }

    public static string NormalizeIdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new CatalogValidationException("idempotency_key_invalid", "Idempotency-Key is required and cannot exceed 128 characters.");
        var normalized = value.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl) || !HasWellFormedUtf16(normalized))
            throw new CatalogValidationException("idempotency_key_invalid", "Idempotency-Key is required and cannot exceed 128 characters.");
        return normalized;
    }

    public static void RequireIdentity(Guid value, string code)
    {
        if (value == Guid.Empty)
            throw new CatalogValidationException(code, "Identity cannot be empty.");
    }

    public static void RequirePrecision(int value)
    {
        if (value is < 0 or > 9)
            throw new CatalogValidationException("precision_invalid", "Unit precision must be between 0 and 9.");
    }

    public static void RequireRevision(long value)
    {
        if (value < 1 || value == long.MaxValue)
            throw new CatalogValidationException("expected_revision_invalid", "Expected revision must be positive.");
    }

    public static void RequirePage(int value)
    {
        if (value is < 1 or > 50)
            throw new CatalogValidationException("page_size_invalid", "Page size must be between 1 and 50.");
    }

    public static void RequireCursor(DateTimeOffset? createdAt, Guid? id, string code)
    {
        if (createdAt is null && id is null)
            return;
        if (createdAt is null || id is null || id == Guid.Empty || createdAt.Value.Offset != TimeSpan.Zero || createdAt.Value == default)
            throw new CatalogValidationException(code, "The page cursor is invalid.");
    }

    internal static void RequireItemKind(CatalogItemKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw new CatalogValidationException("item_kind_invalid", "Item kind must be Product or Service.");
    }

    internal static void RequireStockMode(CatalogStockMode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new CatalogValidationException("stock_mode_invalid", "Stock mode is invalid.");
    }

    public static void RequireQuantity(decimal quantity, int precision)
    {
        RequirePrecision(precision);
        if (quantity <= 0 || quantity > MaximumQuantity || decimal.Round(quantity, precision, MidpointRounding.ToEven) != quantity)
            throw new CatalogValidationException("quantity_invalid", "Quantity must be positive and fit the selected unit precision.");
    }

    internal static string Fingerprint(string kind, params string[] values)
    {
        var canonical = new StringBuilder("v1:").Append(kind).Append(':');
        foreach (var value in values)
            canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static bool HasWellFormedUtf16(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (++index == value.Length || !char.IsLowSurrogate(value[index]))
                    return false;
            }
            else if (char.IsLowSurrogate(value[index]))
                return false;
        }
        return true;
    }
}
