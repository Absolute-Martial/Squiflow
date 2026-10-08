using System.Globalization;
using System.Numerics;
using Application.Tenancy;

namespace Application.Catalog;

public sealed record CatalogConversionSnapshot(
    Guid TenantId, Guid SourceUnitId, Guid TargetUnitId, long Revision,
    decimal Numerator, decimal Denominator, Guid PublishedByAccountId, DateTimeOffset PublishedAt);

public sealed record PublishCatalogConversionRequest(
    Guid SourceUnitId, Guid TargetUnitId, long ExpectedRevision, decimal Numerator, decimal Denominator);

public sealed record CatalogConversionIntent(
    Guid SourceUnitId, Guid TargetUnitId, long ExpectedRevision,
    decimal Numerator, decimal Denominator, string Fingerprint)
{
    public static CatalogConversionIntent Create(PublishCatalogConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequireIdentity(request.SourceUnitId, "source_unit_id_invalid");
        CatalogRules.RequireIdentity(request.TargetUnitId, "target_unit_id_invalid");
        if (request.SourceUnitId == request.TargetUnitId)
            throw new CatalogValidationException("conversion_identity_invalid", "Identity conversion is intrinsic and cannot be published.");
        if (request.ExpectedRevision < 0 || request.ExpectedRevision == long.MaxValue)
            throw new CatalogValidationException("expected_revision_invalid", "Expected revision must be zero for first publication or a supported current revision.");
        CatalogQuantityConversion.RequireFactor(request.Numerator);
        CatalogQuantityConversion.RequireFactor(request.Denominator);
        return new(request.SourceUnitId, request.TargetUnitId, request.ExpectedRevision, request.Numerator, request.Denominator,
            CatalogRules.Fingerprint("publish-conversion", request.SourceUnitId.ToString("N"), request.TargetUnitId.ToString("N"),
                request.ExpectedRevision.ToString(CultureInfo.InvariantCulture),
                request.Numerator.ToString("G29", CultureInfo.InvariantCulture), request.Denominator.ToString("G29", CultureInfo.InvariantCulture)));
    }
}

public enum PublishCatalogConversionStatus
{
    Published = 1,
    Replayed = 2,
    UnitNotFound = 3,
    UnitRetired = 4,
    RevisionConflict = 5,
    IdempotencyKeyConflict = 6,
}

public sealed record PublishCatalogConversionResult(PublishCatalogConversionStatus Status, CatalogConversionSnapshot? Conversion);

public sealed record ChangeCatalogAvailabilityRequest(Guid ItemId, long ExpectedRevision, CatalogAvailability Availability);

public enum ChangeCatalogAvailabilityStatus
{
    Changed = 1,
    Replayed = 2,
    NotFound = 3,
    AlreadyRetired = 4,
    RevisionConflict = 5,
    StockModeMismatch = 6,
    AlreadyInState = 7,
    IdempotencyKeyConflict = 8,
}

public sealed record ChangeCatalogAvailabilityResult(ChangeCatalogAvailabilityStatus Status, CatalogItemSnapshot? Item);

public interface ICatalogConversionStore
{
    Task<PublishCatalogConversionResult> PublishConversionAsync(TenantContext context, CatalogConversionIntent intent,
        string idempotencyKey, CancellationToken cancellationToken);

    Task<CatalogConversionSnapshot?> FindConversionAsync(TenantContext context, Guid sourceUnitId, Guid targetUnitId,
        long revision, CancellationToken cancellationToken);
}

public interface ICatalogAvailabilityStore
{
    Task<ChangeCatalogAvailabilityResult> ChangeAvailabilityAsync(TenantContext context,
        ChangeCatalogAvailabilityRequest request, string idempotencyKey, string fingerprint, CancellationToken cancellationToken);
}

