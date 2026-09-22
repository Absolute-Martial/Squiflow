using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.Tenancy;

namespace Application.Orders;

public sealed record OrderDraftLine(
    int Position,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OrderDraftSnapshot(
    Guid OrderId,
    Guid TenantId,
    Guid CreatedByAccountId,
    string Summary,
    string CurrencyCode,
    decimal Total,
    long Revision,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderDraftLine> Lines);

public sealed record OrderDraftLineInput(
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitPrice);

public sealed record CreateOrderDraftRequest(
    string Summary,
    string CurrencyCode,
    IReadOnlyList<OrderDraftLineInput> Lines);

public enum CreateOrderDraftStatus
{
    Created = 1,
    Replayed = 2,
    IdempotencyKeyConflict = 3,
}

public sealed record CreateOrderDraftResult(
    CreateOrderDraftStatus Status,
    OrderDraftSnapshot? Order);

public sealed class OrderDraftValidationException(string code, string message)
    : ArgumentException(message)
{
    public string Code { get; } = code;
}

public interface IOrderDraftStore
{
    Task<CreateOrderDraftResult> CreateAsync(
        TenantContext tenantContext,
        OrderDraftIntent intent,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<OrderDraftSnapshot?> FindAsync(
        TenantContext tenantContext,
        Guid orderId,
        CancellationToken cancellationToken);
}

public sealed class CreateOrderDraft(IOrderDraftStore store)
{
    public Task<CreateOrderDraftResult> ExecuteAsync(
        TenantContext tenantContext,
        CreateOrderDraftRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        var normalizedKey = OrderDraftRules.NormalizeIdempotencyKey(idempotencyKey);
        var intent = OrderDraftIntent.Create(request);
        return store.CreateAsync(tenantContext, intent, normalizedKey, cancellationToken);
    }
}

public sealed class GetOrderDraft(IOrderDraftStore store)
{
    public Task<OrderDraftSnapshot?> ExecuteAsync(
        TenantContext tenantContext,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (orderId == Guid.Empty)
        {
            throw new OrderDraftValidationException(
                "order_id_invalid",
                "Order identity cannot be empty.");
        }

        return store.FindAsync(tenantContext, orderId, cancellationToken);
    }
}

public sealed record OrderDraftIntent(
    string Summary,
    string CurrencyCode,
    IReadOnlyList<OrderDraftLine> Lines,
    decimal Total,
    string Fingerprint)
{
    public static OrderDraftIntent Create(CreateOrderDraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var summary = OrderDraftRules.NormalizeRequiredText(
            request.Summary,
            200,
            "summary_invalid",
            "Summary is required and cannot exceed 200 characters.");
        var currencyCode = OrderDraftRules.NormalizeCurrencyCode(request.CurrencyCode);

        if (request.Lines is null || request.Lines.Count is < 1 or > 100)
        {
            throw new OrderDraftValidationException(
                "lines_invalid",
                "An order draft requires between 1 and 100 lines.");
        }

        var lines = new OrderDraftLine[request.Lines.Count];
        decimal total = 0;
        for (var index = 0; index < request.Lines.Count; index++)
        {
            var input = request.Lines[index]
                ?? throw new OrderDraftValidationException("line_invalid", "Order lines cannot be null.");
            var description = OrderDraftRules.NormalizeRequiredText(
                input.Description,
                300,
                "line_description_invalid",
                "Line description is required and cannot exceed 300 characters.");
            var unitCode = OrderDraftRules.NormalizeCode(
                input.UnitCode,
                1,
                16,
                "unit_code_invalid",
                "Unit code must contain between 1 and 16 ASCII letters or digits.");
            OrderDraftRules.RequirePositiveDecimal(input.Quantity, "quantity_invalid");
            OrderDraftRules.RequireNonNegativeDecimal(input.UnitPrice, "unit_price_invalid");

            decimal lineTotal;
            try
            {
                lineTotal = decimal.Round(
                    checked(input.Quantity * input.UnitPrice),
                    4,
                    MidpointRounding.ToEven);
                OrderDraftRules.RequireNonNegativeDecimal(lineTotal, "line_total_invalid");
                total = checked(total + lineTotal);
                OrderDraftRules.RequireNonNegativeDecimal(total, "order_total_invalid");
            }
            catch (OverflowException)
            {
                throw new OrderDraftValidationException(
                    "amount_out_of_range",
                    "The order amount exceeds the supported range.");
            }

            lines[index] = new OrderDraftLine(
                index + 1,
                description,
                input.Quantity,
                unitCode,
                input.UnitPrice,
                lineTotal);
        }

        return new OrderDraftIntent(
            summary,
            currencyCode,
            lines,
            total,
            ComputeFingerprint(summary, currencyCode, lines));
    }

    private static string ComputeFingerprint(
        string summary,
        string currencyCode,
        OrderDraftLine[] lines)
    {
        var canonical = new StringBuilder();
        Append(canonical, summary);
        Append(canonical, currencyCode);
        canonical.Append(lines.Length.ToString(CultureInfo.InvariantCulture)).Append(':');
        foreach (var line in lines)
        {
            Append(canonical, line.Description);
            Append(canonical, line.Quantity.ToString("G29", CultureInfo.InvariantCulture));
            Append(canonical, line.UnitCode);
            Append(canonical, line.UnitPrice.ToString("G29", CultureInfo.InvariantCulture));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder target, string value) =>
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
}

internal static class OrderDraftRules
{
    private const decimal MaximumDecimal19Scale4 = 999_999_999_999_999.9999m;

    internal static string NormalizeIdempotencyKey(string value)
    {
        var normalized = NormalizeRequiredText(
            value,
            128,
            "idempotency_key_invalid",
            "Idempotency-Key is required and cannot exceed 128 characters.");
        if (normalized.Any(character => char.IsControl(character)))
        {
            throw new OrderDraftValidationException(
                "idempotency_key_invalid",
                "Idempotency-Key cannot contain control characters.");
        }

        return normalized;
    }

    internal static string NormalizeRequiredText(
        string value,
        int maximumLength,
        string code,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new OrderDraftValidationException(code, message);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength || !HasWellFormedUtf16(normalized))
        {
            throw new OrderDraftValidationException(code, message);
        }

        return normalized;
    }

    internal static string NormalizeCode(
        string value,
        int minimumLength,
        int maximumLength,
        string code,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new OrderDraftValidationException(code, message);
        }

        var normalized = value.Trim();
        if (!HasWellFormedUtf16(normalized))
        {
            throw new OrderDraftValidationException(code, message);
        }

        normalized = normalized.ToUpperInvariant();
        if (
            normalized.Length < minimumLength ||
            normalized.Length > maximumLength ||
            normalized.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new OrderDraftValidationException(code, message);
        }

        return normalized;
    }

    internal static string NormalizeCurrencyCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new OrderDraftValidationException(
                "currency_code_invalid",
                "Currency code must contain exactly three ASCII letters.");
        }

        var normalized = value.Trim();
        if (!HasWellFormedUtf16(normalized))
        {
            throw new OrderDraftValidationException(
                "currency_code_invalid",
                "Currency code must contain exactly three ASCII letters.");
        }

        normalized = normalized.ToUpperInvariant();
        if (
            normalized.Length != 3 ||
            normalized.Any(character => !char.IsAsciiLetter(character)))
        {
            throw new OrderDraftValidationException(
                "currency_code_invalid",
                "Currency code must contain exactly three ASCII letters.");
        }

        return normalized;
    }

    internal static void RequirePositiveDecimal(decimal value, string code)
    {
        if (value <= 0 || !HasSupportedPrecisionAndRange(value))
        {
            throw new OrderDraftValidationException(
                code,
                "The decimal value is outside the supported precision, scale, or range.");
        }
    }

    internal static void RequireNonNegativeDecimal(decimal value, string code)
    {
        if (value < 0 || !HasSupportedPrecisionAndRange(value))
        {
            throw new OrderDraftValidationException(
                code,
                "The decimal value is outside the supported precision, scale, or range.");
        }
    }

    private static bool HasSupportedPrecisionAndRange(decimal value) =>
        value <= MaximumDecimal19Scale4 && GetScale(value) <= 4;

    private static bool HasWellFormedUtf16(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 == value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    return false;
                }

                index++;
            }
            else if (char.IsLowSurrogate(character))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetScale(decimal value) =>
        (decimal.GetBits(value)[3] >> 16) & 0x7F;
}
