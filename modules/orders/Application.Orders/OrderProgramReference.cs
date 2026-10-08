using System.Buffers;
using System.Globalization;
using System.Text;
using Application.Tenancy;

namespace Application.Orders;

public sealed record OrderProgramPolicyFacts(
    Guid ProfileId,
    Guid PolicyRevisionId,
    bool RequireReferenceForProgramOrders);

public static class OrderProgramReference
{
    public const int MaximumLength = 128;

    public static string NormalizeIdempotencyKey(string input) => OrderDraftRules.NormalizeIdempotencyKey(input);

    public static string? Normalize(string? input)
    {
        if (input is null)
        {
            return null;
        }

        for (var offset = 0; offset < input.Length;)
        {
            var status = Rune.DecodeFromUtf16(input.AsSpan(offset), out var rune, out var charsConsumed);
            if (status != OperationStatus.Done || Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control)
            {
                throw new ArgumentException("The external program reference is invalid.", nameof(input));
            }

            offset += charsConsumed;
        }

        var normalized = input.Trim();
        if (normalized.Length == 0)
        {
            return null;
        }

        var scalarCount = 0;
        foreach (var _ in normalized.EnumerateRunes())
        {
            if (++scalarCount > MaximumLength)
            {
                throw new ArgumentException("The external program reference is invalid.", nameof(input));
            }
        }

        return normalized;
    }

    public static void RequireValidStored(OrderProgramPolicyFacts policy, string? externalProgramReference)
    {
        ArgumentNullException.ThrowIfNull(policy);
        try
        {
            if (policy.ProfileId == Guid.Empty || policy.PolicyRevisionId == Guid.Empty ||
                !string.Equals(Normalize(externalProgramReference), externalProgramReference, StringComparison.Ordinal))
                throw new InvalidOperationException("The stored Order program reference is invalid.");
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("The stored Order program reference is invalid.");
        }
    }

    public static bool IsMissing(OrderDraftSnapshot order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return order.ProgramPolicy?.RequireReferenceForProgramOrders == true &&
            order.CustomerContext?.ProgramId is not null &&
            order.ExternalProgramReference is null;
    }
}

public sealed record SetOrderProgramReferenceRequest(
    Guid OrderId,
    long ExpectedRevision,
    string? ExternalProgramReference);

public enum SetOrderProgramReferenceStatus
{
    Updated = 1,
    Replayed = 2,
    NotFound = 3,
    RevisionConflict = 4,
    AlreadyCommitted = 5,
    AlreadyAbandoned = 6,
    IdempotencyKeyConflict = 7,
    ProfileUnavailable = 8,
}

public sealed record SetOrderProgramReferenceResult(
    SetOrderProgramReferenceStatus Status,
    OrderDraftSnapshot? Order);

public interface IOrderProgramReferenceStore
{
    Task<SetOrderProgramReferenceResult> SetProgramReferenceAsync(
        TenantContext tenantContext,
        SetOrderProgramReferenceRequest request,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken);
}
