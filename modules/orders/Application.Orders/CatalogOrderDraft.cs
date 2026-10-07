using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.Catalog;
using Application.Customers;
using Application.Tenancy;
using PriceContracts = Application.Pricing;

namespace Application.Orders;

// These facts enter only through the trusted selector, not supplied-price ingress.
public sealed record OrderCommercialLineFacts(
    CatalogLineFacts Catalog,
    PriceContracts.PriceRevision PublishedPrice,
    PriceContracts.RetainedPriceSelection PriceSelection);

public sealed record CatalogOrderLineInput(Guid ItemId, Guid UnitId, decimal Quantity,
    long? ConversionRevision = null, decimal? OverridePrice = null, string? OverrideReason = null);

public sealed record CatalogOrderDraftRequest(string Summary, string CurrencyCode,
    IReadOnlyList<CatalogOrderLineInput> Lines,
    Guid? CustomerId = null, CustomerOrderContext? CustomerContext = null,
    bool WholesaleApplicable = false);

public sealed record ReviseCatalogOrderDraftRequest(Guid OrderId, long ExpectedRevision, CatalogOrderDraftRequest Draft);

// Only a trusted current-authority adapter constructs this input; no wire DTO accepts it.
public sealed record OrderPricingAuthority(bool CanOverride, bool CanOverrideBeyondPolicy);

public interface IOrderDraftReceiptReader
{
    Task<CreateOrderDraftResult?> FindCreateReceiptAsync(TenantContext context, string key, string fingerprint, CancellationToken ct);
    Task<ReviseOrderDraftResult?> FindRevisionReceiptAsync(TenantContext context, string key, string fingerprint, CancellationToken ct);
}

public sealed class OrderCommercialSelectionException(string code)
    : Exception("The requested catalog price is not currently selectable.")
{
    public string Code { get; } = code;
}

