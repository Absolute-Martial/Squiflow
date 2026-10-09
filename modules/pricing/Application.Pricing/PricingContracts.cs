using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace Application.Pricing;

public static class PricingCapabilities
{
    public const string View = "Pricing.View";
    public const string EditDraft = "Pricing.EditDraft";
    public const string Publish = "Pricing.Publish";
    public const string Retire = "Pricing.Retire";
    public const string Override = "Pricing.Override";
    public const string OverrideBeyondPolicy = "Pricing.OverrideBeyondPolicy";
}

public enum PriceScopeKind
{
    CommittedQuotation,
    CommittedAgreement,
    Customer,
    Program,
    Organization,
    Wholesale,
    Default,
}

public enum PricePublicationState
{
    Draft,
    Published,
    Superseded,
    Retired,
}

public enum PriceResolutionStatus
{
    PriceResolved,
    PriceMissing,
    PriceExpired,
    PriceConflict,
    PriceOverrideRequired,
}

public enum CreatePriceDraftStatus
{
    Created,
    Replayed,
    IdempotencyKeyConflict,
}

public enum PublishPriceStatus
{
    Published,
    Replayed,
    NotFound,
    RevisionConflict,
    PublicationConflict,
    IdempotencyKeyConflict,
}

public enum RetirePriceStatus
{
    Retired,
    Replayed,
    NotFound,
    RevisionConflict,
    IdempotencyKeyConflict,
}

public sealed record PricingActorContext
{
    public PricingActorContext(Guid tenantId, Guid accountId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account identity cannot be empty.", nameof(accountId));
        TenantId = tenantId;
        AccountId = accountId;
    }

    public Guid TenantId { get; }

    public Guid AccountId { get; }
}

public sealed record PriceScope
{
    public PriceScope(PriceScopeKind kind, Guid? targetId = null)
    {
        if (kind == PriceScopeKind.Default && targetId is not null)
            throw new ArgumentException("The default scope cannot have a target identity.", nameof(targetId));
        if (kind != PriceScopeKind.Default && (!targetId.HasValue || targetId.Value == Guid.Empty))
            throw new ArgumentException("A non-default scope requires a target identity.", nameof(targetId));
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        TargetId = targetId;
    }

    public PriceScopeKind Kind { get; }

    public Guid? TargetId { get; }

    public static PriceScope Default() => new(PriceScopeKind.Default);

    public static Guid WholesaleIdentity { get; } = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static PriceScope Wholesale() => new(PriceScopeKind.Wholesale, WholesaleIdentity);

    public static PriceScope Wholesale(Guid tierId) => new(PriceScopeKind.Wholesale, tierId);

    public static PriceScope Organization(Guid organizationId) => new(PriceScopeKind.Organization, organizationId);

    public static PriceScope Program(Guid programId) => new(PriceScopeKind.Program, programId);

    public static PriceScope Customer(Guid customerId) => new(PriceScopeKind.Customer, customerId);

    public static PriceScope CommittedQuotation(Guid quotationId) =>
        new(PriceScopeKind.CommittedQuotation, quotationId);

    public static PriceScope CommittedAgreement(Guid agreementId) =>
        new(PriceScopeKind.CommittedAgreement, agreementId);

    public int Precedence => Kind switch
    {
        PriceScopeKind.CommittedQuotation or PriceScopeKind.CommittedAgreement => 0,
        PriceScopeKind.Customer => 1,
        PriceScopeKind.Program => 2,
        PriceScopeKind.Organization => 3,
        PriceScopeKind.Wholesale => 4,
        PriceScopeKind.Default => 5,
        _ => throw new ArgumentOutOfRangeException(),
    };
}

public sealed record PriceKey
{
    public PriceKey(Guid itemId, string unitCode, string currencyCode, PriceScope scope, Guid unitId = default, long? unitConversionRevision = null)
    {
        if (itemId == Guid.Empty)
            throw new ArgumentException("Item identity cannot be empty.", nameof(itemId));
        ItemId = itemId;
        UnitId = unitId;
        if (unitConversionRevision is < 1) throw new ArgumentOutOfRangeException(nameof(unitConversionRevision));
        UnitConversionRevision = unitConversionRevision;
        UnitCode = NormalizeCode(unitCode, nameof(unitCode), 64);
        CurrencyCode = NormalizeCurrency(currencyCode);
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    public Guid ItemId { get; }

    public Guid UnitId { get; }

    public long? UnitConversionRevision { get; }

    public bool HasSameDimensions(PriceKey other) => ItemId == other.ItemId && UnitId == other.UnitId &&
        CurrencyCode == other.CurrencyCode && Scope == other.Scope;

    public string UnitCode { get; }

    public string CurrencyCode { get; }

    public PriceScope Scope { get; }

    internal static string NormalizeCode(string value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maxLength + 16)
            throw new ArgumentException("Code exceeds the supported boundary.", parameterName);
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > maxLength || normalized.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '-')))
            throw new ArgumentException($"{parameterName} must be an ASCII code of at most {maxLength} characters.", parameterName);
        return normalized;
    }

    internal static string NormalizeCurrency(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 16)
            throw new ArgumentException("Currency exceeds the supported boundary.", nameof(value));
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("Currency code must contain three ASCII letters.", nameof(value));
        return normalized;
    }
}

