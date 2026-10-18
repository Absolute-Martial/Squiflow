using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Application.Web.Business;

public sealed record DuplicateCustomer(Guid IndividualId, Guid TenantId, string DisplayName, string? Email, string? Phone, int Availability,
    long Revision, Guid CreatedByAccountId, DateTimeOffset CreatedAt, string CustomerType, string? ExternalRegistrationId,
    string? AddressLine1, string? AddressLine2, string? City, string? Notes, Guid? RedirectTargetIndividualId);
public sealed record DuplicateEvidence(int Kind);
public sealed record DuplicateResolution(Guid ResolutionId, Guid CustomerId, Guid OtherCustomerId, int Outcome, Guid ActorAccountId,
    DateTimeOffset ResolvedAt, string? Reason, string Evidence);
public sealed record DuplicateCandidate(Guid CaseId, DuplicateCustomer Customer, DuplicateEvidence[] Evidence,
    [property: JsonRequired] bool RequiresManualReview, DuplicateResolution? Resolution);
public sealed record DuplicateSearchPage(DuplicateCandidate[] Candidates, [property: JsonRequired] bool HasMore);
public sealed record DuplicateReceipt(int Status, DuplicateResolution? Resolution, Guid? CanonicalCustomerId);
public sealed record ImportIntent(string DisplayName, string? Email, string? Phone, string CustomerType, string? ExternalRegistrationId,
    string? AddressLine1, string? AddressLine2, string? City, string? Notes, string Fingerprint);
public sealed record ImportPlanReceipt(Guid ImportId, string ContractVersion, string ManifestHash, [property: JsonRequired] int RowCount,
    [property: JsonRequired] int PendingRows, [property: JsonRequired] int RejectedRows, [property: JsonRequired] int RowsRequiringDecision);
public sealed record ImportRow(int RowNumber, string SourceRowHash, int Status, ImportIntent? Intent, string? ErrorCode, string? ErrorMessage,
    Guid? RowId, Guid? CustomerId, [property: JsonRequired] bool RequiresDecision, string? DuplicateEvidence, int? Decision, Guid? MappingCustomerId, int Attempts);
public sealed record ImportRows(ImportRow[] Items, int? NextRowNumber);
public sealed record ImportWork(Guid WorkId, Guid ImportId, int Status, [property: JsonRequired] int ProcessedRows,
    [property: JsonRequired] int RemainingRows, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, string? LastError);
public sealed record ImportSource(long ByteLength, string Sha256, string ContentType, int State, int Retention, DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt, string? FailureCode);
public sealed record ImportSummary(Guid ImportId, string ContractVersion, string ManifestHash, [property: JsonRequired] int RowCount,
    [property: JsonRequired] int Pending, [property: JsonRequired] int Imported, [property: JsonRequired] int MappedToExisting,
    [property: JsonRequired] int Rejected, [property: JsonRequired] int Failed, ImportWork? Work, ImportSource? Source);

