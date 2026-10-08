using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.Tenancy;

namespace Application.Orders;

public sealed class SetOrderProgramReference(IOrderProgramReferenceStore store)
{
    public Task<SetOrderProgramReferenceResult> ExecuteAsync(TenantContext context, SetOrderProgramReferenceRequest request,
        string idempotencyKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(request);
        if (request.OrderId == Guid.Empty || request.ExpectedRevision is < 1 or long.MaxValue)
            throw new ArgumentException("The Order reference request is invalid.");
        var value = OrderProgramReference.Normalize(request.ExternalProgramReference);
        var canonical = string.Concat("order-program-reference/v1:", request.OrderId.ToString("D"), ":",
            request.ExpectedRevision.ToString(CultureInfo.InvariantCulture), ":", value?.Length.ToString(CultureInfo.InvariantCulture) ?? "0", ":", value);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return store.SetProgramReferenceAsync(context, request with { ExternalProgramReference = value },
            OrderDraftRules.NormalizeIdempotencyKey(idempotencyKey), fingerprint, ct);
    }
}