public sealed record PriceValidity
{
    public PriceValidity(DateTimeOffset validFrom, DateTimeOffset? validTo = null)
    {
        if (validTo.HasValue && validTo.Value <= validFrom)
            throw new ArgumentException("Price validity must end after it starts.", nameof(validTo));
        ValidFrom = validFrom.ToUniversalTime();
        ValidTo = validTo?.ToUniversalTime();
    }

    public DateTimeOffset ValidFrom { get; }

    public DateTimeOffset? ValidTo { get; }

    public bool Contains(DateTimeOffset instant) =>
        instant >= ValidFrom && (!ValidTo.HasValue || instant < ValidTo.Value);

    public bool HasExpiredAt(DateTimeOffset instant) => ValidTo.HasValue && ValidTo.Value <= instant;

    public bool Overlaps(PriceValidity other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var thisEnd = ValidTo ?? DateTimeOffset.MaxValue;
        var otherEnd = other.ValidTo ?? DateTimeOffset.MaxValue;
        return ValidFrom < otherEnd && other.ValidFrom < thisEnd;
    }
}

public sealed record PriceRevision
{
    public PriceRevision(
        Guid tenantId,
        Guid revisionId,
        long revisionNumber,
        PriceKey key,
        decimal baseUnitPrice,
        PriceValidity validity,
        PricePublicationState state,
        Guid createdByAccountId,
        DateTimeOffset createdAt,
        DateTimeOffset? publishedAt = null,
        DateTimeOffset? retiredAt = null,
        Guid priceId = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        if (revisionId == Guid.Empty)
            throw new ArgumentException("Price revision identity cannot be empty.", nameof(revisionId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revisionNumber);
        if (baseUnitPrice < 0 || baseUnitPrice > 999999999999999.9999m)
            throw new ArgumentOutOfRangeException(nameof(baseUnitPrice), "Base price must fit decimal(19,4) and be nonnegative.");
        if (createdByAccountId == Guid.Empty)
            throw new ArgumentException("Creating account identity cannot be empty.", nameof(createdByAccountId));
        if (!Enum.IsDefined(state))
            throw new ArgumentOutOfRangeException(nameof(state));
        if (state != PricePublicationState.Draft && !publishedAt.HasValue)
            throw new ArgumentException("Published revisions require a publication timestamp.", nameof(publishedAt));
        if (state == PricePublicationState.Retired && !retiredAt.HasValue)
            throw new ArgumentException("Retired revisions require a retirement timestamp.", nameof(retiredAt));
        TenantId = tenantId;
        RevisionId = revisionId;
        PriceId = priceId == Guid.Empty ? revisionId : priceId;
        RevisionNumber = revisionNumber;
        Key = key ?? throw new ArgumentNullException(nameof(key));
        if (decimal.Round(baseUnitPrice, 4) != baseUnitPrice)
            throw new ArgumentException("Base price cannot exceed four decimal places.", nameof(baseUnitPrice));
        BaseUnitPrice = baseUnitPrice;
        Validity = validity ?? throw new ArgumentNullException(nameof(validity));
        State = state;
        CreatedByAccountId = createdByAccountId;
        CreatedAt = createdAt;
        PublishedAt = publishedAt;
        RetiredAt = retiredAt;
    }

    public Guid TenantId { get; }

    public Guid RevisionId { get; }

    public Guid PriceId { get; }

    public long RevisionNumber { get; }

    public PriceKey Key { get; }

    public decimal BaseUnitPrice { get; }

    public PriceValidity Validity { get; }

    public PricePublicationState State { get; }

    public Guid CreatedByAccountId { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? PublishedAt { get; }

    public DateTimeOffset? RetiredAt { get; }

    public PriceRevision Publish(DateTimeOffset publishedAt)
    {
        if (State != PricePublicationState.Draft)
            throw new InvalidOperationException("Only draft prices can be published.");
        return new(TenantId, RevisionId, RevisionNumber, Key, BaseUnitPrice, Validity,
            PricePublicationState.Published, CreatedByAccountId, CreatedAt, publishedAt, priceId: PriceId);
    }

    public PriceRevision Retire(DateTimeOffset retiredAt)
    {
        if (State != PricePublicationState.Published)
            throw new InvalidOperationException("Only published prices can be retired.");
        return new(TenantId, RevisionId, RevisionNumber, Key, BaseUnitPrice, Validity,
            PricePublicationState.Retired, CreatedByAccountId, CreatedAt, PublishedAt, retiredAt, PriceId);
    }
}

public sealed record PriceSelectionContext(
    Guid? CustomerId = null,
    Guid? ProgramId = null,
    Guid? OrganizationId = null,
    Guid? WholesaleTierId = null,
    Guid? CommittedQuotationId = null,
    Guid? CommittedAgreementId = null,
    bool WholesaleApplicable = false,
    long? UnitConversionRevision = null)
{
    public bool Matches(PriceScope scope) => scope.Kind switch
    {
        PriceScopeKind.CommittedQuotation => scope.TargetId == CommittedQuotationId,
        PriceScopeKind.CommittedAgreement => scope.TargetId == CommittedAgreementId,
        PriceScopeKind.Customer => scope.TargetId == CustomerId,
        PriceScopeKind.Program => scope.TargetId == ProgramId,
        PriceScopeKind.Organization => scope.TargetId == OrganizationId,
        PriceScopeKind.Wholesale => WholesaleApplicable && scope.TargetId == PriceScope.WholesaleIdentity,
        PriceScopeKind.Default => true,
        _ => false,
    };
}

public sealed record PriceSelectionRequest
{
    public PriceSelectionRequest(
        Guid tenantId,
        Guid itemId,
        string unitCode,
        string currencyCode,
        PriceSelectionContext context,
        DateTimeOffset at,
        long policyRevision,
        Guid unitId = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant identity cannot be empty.", nameof(tenantId));
        if (itemId == Guid.Empty)
            throw new ArgumentException("Item identity cannot be empty.", nameof(itemId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(policyRevision);
        TenantId = tenantId;
        ItemId = itemId;
        UnitId = unitId;
        UnitCode = PriceKey.NormalizeCode(unitCode, nameof(unitCode), 64);
        CurrencyCode = PriceKey.NormalizeCurrency(currencyCode);
        Context = context ?? throw new ArgumentNullException(nameof(context));
        At = at;
        PolicyRevision = policyRevision;
    }

    public Guid TenantId { get; }

    public Guid ItemId { get; }

    public Guid UnitId { get; }

    public string UnitCode { get; }

    public string CurrencyCode { get; }

    public PriceSelectionContext Context { get; }

    public DateTimeOffset At { get; }

    public long PolicyRevision { get; }
}

// Owner of override-envelope SEMANTICS versioning. A retained selection explanation pins the
// version whose evaluator produced its BeyondPolicy decision, so changing envelope meaning is a
// NEW version with a new evaluator, never an edit of an existing one. Version 1 is frozen: its
// body must keep evaluating exactly as first qualified, because every already-issued quotation
// and committed Order retains version 1 and revalidates through it forever.
public static class PricingOverridePolicySemantics
{
    public const int Version1 = 1;

    // Newly published policies are evaluated under the current meaning. Newly added meaning is a
    // new constant plus a new frozen evaluator; Version1 stays reachable so old facts stay valid.
    public const int CurrentVersion = Version1;

    public static bool Supported(int semanticsVersion) => semanticsVersion == Version1;

    public static bool Contains(PricingOverridePolicy policy, decimal value, decimal basePrice)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return policy.EnvelopeSemanticsVersion switch
        {
            Version1 => ContainsVersion1(policy, value, basePrice),
            _ => throw new PricingValidationException("pricing_policy_semantics_unsupported",
                "The retained override policy envelope semantics are not supported by this reader.")
        };
    }

    // Frozen version 1: inclusive absolute bounds plus optional percentage decrease/increase
    // measured against the selected base price. Do not modify; add a new version instead.
    private static bool ContainsVersion1(PricingOverridePolicy policy, decimal value, decimal basePrice) =>
        value >= policy.MinimumUnitPrice && value <= policy.MaximumUnitPrice &&
        (!policy.MaximumDecreasePercent.HasValue || value >= basePrice * (1 - policy.MaximumDecreasePercent.Value / 100)) &&
        (!policy.MaximumIncreasePercent.HasValue || value <= basePrice * (1 + policy.MaximumIncreasePercent.Value / 100));
}

public sealed record PricingOverridePolicy
{
    public PricingOverridePolicy(
        long policyRevision,
        decimal minimumUnitPrice,
        decimal maximumUnitPrice,
        decimal? maximumDecreasePercent = null,
        decimal? maximumIncreasePercent = null,
        // Default is the frozen legacy version, NOT current: retained JSON written before this
        // field existed must read back as version 1, whatever version is current when it is read.
        int envelopeSemanticsVersion = PricingOverridePolicySemantics.Version1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(policyRevision);
        if (minimumUnitPrice < 0 || maximumUnitPrice < minimumUnitPrice)
            throw new ArgumentOutOfRangeException(nameof(maximumUnitPrice));
        if (maximumUnitPrice > 999999999999999.9999m || decimal.Round(minimumUnitPrice, 4) != minimumUnitPrice || decimal.Round(maximumUnitPrice, 4) != maximumUnitPrice)
            throw new ArgumentOutOfRangeException(nameof(maximumUnitPrice));
        PolicyRevision = policyRevision;
        MinimumUnitPrice = decimal.Round(minimumUnitPrice, 4, MidpointRounding.ToEven);
        MaximumUnitPrice = decimal.Round(maximumUnitPrice, 4, MidpointRounding.ToEven);
        if (maximumDecreasePercent is < 0 or > 100 || maximumIncreasePercent is < 0 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(maximumDecreasePercent));
        MaximumDecreasePercent = maximumDecreasePercent;
        MaximumIncreasePercent = maximumIncreasePercent;
        // Deliberately not range-checked. A snapshot written by a newer writer must still
        // deserialize so a reader can reject it as an unsupported version explicitly, rather
        // than failing as a corrupt read. Readers gate on PricingOverridePolicySemantics.Supported.
        EnvelopeSemanticsVersion = envelopeSemanticsVersion;
    }

    public long PolicyRevision { get; }

    public decimal MinimumUnitPrice { get; }

    public decimal MaximumUnitPrice { get; }

    public decimal? MaximumDecreasePercent { get; }

    public decimal? MaximumIncreasePercent { get; }

    // Retained with the snapshot. A reader that cannot evaluate this version fails closed rather
    // than silently applying its own current envelope meaning to frozen facts.
    public int EnvelopeSemanticsVersion { get; }

    public bool Contains(decimal value) => value >= MinimumUnitPrice && value <= MaximumUnitPrice;

    public bool Contains(decimal value, decimal basePrice) =>
        PricingOverridePolicySemantics.Contains(this, value, basePrice);
}

public sealed record PriceOverrideRequest
{
    public PriceOverrideRequest(
        decimal unitPrice,
        string reason,
        bool canOverride,
        bool canOverrideBeyondPolicy,
        string? approvalReference = null,
        Guid? approvedByAccountId = null)
    {
        if (unitPrice < 0 || unitPrice > 999999999999999.9999m)
            throw new ArgumentOutOfRangeException(nameof(unitPrice));
        if (decimal.Round(unitPrice, 4) != unitPrice)
            throw new ArgumentException("An override cannot exceed four decimal places.", nameof(unitPrice));
        UnitPrice = unitPrice;
        Reason = reason?.Trim() ?? string.Empty;
        if (Reason.Length > 500 || Reason.Any(char.IsControl))
            throw new ArgumentException("Override reason must be bounded text.", nameof(reason));
        CanOverride = canOverride;
        CanOverrideBeyondPolicy = canOverrideBeyondPolicy;
        ApprovalReference = approvalReference?.Trim();
        ApprovedByAccountId = approvedByAccountId;
    }

    public decimal UnitPrice { get; }

    public string Reason { get; }

    public bool CanOverride { get; }

    public bool CanOverrideBeyondPolicy { get; }

    public string? ApprovalReference { get; }

    public Guid? ApprovedByAccountId { get; }
}

public sealed record PriceOverrideEvidence(
    decimal UnitPrice,
    string Reason,
    bool BeyondPolicy,
    string? ApprovalReference,
    Guid? ApprovedByAccountId);

public sealed record PriceCandidate(
    Guid RevisionId,
    long RevisionNumber,
    PriceScope Scope,
    PricePublicationState State,
    PriceValidity Validity,
    decimal? BaseUnitPrice);

public sealed record PriceDiscountEvidence(string PolicyReference, long Revision, decimal Amount);

public sealed record PriceSelectionExplanation(
    decimal? SelectedPrice,
    Guid? SelectedRevisionId,
    long? SelectedRevision,
    PriceScope? SelectedScope,
    IReadOnlyList<PriceCandidate> Candidates,
    PriceOverrideEvidence? Override,
    PriceDiscountEvidence? Discount,
    long PolicyRevision,
    DateTimeOffset EvaluatedAt,
    PriceSelectionContext Context,
    Guid? SelectedPriceId = null,
    string Why = "",
    PricingOverridePolicy? Policy = null);

public abstract record PriceResolution(
    PriceResolutionStatus Status,
    PriceSelectionExplanation Explanation);

public sealed record PriceResolved(
    PriceSelectionExplanation Explanation,
    decimal UnitPrice,
    PriceRevision BasePrice,
    PriceOverrideEvidence? AppliedOverride = null)
    : PriceResolution(PriceResolutionStatus.PriceResolved, Explanation);

public sealed record PriceMissing(PriceSelectionExplanation Explanation)
    : PriceResolution(PriceResolutionStatus.PriceMissing, Explanation);

public sealed record PriceExpired(PriceSelectionExplanation Explanation)
    : PriceResolution(PriceResolutionStatus.PriceExpired, Explanation);

public sealed record PriceConflict(PriceSelectionExplanation Explanation)
    : PriceResolution(PriceResolutionStatus.PriceConflict, Explanation);

public sealed record PriceOverrideRequired(
    PriceSelectionExplanation Explanation,
    string Reason)
    : PriceResolution(PriceResolutionStatus.PriceOverrideRequired, Explanation);

public sealed record CreatePriceDraftRequest(
    Guid ItemId,
    string UnitCode,
    string CurrencyCode,
    PriceScope Scope,
    decimal BaseUnitPrice,
    PriceValidity Validity,
    Guid UnitId = default,
    Guid? PriceId = null,
    long? UnitConversionRevision = null)
{
    public PriceKey Key => new(ItemId, UnitCode, CurrencyCode, Scope, UnitId, UnitConversionRevision);

    public string Fingerprint => PricingFingerprint.Compute(
        string.Create(CultureInfo.InvariantCulture, $"create-v2|{PriceId:D}|{ItemId:D}|{UnitId:D}|{UnitConversionRevision}|{Key.UnitCode}|{Key.CurrencyCode}|{Key.Scope.Kind}|{Key.Scope.TargetId:D}|{BaseUnitPrice:0.0000}|{Validity.ValidFrom.UtcDateTime:O}|{Validity.ValidTo?.UtcDateTime:O}"));
}

public sealed record PublishPriceRequest(Guid RevisionId, Guid? SupersedeRevisionId = null)
{
    public string Fingerprint => PricingFingerprint.Compute($"publish|{RevisionId:D}|{SupersedeRevisionId:D}");
}

public sealed record RetirePriceRequest(Guid RevisionId)
{
    public string Fingerprint => PricingFingerprint.Compute($"retire|{RevisionId:D}");
}

public sealed record PriceLookupRequest
{
    public PriceLookupRequest(Guid itemId, string unitCode, string currencyCode, Guid unitId = default,
        PriceSelectionContext? context = null, int limit = 50, long? afterRevision = null, DateTimeOffset? at = null)
    {
        if (itemId == Guid.Empty)
            throw new ArgumentException("Item identity cannot be empty.", nameof(itemId));
        ItemId = itemId;
        if (limit is < 1 or > 50 || afterRevision is < 1)
            throw new ArgumentOutOfRangeException(nameof(limit));
        UnitId = unitId;
        Context = context ?? new();
        Limit = limit;
        AfterRevision = afterRevision;
        At = at;
        UnitCode = PriceKey.NormalizeCode(unitCode, nameof(unitCode), 64);
        CurrencyCode = PriceKey.NormalizeCurrency(currencyCode);
    }

    public Guid ItemId { get; }

    public Guid UnitId { get; }

    public PriceSelectionContext Context { get; }

    public int Limit { get; }

    public long? AfterRevision { get; }

    public DateTimeOffset? At { get; }

    public string UnitCode { get; }

    public string CurrencyCode { get; }
}

public sealed record CreatePriceDraftResult(CreatePriceDraftStatus Status, PriceRevision? Price);

public sealed record PublishPriceResult(PublishPriceStatus Status, PriceRevision? Price);

public sealed record RetirePriceResult(RetirePriceStatus Status, PriceRevision? Price);

internal static class PricingFingerprint
{
    internal static string Compute(string canonical)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
