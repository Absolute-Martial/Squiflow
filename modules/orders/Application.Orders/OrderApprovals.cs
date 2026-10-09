using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Tenancy;

namespace Application.Orders;

public enum OrderApprovalState { Pending = 1, Approved = 2, Rejected = 3, Canceled = 4, Superseded = 5 }
public enum OrderApprovalDecision { Approve = 1, Reject = 2 }
public enum OrderApprovalChange { Submitted = 1, Approved = 2, Rejected = 3, Reassigned = 4, Canceled = 5, Superseded = 6 }
public enum OrderApprovalStatus
{
    Applied = 1, Replayed = 2, NotFound = 3, RevisionConflict = 4, IdempotencyKeyConflict = 5,
    Forbidden = 6, IneligibleApprover = 7, SelfApprovalForbidden = 8, SubjectStale = 9,
    PendingRequestExists = 10, TerminalRequest = 11, OrderNotDraft = 12,
    ProfileUnavailable = 13, ProgramReferenceRequired = 14, CommercialFactsConflict = 15,
}

public sealed record OrderApprovalAuthority(long AuthorizationRevision, bool CanSubmit, bool CanView,
    bool CanDecide, bool CanRecover, bool IsCurrentInitialOwner, bool HasRecentAuthentication, bool CanViewOrder = false)
{
    public bool MayRecover => CanView && CanRecover && IsCurrentInitialOwner && HasRecentAuthentication;
}

// The host supplies fresh active membership, independent permissions and recent Owner authentication.
public interface IOrderApprovalAuthority
{
    Task<OrderApprovalAuthority> ResolveAsync(TenantContext context, CancellationToken cancellationToken);
    Task<bool> IsEligibleApproverAsync(TenantContext context, Guid accountId, CancellationToken cancellationToken);
}

public sealed record OrderApprovalSubject(int SchemaVersion, OrderDraftSnapshot Order,
    Guid RevisionAuthorAccountId, string Fingerprint);
public sealed record OrderApprovalSnapshot(Guid RequestId, Guid TenantId, Guid OrderId,
    Guid RequestedByAccountId, Guid AssignedAccountId, long Revision, OrderApprovalState State,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, OrderApprovalSubject Subject,
    Guid LastActorAccountId, long AuthorizationRevision, OrderApprovalChange Change, string? Reason);
public sealed record OrderApprovalEvidence(Guid RequestId, long RequestRevision, long OrderRevision,
    string SubjectFingerprint, Guid ApprovedByAccountId, DateTimeOffset ApprovedAt, long AuthorizationRevision);
public sealed record OrderApprovalResult(OrderApprovalStatus Status, OrderApprovalSnapshot? Request);
public sealed class OrderApprovalAccessDeniedException : Exception
{
    public OrderApprovalAccessDeniedException() : base("The Order approval operation is not authorized.") { }
}
public sealed record SubmitOrderApprovalRequest(Guid OrderId, long ExpectedOrderRevision, Guid AssignedAccountId,
    long? ExpectedPendingRequestRevision = null);
public sealed record DecideOrderApprovalRequest(Guid RequestId, long ExpectedRequestRevision,
    OrderApprovalDecision Decision, string? Reason = null);
public sealed record ReassignOrderApprovalRequest(Guid RequestId, long ExpectedRequestRevision,
    Guid AssignedAccountId, string Reason);
public sealed record CancelOrderApprovalRequest(Guid RequestId, long ExpectedRequestRevision,
    string Reason, bool Recover = false);
public sealed record OrderApprovalCursor(DateTimeOffset CreatedAt, Guid RequestId);
public sealed record ListOrderApprovalsRequest(int Limit = 25, OrderApprovalCursor? After = null, bool Recovery = false);
public sealed record OrderApprovalListItem(Guid RequestId, Guid OrderId, long OrderRevision, long RequestRevision,
    OrderApprovalState State, Guid RequestedByAccountId, Guid AssignedAccountId, DateTimeOffset CreatedAt,
    bool SubjectStale, bool AssigneeUnavailable);
public sealed record OrderApprovalPage(IReadOnlyList<OrderApprovalListItem> Items, OrderApprovalCursor? NextCursor);
public sealed record ListOrderApprovalHistoryRequest(int Limit = 10, long? BeforeRevision = null);
public sealed record OrderApprovalHistoryPage(long CurrentRevision, IReadOnlyList<OrderApprovalSnapshot> Items, long? NextRevision);