public sealed class CatalogOrderDraftApplication(IOrderDraftStore store, IOrderDraftReceiptReader receipts,
    SelectCatalogLineFacts catalog, PriceContracts.PricingApplication pricing, ResolveCustomerOrderContext customers,
    ICustomerCanonicalDirectory? canonicalCustomers = null,
    IOrderPricingAuthorityReader? currentPricingAuthority = null)
{
    public async Task<CreateOrderDraftResult> CreateAsync(TenantContext context, CatalogOrderDraftRequest request,
        string key, OrderPricingAuthority authority, CancellationToken ct)
    {
        var normalized = Normalize(request, authority);
        key = OrderDraftRules.NormalizeIdempotencyKey(key);
        var fingerprint = Fingerprint(normalized);
        var replay = await receipts.FindCreateReceiptAsync(context, key, fingerprint, ct).ConfigureAwait(false);
        if (replay is not null)
        {
            if (replay.Status == CreateOrderDraftStatus.Replayed)
                await RequireReplayAuthorityAsync(context, replay.Order!, authority, ct).ConfigureAwait(false);
            return replay;
        }
        var intent = await SelectAsync(context, normalized, authority, fingerprint, ct).ConfigureAwait(false);
        var result = await store.CreateAsync(context, intent, key, ct).ConfigureAwait(false);
        if (result.Status == CreateOrderDraftStatus.Replayed)
            await RequireReplayAuthorityAsync(context, result.Order!, authority, ct).ConfigureAwait(false);
        return result;
    }

    public async Task<ReviseOrderDraftResult> ReviseAsync(TenantContext context, ReviseCatalogOrderDraftRequest request,
        string key, OrderPricingAuthority authority, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OrderId == Guid.Empty || request.ExpectedRevision < 1)
            throw new OrderDraftValidationException("revision_request_invalid", "Order identity and positive expected revision are required.");
        var normalized = Normalize(request.Draft, authority);
        key = OrderDraftRules.NormalizeIdempotencyKey(key);
        var intentFingerprint = Fingerprint(normalized);
        var fingerprint = Hash(FormattableString.Invariant($"catalog-revise-v1:{request.OrderId:N}:{request.ExpectedRevision}:{intentFingerprint}"));
        var replay = await receipts.FindRevisionReceiptAsync(context, key, fingerprint, ct).ConfigureAwait(false);
        if (replay is not null)
        {
            if (replay.Status == ReviseOrderDraftStatus.Replayed)
                await RequireReplayAuthorityAsync(context, replay.Order!, authority, ct).ConfigureAwait(false);
            return replay;
        }
        var intent = await SelectAsync(context, normalized, authority, intentFingerprint, ct).ConfigureAwait(false);
        var manualShape = new ReviseOrderDraftRequest(request.OrderId, request.ExpectedRevision, intent.Summary,
            intent.CurrencyCode, [], intent.CustomerContext);
        var result = await store.ReviseAsync(context, manualShape, intent, key, fingerprint, ct).ConfigureAwait(false);
        if (result.Status == ReviseOrderDraftStatus.Replayed)
            await RequireReplayAuthorityAsync(context, result.Order!, authority, ct).ConfigureAwait(false);
        return result;
    }

    private async Task RequireReplayAuthorityAsync(TenantContext context, OrderDraftSnapshot order,
        OrderPricingAuthority authority, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        // Replay preserves the original envelope/evidence, even after policy changes.
        if (!order.Lines.Any(line => line.CommercialFacts?.PriceSelection.Explanation.Override is { BeyondPolicy: true }))
            return;
        var permitted = currentPricingAuthority is null
            ? authority.CanOverrideBeyondPolicy
            : await currentPricingAuthority.CanOverrideBeyondPolicyAsync(context, ct).ConfigureAwait(false);
        if (!permitted) throw new OrderCommercialSelectionException("pricing_override_forbidden");
    }

    private async Task<OrderDraftIntent> SelectAsync(TenantContext context, CatalogOrderDraftRequest request,
        OrderPricingAuthority authority, string fingerprint, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (request.CustomerId is { } requestedCustomer && canonicalCustomers is not null)
        {
            var canonical = await canonicalCustomers.ResolveCurrentCustomerAsync(context, requestedCustomer, ct).ConfigureAwait(false)
                ?? throw new OrderCommercialSelectionException("pricing_customer_invalid");
            if (canonical.TenantId != context.TenantId || canonical.IndividualId == Guid.Empty ||
                canonical.Availability != CustomerIndividualAvailability.Active || canonical.RedirectTargetIndividualId.HasValue)
                throw new InvalidOperationException("The canonical customer directory returned inconsistent current facts.");
            request = request with { CustomerId = canonical.IndividualId };
        }
        if (request.CustomerContext is { } customer &&
            await customers.ExecuteAsync(context, customer.OrganizationId, customer.ProgramId, ct).ConfigureAwait(false) is null)
            throw new CustomerOrderContextNotFoundException();

        var inputs = new List<OrderDraftLineInput>(request.Lines.Count);
        var facts = new List<OrderCommercialLineFacts>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            var selection = await catalog.ExecuteAsync(context,
                new(line.ItemId, line.UnitId, line.Quantity, line.ConversionRevision), ct).ConfigureAwait(false);
            if (selection.Facts is not { } selected || selection.Status != CatalogLineFactsStatus.Available)
                throw new OrderCommercialSelectionException("catalog_" + SnakeCase(selection.Status.ToString()));
            if (selected.ItemId != line.ItemId || selected.UnitId != line.UnitId || selected.Quantity != line.Quantity)
                throw new InvalidOperationException("Catalog returned inconsistent selected line identities or quantity.");
            var priceContext = new PriceContracts.PriceSelectionContext(CustomerId: request.CustomerId,
                OrganizationId: request.CustomerContext?.OrganizationId, ProgramId: request.CustomerContext?.ProgramId,
                WholesaleApplicable: request.WholesaleApplicable, UnitConversionRevision: line.ConversionRevision);
            var result = await pricing.ResolveAsync(new(context.TenantId, context.AccountId), line.ItemId, line.UnitId,
                request.CurrencyCode, priceContext, null, null, false, false, ct).ConfigureAwait(false);
            if (line.OverridePrice is { } overridePrice)
            {
                var canBeyond = authority.CanOverrideBeyondPolicy;
                if (authority.CanOverride && currentPricingAuthority is not null &&
                    PriceContracts.PriceSelectionEngine.RequiresElevatedOverride(result, overridePrice))
                    canBeyond = await currentPricingAuthority.CanOverrideBeyondPolicyAsync(context, ct).ConfigureAwait(false);
                result = PriceContracts.PriceSelectionEngine.ApplyOverride(result,
                    new(overridePrice, line.OverrideReason!, authority.CanOverride, canBeyond));
            }
            if (result is not PriceContracts.PriceResolved resolved)
                throw new OrderCommercialSelectionException(SnakeCase(result.Status.ToString()));
            if (resolved.BasePrice.Key.UnitConversionRevision != line.ConversionRevision)
                throw new OrderCommercialSelectionException("pricing_conversion_conflict");
            if (resolved.BasePrice.TenantId != context.TenantId || resolved.BasePrice.Key.ItemId != selected.ItemId ||
                resolved.BasePrice.Key.UnitId != selected.UnitId || resolved.BasePrice.Key.CurrencyCode != request.CurrencyCode ||
                resolved.BasePrice.State != PriceContracts.PricePublicationState.Published)
                throw new InvalidOperationException("The price selection returned inconsistent facts.");
            inputs.Add(new(selected.ItemName, selected.Quantity, selected.UnitCode, resolved.UnitPrice));
            facts.Add(new(selected, resolved.BasePrice,
                new(selected.ItemId, selected.UnitId, request.CurrencyCode, resolved.UnitPrice, resolved.Explanation)));
        }

        // The existing Orders calculator remains the only selling amount arithmetic.
        var calculated = OrderDraftIntent.Create(new(request.Summary, request.CurrencyCode, inputs, request.CustomerContext), catalogUnitCodes: true);
        var retained = calculated.Lines.Select((line, index) => line with { CommercialFacts = facts[index] }).ToArray();
        return calculated with { Lines = retained, Fingerprint = fingerprint };
    }

    private static CatalogOrderDraftRequest Normalize(CatalogOrderDraftRequest request, OrderPricingAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(authority);
        if (request.Lines is null || request.Lines.Count is < 1 or > 100)
            throw new OrderDraftValidationException("lines_invalid", "An order requires between 1 and 100 lines.");
        var lines = request.Lines.Select(line =>
        {
            if (line is null || line.ItemId == Guid.Empty || line.UnitId == Guid.Empty || line.ConversionRevision is < 1)
                throw new OrderDraftValidationException("catalog_line_invalid", "Catalog identities and conversion revision must be valid.");
            OrderDraftRules.RequirePositiveDecimal(line.Quantity, "quantity_invalid");
            if (line.OverridePrice is { } price)
            {
                OrderDraftRules.RequireNonNegativeDecimal(price, "unit_price_invalid");
                if (!authority.CanOverride) throw new OrderCommercialSelectionException("pricing_override_forbidden");
                var reason = OrderDraftRules.NormalizeRequiredText(line.OverrideReason!, 500,
                    "override_reason_invalid", "A bounded override reason is required.");
                if (reason.Any(char.IsControl))
                    throw new OrderDraftValidationException("override_reason_invalid", "Override reason cannot contain control characters.");
                return line with { OverrideReason = reason };
            }
            if (line.OverrideReason is not null)
                throw new OrderDraftValidationException("override_reason_invalid", "An override reason requires an override price.");
            return line;
        }).ToArray();
        if (request.CustomerId == Guid.Empty || (request.CustomerContext is { } customer &&
            (customer.OrganizationId == Guid.Empty || customer.ProgramId == Guid.Empty)))
            throw new OrderDraftValidationException("customer_context_invalid", "Customer identities must be nonempty.");
        return request with
        {
            Summary = OrderDraftRules.NormalizeRequiredText(request.Summary, 200, "summary_invalid", "A bounded summary is required."),
            CurrencyCode = OrderDraftRules.NormalizeCurrencyCode(request.CurrencyCode),
            Lines = lines,
        };
    }

    private static string Fingerprint(CatalogOrderDraftRequest request)
    {
        var text = new StringBuilder("catalog-create-v1:");
        void Append(string value) => text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        Append(request.Summary);
        Append(request.CurrencyCode);
        Append(request.CustomerId?.ToString("N") ?? "");
        Append(request.CustomerContext?.OrganizationId.ToString("N") ?? "");
        Append(request.CustomerContext?.ProgramId?.ToString("N") ?? "");
        Append(request.WholesaleApplicable ? "1" : "0");
        foreach (var line in request.Lines)
        {
            Append(line.ItemId.ToString("N")); Append(line.UnitId.ToString("N"));
            Append(line.Quantity.ToString("G29", CultureInfo.InvariantCulture));
            Append(line.ConversionRevision?.ToString(CultureInfo.InvariantCulture) ?? "");
            Append(line.OverridePrice?.ToString("G29", CultureInfo.InvariantCulture) ?? "");
            Append(line.OverrideReason ?? "");
        }
        return Hash(text.ToString());
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string SnakeCase(string value) => string.Concat(value.Select((character, index) =>
        (index > 0 && char.IsUpper(character) ? "_" : "") + char.ToLowerInvariant(character)));
}

// Comparison only: the calling commit provider must already protect selectable
// Catalog/Pricing facts inside its effect/receipt transaction until commit/rollback.
// The guard must not acquire a second publication pin on another connection:
// a queued exclusive publisher would wait on the caller and block that second pin.
// False rejects without refreshing retained facts; failures propagate and roll back.
public interface IOrderCommercialCommitGuard
{
    Task<bool> IsCompatibleAsync(TenantContext context, OrderDraftSnapshot order, CancellationToken ct);
}