public sealed class PublishCatalogConversion(ICatalogConversionStore store)
{
    public Task<PublishCatalogConversionResult> ExecuteAsync(TenantContext context, PublishCatalogConversionRequest request,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return store.PublishConversionAsync(context, CatalogConversionIntent.Create(request),
            CatalogRules.NormalizeIdempotencyKey(idempotencyKey), cancellationToken);
    }
}

public sealed class GetCatalogConversion(ICatalogConversionStore store)
{
    public Task<CatalogConversionSnapshot?> ExecuteAsync(TenantContext context, Guid sourceUnitId, Guid targetUnitId,
        long revision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        CatalogRules.RequireIdentity(sourceUnitId, "source_unit_id_invalid");
        CatalogRules.RequireIdentity(targetUnitId, "target_unit_id_invalid");
        CatalogRules.RequireRevision(revision);
        return store.FindConversionAsync(context, sourceUnitId, targetUnitId, revision, cancellationToken);
    }
}

public sealed class ChangeCatalogAvailability(ICatalogAvailabilityStore store)
{
    public Task<ChangeCatalogAvailabilityResult> ExecuteAsync(TenantContext context, ChangeCatalogAvailabilityRequest request,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        CatalogRules.RequireIdentity(request.ItemId, "item_id_invalid");
        CatalogRules.RequireRevision(request.ExpectedRevision);
        if (!Enum.IsDefined(request.Availability))
            throw new CatalogValidationException("availability_invalid", "Availability must be available or unavailable.");
        var fingerprint = CatalogRules.Fingerprint("change-availability", request.ItemId.ToString("N"),
            request.ExpectedRevision.ToString(CultureInfo.InvariantCulture), ((int)request.Availability).ToString(CultureInfo.InvariantCulture));
        return store.ChangeAvailabilityAsync(context, request, CatalogRules.NormalizeIdempotencyKey(idempotencyKey), fingerprint, cancellationToken);
    }
}

public static class CatalogQuantityConversion
{
    public static void RequireFactor(decimal factor)
    {
        if (factor <= 0 || factor > 9_999_999_999.999999999m || decimal.Round(factor, 9, MidpointRounding.ToEven) != factor)
            throw new CatalogValidationException("conversion_factor_invalid", "Conversion factors must be positive decimal 19,9 values.");
    }

    // Exact decimal rational arithmetic avoids intermediate decimal overflow and
    // double rounding. Only the final base quantity is rounded, at its precision.
    public static decimal Convert(decimal quantity, int sourcePrecision, int targetPrecision, decimal numerator, decimal denominator)
    {
        CatalogRules.RequireQuantity(quantity, sourcePrecision);
        CatalogRules.RequirePrecision(targetPrecision);
        RequireFactor(numerator);
        RequireFactor(denominator);
        var (q, qs) = DecimalParts(quantity);
        var (n, ns) = DecimalParts(numerator);
        var (d, ds) = DecimalParts(denominator);
        var top = q * n * BigInteger.Pow(10, ds + targetPrecision);
        var bottom = d * BigInteger.Pow(10, qs + ns);
        var rounded = BigInteger.DivRem(top, bottom, out var remainder);
        var comparison = (remainder * 2).CompareTo(bottom);
        if (comparison > 0 || (comparison == 0 && !rounded.IsEven)) rounded++;
        try
        {
            var result = (decimal)rounded / (decimal)BigInteger.Pow(10, targetPrecision);
            CatalogRules.RequireQuantity(result, targetPrecision);
            return result;
        }
        catch (OverflowException)
        {
            throw new CatalogValidationException("quantity_invalid", "The converted quantity exceeds the supported range.");
        }
    }

    private static (BigInteger Coefficient, int Scale) DecimalParts(decimal value)
    {
        var bits = decimal.GetBits(value);
        var coefficient = (BigInteger)(uint)bits[0] | ((BigInteger)(uint)bits[1] << 32) | ((BigInteger)(uint)bits[2] << 64);
        return (coefficient, (bits[3] >> 16) & 0x7F);
    }
}
