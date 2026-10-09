namespace Application.Orders.Postgres;

internal static partial class OrderSql
{
    internal static readonly string OrderApprovalsSchema = Load(nameof(OrderApprovalsSchema));
    internal static readonly string FindApproval = Load(nameof(FindApproval));
    internal static readonly string FindPendingApproval = Load(nameof(FindPendingApproval));
    internal static readonly string FindApprovalReceipt = Load(nameof(FindApprovalReceipt));
    internal static readonly string InsertApproval = Load(nameof(InsertApproval));
    internal static readonly string UpdateApproval = Load(nameof(UpdateApproval));
    internal static readonly string InsertApprovalEvent = Load(nameof(InsertApprovalEvent));
    internal static readonly string ReadApprovalRevisionAuthor = Load(nameof(ReadApprovalRevisionAuthor));
    internal static readonly string ListApprovals = Load(nameof(ListApprovals));
    internal static readonly string ListApprovalHistory = Load(nameof(ListApprovalHistory));
    internal static readonly string FindApprovedOrderSubject = Load(nameof(FindApprovedOrderSubject));
    internal static readonly string InsertApprovalCommitEvidence = Load(nameof(InsertApprovalCommitEvidence));
    internal static readonly string ReadApprovalCommitEvidence = Load(nameof(ReadApprovalCommitEvidence));
}
