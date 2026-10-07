using Application.Orders;
using Application.Tenancy;

namespace Application.Quotations;

public enum QuotationResponseKind { Accepted = 1, Rejected = 2, Expired = 3 }
public sealed record QuotationResponseRequest(Guid IssuedRevisionId, long ExpectedVersion, string Evidence, string? CustomerClaim = null);
public sealed record QuotationConvertRequest(Guid IssuedRevisionId, long ExpectedVersion);
public sealed record QuotationResponseFacts(Guid QuotationId, Guid IssuedRevisionId, Guid TenantId,
    QuotationResponseKind Kind, Guid RecordedByAccountId, DateTimeOffset RecordedAt, string Evidence, string? CustomerClaim);
public sealed record QuotationConversionFacts(Guid QuotationId, Guid IssuedRevisionId, Guid TenantId,
    Guid ConvertedByAccountId, DateTimeOffset ConvertedAt, OrderDraftSnapshot OriginalOrder);
public interface IQuotationResponseStore
{
    Task<QuotationCommandResult> RespondAsync(TenantContext context, Guid quotationId, QuotationResponseKind kind,
        QuotationResponseRequest request, string key, string fingerprint, CancellationToken ct);
    Task<QuotationCommandResult> ConvertAsync(TenantContext context, Guid quotationId, QuotationConvertRequest request,
        string key, string fingerprint, CancellationToken ct);
    Task<QuotationResponseFacts?> FindResponseAsync(TenantContext context, Guid quotationId, Guid issuedRevisionId, CancellationToken ct);
}
public sealed partial class QuotationApplication
{
    private IQuotationResponseStore ResponseStore => responses ?? store as IQuotationResponseStore
        ?? throw new InvalidOperationException("Quotation response persistence is unavailable.");
    public Task<QuotationCommandResult> AcceptAsync(TenantContext context, Guid id, QuotationResponseRequest request, string key, CancellationToken ct) =>
        RespondAsync(context, id, QuotationResponseKind.Accepted, request, key, ct);
    public Task<QuotationCommandResult> RejectAsync(TenantContext context, Guid id, QuotationResponseRequest request, string key, CancellationToken ct) =>
        RespondAsync(context, id, QuotationResponseKind.Rejected, request, key, ct);
    public Task<QuotationCommandResult> ExpireAsync(TenantContext context, Guid id, QuotationResponseRequest request, string key, CancellationToken ct) =>
        RespondAsync(context, id, QuotationResponseKind.Expired, request, key, ct);
    private async Task<QuotationCommandResult> RespondAsync(TenantContext context, Guid id, QuotationResponseKind kind,
        QuotationResponseRequest request, string key, CancellationToken ct)
    {
        await pricing.RequireAsync(context, kind == QuotationResponseKind.Expired ? QuotationCapability.Expire : QuotationCapability.Respond, ct).ConfigureAwait(false);
        QuotationRules.RequireIdentity(id, request.ExpectedVersion);
        if (request.IssuedRevisionId == Guid.Empty || kind == QuotationResponseKind.Expired && request.CustomerClaim is not null)
            throw new QuotationValidationException("response_invalid", "An exact issued revision and supported response evidence are required.");
        request = request with
        {
            Evidence = QuotationRules.Text(request.Evidence, 2000, "evidence_invalid"),
            CustomerClaim = kind == QuotationResponseKind.Expired ? null : QuotationRules.Text(request.CustomerClaim!, 300, "customer_claim_invalid")
        };
        var result = await ResponseStore.RespondAsync(context, id, kind, request, QuotationRules.Key(key),
            QuotationRules.Fingerprint(new { id, kind, request }), ct).ConfigureAwait(false);
        RequireResultTenant(context, result);
        return result;
    }
    public async Task<QuotationCommandResult> ConvertAsync(TenantContext context, Guid id, QuotationConvertRequest request, string key, CancellationToken ct)
    {
        await pricing.RequireAsync(context, QuotationCapability.Convert, ct).ConfigureAwait(false);
        await pricing.RequireAsync(context, QuotationCapability.OrderCreate, ct).ConfigureAwait(false);
        QuotationRules.RequireIdentity(id, request.ExpectedVersion);
        if (request.IssuedRevisionId == Guid.Empty) throw new QuotationValidationException("response_invalid", "An exact issued revision is required.");
        var result = await ResponseStore.ConvertAsync(context, id, request, QuotationRules.Key(key),
            QuotationRules.Fingerprint(new { id, request }), ct).ConfigureAwait(false);
        RequireResultTenant(context, result);
        return result;
    }
    public async Task<QuotationResponseFacts?> FindResponseAsync(TenantContext context, Guid id, Guid revisionId, CancellationToken ct)
    {
        await pricing.RequireAsync(context, QuotationCapability.View, ct).ConfigureAwait(false);
        if (id == Guid.Empty || revisionId == Guid.Empty) throw new QuotationValidationException("quotation_id_invalid", "Exact quotation and revision identities are required.");
        var result = await ResponseStore.FindResponseAsync(context, id, revisionId, ct).ConfigureAwait(false);
        if (result is not null && (result.TenantId != context.TenantId || result.QuotationId != id || result.IssuedRevisionId != revisionId))
            throw new InvalidOperationException("The quotation response store returned foreign facts.");
        return result;
    }
    private static void RequireResultTenant(TenantContext context, QuotationCommandResult result)
    {
        if (result.Quotation is { } snapshot && snapshot.TenantId != context.TenantId)
            throw new InvalidOperationException("The quotation store returned a foreign snapshot.");
    }
}