public interface IOrderApprovalStore
{
    Task<CommitOrderDraftStatus> AssessAdmissionAsync(TenantContext context, Guid orderId, long expectedOrderRevision, CancellationToken ct);
    Task<OrderApprovalResult> SubmitApprovalAsync(TenantContext context, SubmitOrderApprovalRequest request, string key, CancellationToken ct);
    Task<OrderApprovalResult> DecideApprovalAsync(TenantContext context, DecideOrderApprovalRequest request, string key, CancellationToken ct);
    Task<OrderApprovalResult> ReassignApprovalAsync(TenantContext context, ReassignOrderApprovalRequest request, string key, CancellationToken ct);
    Task<OrderApprovalResult> CancelApprovalAsync(TenantContext context, CancelOrderApprovalRequest request, string key, CancellationToken ct);
    Task<OrderApprovalSnapshot?> FindApprovalAsync(TenantContext context, Guid requestId, CancellationToken ct);
    Task<OrderApprovalPage> ListApprovalsAsync(TenantContext context, ListOrderApprovalsRequest request, CancellationToken ct);
    Task<OrderApprovalHistoryPage?> ListApprovalHistoryAsync(TenantContext context, Guid requestId, ListOrderApprovalHistoryRequest request, CancellationToken ct);
}

public static class OrderApprovalRules
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool IsRequired(OrderDraftSnapshot order) =>
        order.CustomerContext?.ProgramId is not null && order.ProgramPolicy?.RequireApprovalForProgramOrders == true;

    public static OrderApprovalSubject CreateSubject(OrderDraftSnapshot order, Guid revisionAuthor)
    {
        RequireValidDraft(order);
        if (revisionAuthor == Guid.Empty) throw Invalid();
        return new(1, order, revisionAuthor, Fingerprint(new { schemaVersion = 1, order, revisionAuthor }));
    }

    public static bool Matches(OrderApprovalSubject subject, OrderDraftSnapshot order) =>
        JsonElement.DeepEquals(JsonSerializer.SerializeToElement(subject.Order, JsonOptions),
            JsonSerializer.SerializeToElement(order, JsonOptions));

    public static bool IsIndependent(OrderApprovalSnapshot request, Guid actor) =>
        actor != Guid.Empty && actor != request.RequestedByAccountId && actor != request.Subject.RevisionAuthorAccountId;

    public static string NormalizeReason(string? value, bool required)
    {
        var original = value ?? string.Empty;
        for (var offset = 0; offset < original.Length;)
        {
            if (Rune.DecodeFromUtf16(original.AsSpan(offset), out var rune, out var consumed) != OperationStatus.Done ||
                Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control)
                throw new OrderDraftValidationException("approval_reason_invalid", "Approval reason is invalid.");
            offset += consumed;
        }
        var normalized = original.Trim();
        if (!required && normalized.Length == 0) return string.Empty;
        var count = normalized.EnumerateRunes().Count();
        if (count is < 1 or > 512)
            throw new OrderDraftValidationException("approval_reason_invalid", "Approval reason must contain 1 to 512 Unicode scalar values.");
        return normalized;
    }

    public static string NormalizeKey(string key) => OrderDraftRules.NormalizeIdempotencyKey(key);
    public static string Fingerprint<T>(T value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();

    public static void ValidateIdentity(Guid id, long revision)
    {
        if (id == Guid.Empty || revision is < 1 or long.MaxValue)
            throw new OrderDraftValidationException("approval_request_invalid", "Approval identity and expected revision are invalid.");
    }

    public static void RequireValidStored(OrderApprovalSnapshot request)
    {
        if (request.RequestId == Guid.Empty || request.TenantId == Guid.Empty || request.OrderId == Guid.Empty ||
            request.RequestedByAccountId == Guid.Empty || request.AssignedAccountId == Guid.Empty ||
            request.LastActorAccountId == Guid.Empty || request.Revision < 1 || request.AuthorizationRevision < 1 ||
            request.CreatedAt == default || request.UpdatedAt < request.CreatedAt || request.CreatedAt.Offset != TimeSpan.Zero ||
            request.UpdatedAt.Offset != TimeSpan.Zero || !Enum.IsDefined(request.State) || !Enum.IsDefined(request.Change) ||
            request.Subject is null || request.Subject.SchemaVersion != 1 || request.Subject.Order is null ||
            request.TenantId != request.Subject.Order.TenantId || request.OrderId != request.Subject.Order.OrderId)
            throw Invalid();
        var subject = CreateSubject(request.Subject.Order, request.Subject.RevisionAuthorAccountId);
        if (subject.Fingerprint != request.Subject.Fingerprint && !MatchesLegacySubjectFingerprint(request.Subject) ||
            !IsIndependent(request, request.AssignedAccountId) ||
            (request.Revision == 1) != (request.Change == OrderApprovalChange.Submitted) ||
            (request.Change == OrderApprovalChange.Submitted &&
                (request.State != OrderApprovalState.Pending || request.LastActorAccountId != request.RequestedByAccountId || request.UpdatedAt != request.CreatedAt)) ||
            (request.Change == OrderApprovalChange.Reassigned && request.State != OrderApprovalState.Pending) ||
            (request.Change == OrderApprovalChange.Approved && request.State != OrderApprovalState.Approved) ||
            (request.Change == OrderApprovalChange.Rejected && request.State != OrderApprovalState.Rejected) ||
            (request.Change == OrderApprovalChange.Canceled && request.State != OrderApprovalState.Canceled) ||
            (request.Change == OrderApprovalChange.Superseded && request.State != OrderApprovalState.Superseded) ||
            (request.Change is OrderApprovalChange.Approved or OrderApprovalChange.Rejected &&
                (request.LastActorAccountId != request.AssignedAccountId || !IsIndependent(request, request.LastActorAccountId))))
            throw Invalid();
        var reasonRequired = request.Change is OrderApprovalChange.Rejected or OrderApprovalChange.Reassigned or OrderApprovalChange.Canceled;
        try
        {
            if ((request.Reason ?? string.Empty) != NormalizeReason(request.Reason, reasonRequired)) throw Invalid();
        }
        catch (OrderDraftValidationException) { throw Invalid(); }
    }

    private static bool MatchesLegacySubjectFingerprint(OrderApprovalSubject subject)
    {
        // Version-1 approval subjects predate the explicit pricing envelope version. Its legacy
        // default is version 1; only that additive field may be omitted when checking old evidence.
        var document = JsonSerializer.SerializeToNode(new
        { schemaVersion = 1, order = subject.Order, revisionAuthor = subject.RevisionAuthorAccountId }, JsonOptions)!;
        foreach (var line in document["order"]!["lines"]!.AsArray())
        {
            var policy = line?["commercialFacts"]?["priceSelection"]?["explanation"]?["policy"]?.AsObject();
            if (policy is null) continue;
            if (policy["envelopeSemanticsVersion"]?.GetValue<int>() != 1) return false;
            policy.Remove("envelopeSemanticsVersion");
        }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document.ToJsonString(JsonOptions)))).ToLowerInvariant();
        return fingerprint == subject.Fingerprint;
    }

    public static void RequireValidEvidence(OrderDraftSnapshot order)
    {
        if (order.ApprovalEvidence is not { } evidence) return;
        if (order.State != OrderDraftState.Committed || !IsRequired(order) || evidence.RequestId == Guid.Empty ||
            evidence.RequestRevision < 2 || evidence.OrderRevision != order.Revision - 1 ||
            evidence.ApprovedByAccountId == Guid.Empty || evidence.AuthorizationRevision < 1 ||
            evidence.ApprovedAt == default || evidence.ApprovedAt.Offset != TimeSpan.Zero || evidence.ApprovedAt > order.CommittedAt ||
            evidence.SubjectFingerprint is not { Length: 64 } || evidence.SubjectFingerprint.Any(c => !char.IsAsciiHexDigitLower(c)))
            throw Invalid();
    }

    private static void RequireValidDraft(OrderDraftSnapshot order)
    {
        if (order.OrderId == Guid.Empty || order.TenantId == Guid.Empty || order.CreatedByAccountId == Guid.Empty ||
            order.Revision is < 1 or long.MaxValue || order.State != OrderDraftState.Draft || order.CreatedAt == default ||
            order.CreatedAt.Offset != TimeSpan.Zero || order.AbandonedAt is not null || order.AbandonedByAccountId is not null ||
            order.CommittedAt is not null || order.CommittedByAccountId is not null || order.ApprovalEvidence is not null ||
            order.ProgramPolicy is null || order.Lines is null || order.Lines.Count > 100 || order.Lines.Any(line => line is null))
            throw Invalid();
        OrderProgramReference.RequireValidStored(order.ProgramPolicy, order.ExternalProgramReference);
        if (OrderProgramReference.IsMissing(order)) throw Invalid();
        var intent = OrderDraftIntent.Create(new(order.Summary, order.CurrencyCode,
            order.Lines.Select(line => new OrderDraftLineInput(line.Description, line.Quantity, line.UnitCode, line.UnitPrice)).ToArray(),
            order.CustomerContext), catalogUnitCodes: order.Lines.Any(line => line.CommercialFacts is not null));
        if (order.Summary != intent.Summary || order.CurrencyCode != intent.CurrencyCode || order.Total != intent.Total ||
            order.Lines.Where((line, index) => line.Position != index + 1 || line.LineTotal != intent.Lines[index].LineTotal).Any())
            throw Invalid();
        if (order.QuotationOrigin is { } origin)
            new AcceptedQuotationOrder(origin, order.Summary, order.CurrencyCode, order.Total, order.Lines, order.CustomerContext).RequireValid(order.TenantId);
        OrderCommercialFactsValidation.RequireValid(order);
    }

    private static InvalidOperationException Invalid() => new("The retained Order approval evidence is invalid or unsupported.");
}
