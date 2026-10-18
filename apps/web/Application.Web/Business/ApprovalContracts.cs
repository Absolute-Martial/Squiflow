using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace Application.Web.Business;

public enum ApprovalState { Pending = 1, Approved = 2, Rejected = 3, Canceled = 4, Superseded = 5 }
public enum ApprovalChange { Submitted = 1, Approved = 2, Rejected = 3, Reassigned = 4, Canceled = 5, Superseded = 6 }
public sealed record ApprovalPolicy([property: JsonRequired] Guid ProfileId, [property: JsonRequired] Guid PolicyRevisionId,
    [property: JsonRequired] bool RequireReferenceForProgramOrders, bool RequireApprovalForProgramOrders);
public sealed record ApprovalOrder([property: JsonRequired] Guid OrderId, string Summary, string CurrencyCode,
    [property: JsonRequired] decimal Total, [property: JsonRequired] long Revision, [property: JsonRequired] DateTimeOffset CreatedAt,
    OrderLine[] Lines, string State, DateTimeOffset? AbandonedAt, OrderCustomer? CustomerContext, DateTimeOffset? CommittedAt,
    OrderOrigin? QuotationOrigin, ApprovalPolicy? ProgramPolicy, string? ExternalProgramReference);
public sealed record ApprovalSubject([property: JsonRequired] int SchemaVersion, ApprovalOrder Order,
    [property: JsonRequired] Guid RevisionAuthorAccountId, string Fingerprint);
public sealed record ApprovalSummary([property: JsonRequired] Guid RequestId, [property: JsonRequired] Guid OrderId,
    [property: JsonRequired] long OrderRevision, [property: JsonRequired] long RequestRevision, [property: JsonRequired] ApprovalState State,
    [property: JsonRequired] Guid RequestedByAccountId, [property: JsonRequired] Guid AssignedAccountId,
    [property: JsonRequired] DateTimeOffset CreatedAt, [property: JsonRequired] bool SubjectStale, [property: JsonRequired] bool AssigneeUnavailable);
public sealed record ApprovalPage(ApprovalSummary[] Items, string? NextCursor);
public sealed record ApprovalDetail([property: JsonRequired] Guid RequestId, [property: JsonRequired] Guid OrderId,
    [property: JsonRequired] Guid RequestedByAccountId, [property: JsonRequired] Guid AssignedAccountId, [property: JsonRequired] long Revision,
    [property: JsonRequired] ApprovalState State, [property: JsonRequired] DateTimeOffset CreatedAt, [property: JsonRequired] DateTimeOffset UpdatedAt,
    ApprovalSubject Subject, [property: JsonRequired] Guid LastActorAccountId, [property: JsonRequired] long AuthorizationRevision,
    [property: JsonRequired] ApprovalChange Change, string? Reason);
public sealed record ApprovalReceipt([property: JsonRequired] Guid RequestId, [property: JsonRequired] Guid OrderId,
    [property: JsonRequired] long OrderRevision, [property: JsonRequired] long RequestRevision, [property: JsonRequired] ApprovalState State);
public sealed record ApprovalHistory([property: JsonRequired] long CurrentRevision, ApprovalDetail[] Items, long? NextRevision);
public sealed record ApprovalProgramReceipt([property: JsonRequired] Guid OrderId, [property: JsonRequired] long Revision,
    string? ExternalProgramReference, ApprovalPolicy ProgramPolicy);

public static class ApprovalContracts
{
    public static bool ScalarText(string? text, int maximum, bool required = true)
    {
        if (text is null) return !required;
        var count = 0;
        for (var offset = 0; offset < text.Length;)
        {
            if (Rune.DecodeFromUtf16(text.AsSpan(offset), out var rune, out var consumed) != OperationStatus.Done ||
                Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control || ++count > maximum) return false;
            offset += consumed;
        }
        return !required || !string.IsNullOrWhiteSpace(text);
    }
    public static bool Policy(ApprovalPolicy row) => row.ProfileId != Guid.Empty && row.PolicyRevisionId != Guid.Empty;
    public static bool Order(ApprovalOrder row, Guid? id = null) => OrderContracts.Record(new(row.OrderId, row.Summary, row.CurrencyCode, row.Total,
        row.Revision, row.CreatedAt, row.Lines, row.State, row.AbandonedAt, row.CustomerContext, row.CommittedAt, row.QuotationOrigin), id) &&
        (row.ProgramPolicy is null || Policy(row.ProgramPolicy)) && ScalarText(row.ExternalProgramReference, 128, false);
    public static bool Summary(ApprovalSummary row) => row.RequestId != Guid.Empty && row.OrderId != Guid.Empty && row.OrderRevision > 0 && row.RequestRevision > 0 &&
        Enum.IsDefined(row.State) && row.RequestedByAccountId != Guid.Empty && row.AssignedAccountId != Guid.Empty && row.CreatedAt != default;
    public static bool Detail(ApprovalDetail row, Guid? id = null) => row.RequestId != Guid.Empty && (!id.HasValue || row.RequestId == id) && row.OrderId != Guid.Empty &&
        row.RequestedByAccountId != Guid.Empty && row.AssignedAccountId != Guid.Empty && row.Revision > 0 && Enum.IsDefined(row.State) && Enum.IsDefined(row.Change) &&
        row.CreatedAt != default && row.UpdatedAt >= row.CreatedAt && row.LastActorAccountId != Guid.Empty && row.AuthorizationRevision > 0 && ScalarText(row.Reason, 512, false) &&
        row.Subject is { SchemaVersion: 1, Order: { } order } && Order(order, row.OrderId) && order.State == "draft" && order.ProgramPolicy is not null &&
        row.Subject.RevisionAuthorAccountId != Guid.Empty && row.Subject.Fingerprint is { Length: 64 } && row.Subject.Fingerprint.All(char.IsAsciiHexDigitLower);
    public static bool Receipt(ApprovalReceipt row, Guid? request = null, Guid? order = null) => row.RequestId != Guid.Empty && row.OrderId != Guid.Empty &&
        (!request.HasValue || row.RequestId == request) && (!order.HasValue || row.OrderId == order) && row.OrderRevision > 0 && row.RequestRevision > 0 && Enum.IsDefined(row.State);
}