public static class CustomerOperationsContracts
{
    public static bool Hash(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    public static bool Customer(DuplicateCustomer value, Guid tenant) => value.IndividualId != Guid.Empty && value.TenantId == tenant &&
        value.Revision > 0 && value.Availability is 1 or 2 && value.CreatedByAccountId != Guid.Empty && value.CreatedAt != default &&
        Intent(new(value.DisplayName, value.Email, value.Phone, value.CustomerType, value.ExternalRegistrationId, value.AddressLine1,
            value.AddressLine2, value.City, value.Notes, new string('0', 64))) &&
        (value.RedirectTargetIndividualId is null || value.RedirectTargetIndividualId != Guid.Empty && value.RedirectTargetIndividualId != value.IndividualId);
    public static bool Resolution(DuplicateResolution value) => value.ResolutionId != Guid.Empty && value.CustomerId != Guid.Empty &&
        value.OtherCustomerId != Guid.Empty && value.CustomerId != value.OtherCustomerId && value.Outcome is >= 1 and <= 4 &&
        value.ActorAccountId != Guid.Empty && value.ResolvedAt != default && BusinessValidation.Text(value.Reason, 1000, false) &&
        BusinessValidation.Text(value.Evidence, 4096, false);
    public static bool Search(DuplicateSearchPage value, Guid tenant) => value.Candidates is { Length: <= 2 } &&
        value.Candidates.All(x => x is not null && x.CaseId != Guid.Empty && x.Customer is not null && Customer(x.Customer, tenant) &&
            x.Customer.RedirectTargetIndividualId is null && x.Evidence is { Length: > 0 and <= 7 } &&
            x.Evidence.All(e => e is not null && e.Kind is >= 1 and <= 7) && x.Evidence.Select(e => e.Kind).Distinct().Count() == x.Evidence.Length &&
            (x.Resolution is null || Resolution(x.Resolution) && (x.Resolution.CustomerId == x.Customer.IndividualId || x.Resolution.OtherCustomerId == x.Customer.IndividualId))) &&
        value.Candidates.Select(x => x.CaseId).Distinct().Count() == value.Candidates.Length &&
        value.Candidates.Select(x => x.Customer.IndividualId).Distinct().Count() == value.Candidates.Length;
    public static bool Receipt(DuplicateReceipt value, Guid source, Guid other, int outcome, bool consolidate) => value.Status is 1 or 2 &&
        value.Resolution is { } r && Resolution(r) && r.CustomerId == source && r.OtherCustomerId == other && r.Outcome == outcome &&
        (!consolidate || value.CanonicalCustomerId == other);
    public static bool Intent(ImportIntent value) => OperationsContracts.Text(value.DisplayName, 200) && BusinessValidation.Text(value.Email, 254, false) &&
        BusinessValidation.Text(value.Phone, 32, false) && OperationsContracts.Text(value.CustomerType, 32) &&
        BusinessValidation.Text(value.ExternalRegistrationId, 200, false) && BusinessValidation.Text(value.AddressLine1, 200, false) &&
        BusinessValidation.Text(value.AddressLine2, 200, false) && BusinessValidation.Text(value.City, 120, false) &&
        BusinessValidation.Text(value.Notes, 4000, false) && Hash(value.Fingerprint);
    public static bool Plan(ImportPlanReceipt value) => value.ImportId != Guid.Empty && value.ContractVersion == "customer-import/v1" && Hash(value.ManifestHash) &&
        value.RowCount is >= 0 and <= 10000 && value.PendingRows >= 0 && value.RejectedRows >= 0 && value.PendingRows + value.RejectedRows == value.RowCount &&
        value.RowsRequiringDecision >= 0 && value.RowsRequiringDecision <= value.PendingRows;
    public static bool Row(ImportRow value) => value.RowNumber is >= 2 and <= 10001 && Hash(value.SourceRowHash) && value.Status is >= 1 and <= 5 &&
        (value.Intent is null || Intent(value.Intent)) && (value.Status != 1 || value.Intent is not null) &&
        BusinessValidation.Text(value.ErrorCode, 256, false) && BusinessValidation.Text(value.ErrorMessage, 4096, false) &&
        BusinessValidation.Text(value.DuplicateEvidence, 4096, false) && value.RowId != Guid.Empty && value.CustomerId != Guid.Empty &&
        value.MappingCustomerId != Guid.Empty && value.Decision is null or >= 1 and <= 3 && value.Attempts is >= 0 and <= 10000;
    public static bool Rows(ImportRows value, int after, int total) => total is >= 0 and <= 10000 && after is >= 0 and <= 10001 &&
        value.Items is { Length: <= 2 } && value.Items.Length == Math.Min(2, Math.Max(0, total + 2 - Math.Max(2, after + 1))) &&
        value.Items.All(x => x is not null && Row(x)) && value.Items.Select((row, index) => row.RowNumber == Math.Max(2, after + 1) + index).All(x => x) &&
        value.NextRowNumber == (value.Items.Length > 0 && value.Items[^1].RowNumber < total + 1 ? value.Items[^1].RowNumber : (int?)null);
    public static bool Work(ImportWork value, Guid import) => value.WorkId != Guid.Empty && value.ImportId == import && value.Status is >= 1 and <= 4 &&
        value.ProcessedRows is >= 0 and <= 10000 && value.RemainingRows is >= 0 and <= 10000 &&
        (long)value.ProcessedRows + value.RemainingRows <= 10000 && value.CreatedAt != default &&
        (value.CompletedAt is null || value.CompletedAt >= value.CreatedAt) && BusinessValidation.Text(value.LastError, 4096, false);
    public static bool Summary(ImportSummary value, Guid id) => value.ImportId == id && id != Guid.Empty && value.ContractVersion == "customer-import/v1" && Hash(value.ManifestHash) &&
        value.RowCount is >= 0 and <= 10000 && value.Pending >= 0 && value.Imported >= 0 && value.MappedToExisting >= 0 && value.Rejected >= 0 && value.Failed >= 0 &&
        (long)value.Pending + value.Imported + value.MappedToExisting + value.Rejected + value.Failed == value.RowCount &&
        (value.Work is null || Work(value.Work, id) && (long)value.Work.ProcessedRows + value.Work.RemainingRows <= value.RowCount) &&
        (value.Source is null || value.Source.ByteLength is > 0 and <= BusinessApiClient.MaximumImportBytes && Hash(value.Source.Sha256) &&
            value.Source.State is >= 1 and <= 7 && value.Source.Retention is 1 or 2 && value.Source.CreatedAt != default &&
            BusinessValidation.Text(value.Source.ContentType, 128) && BusinessValidation.Text(value.Source.FailureCode, 256, false));
    public static bool Csv(BusinessCsv value, ImportSource? source = null)
    {
        if (value.Content.Length is < 1 or > BusinessApiClient.MaximumImportBytes) return false;
        try { _ = new UTF8Encoding(false, true).GetCharCount(value.Content); } catch (DecoderFallbackException) { return false; }
        return source is null || source.ByteLength == value.Content.Length && string.Equals(Convert.ToHexString(SHA256.HashData(value.Content)), source.Sha256, StringComparison.OrdinalIgnoreCase);
    }
    public static string Evidence(int kind) => kind switch { 1 => "Normalized email", 2 => "Normalized phone", 3 => "External registration ID", 4 => "Exact name", 5 => "Organization relationship", 6 => "Program relationship", 7 => "Fuzzy name discovery", _ => "Unsupported" };
    public static string Outcome(int state) => state switch { 1 => "Potential duplicate", 2 => "Keep separate", 3 => "Consolidate into survivor", 4 => "Dismiss", _ => "Unsupported" };
    public static string RowState(int state) => state switch { 1 => "Pending", 2 => "Imported", 3 => "Mapped to existing", 4 => "Rejected", 5 => "Failed", _ => "Unsupported" };
    public static string WorkState(int state) => state switch { 1 => "Accepted", 2 => "Running", 3 => "Completed", 4 => "Failed", _ => "Unsupported" };
    public static string SourceState(int state) => state switch { 1 => "Staged", 2 => "Quarantined", 3 => "Available", 4 => "Unavailable", 5 => "Orphaned", 6 => "Retired", 7 => "Retirement pending", _ => "Unsupported" };
}
