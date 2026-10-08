using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.ObjectStorage;
using Application.Tenancy;

namespace Application.Customers;

public enum CustomerDuplicateEvidenceKind
{
    NormalizedEmail = 1,
    NormalizedPhone = 2,
    ExternalRegistrationId = 3,
    ExactName = 4,
    OrganizationRelationship = 5,
    ProgramRelationship = 6,
    FuzzyNameDiscovery = 7,
}

public static class CustomerIdentityNormalization
{
    public static string Name(string value) => MatchText(value, 200);

    public static string Email(string value) => MatchText(value, 254);

    public static string Phone(string value)
    {
        if (value.Length > 32 || !value.All(ch => char.IsAsciiDigit(ch) || ch is '+' or '-' or ' ' or '(' or ')')
            || value.Count(ch => ch == '+') > 1 || (value.Contains('+') && !value.TrimStart().StartsWith('+')))
            throw new CustomerValidationException("phone_invalid", "Phone must contain ASCII digits and an optional leading plus.");
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 3 or > 15)
            throw new CustomerValidationException("phone_invalid", "Phone must contain 3 to 15 ASCII digits.");
        return digits;
    }

    public static string? Reason(string? value)
    {
        if (value is null) return null;
        if (!CustomerRules.HasWellFormedUtf16(value) || value.Any(char.IsControl) || value.Trim().Length is 0 or > 1000)
            throw new CustomerValidationException("reason_invalid", "Reason must contain 1 to 1000 printable characters.");
        return value.Trim().Normalize(NormalizationForm.FormC);
    }

    public static string ExternalRegistrationId(string value) => MatchText(value, 200);

    private static string MatchText(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || !CustomerRules.HasWellFormedUtf16(value) || value.Any(char.IsControl))
            throw new CustomerValidationException("identity_signal_invalid", "Identity signal is invalid.");
        var normalized = value.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        if (normalized.Length > maximumLength)
            throw new CustomerValidationException("identity_signal_invalid", "Normalized identity signal exceeds the supported length.");
        return normalized;
    }

    public static string Fingerprint(string kind, params string[] values)
    {
        var canonical = new StringBuilder("v1:").Append(kind).Append(':');
        foreach (var value in values)
            canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    public static Guid StableGuid(params Guid[] identities)
    {
        var bytes = SHA256.HashData(identities.SelectMany(identity => identity.ToByteArray()).ToArray());
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes[..16]);
    }
}

public sealed record CustomerDuplicateEvidence(CustomerDuplicateEvidenceKind Kind);

public sealed record FindCustomerDuplicatesRequest(
    CustomerDuplicateSignals Signals,
    Guid? ExcludeCustomerId = null,
    int Limit = 25,
    bool IncludeFuzzyNameDiscovery = false);

public sealed record CustomerDuplicateCandidate(
    Guid CaseId,
    CustomerIndividualSnapshot Customer,
    IReadOnlyList<CustomerDuplicateEvidence> Evidence,
    bool RequiresManualReview,
    CustomerDuplicateResolutionSnapshot? Resolution = null);

public sealed record CustomerDuplicateMatchRecord(
    CustomerIndividualSnapshot Customer,
    CustomerDuplicateSignals Signals);

public static class CustomerDuplicateMatcher
{
    public static bool IsFuzzyNameDiscovery(string left, string right)
    {
        if (left.Length > 200 || right.Length > 200)
            throw new CustomerValidationException("identity_signal_invalid", "Fuzzy discovery requires bounded name signals.");
        return !string.Equals(left, right, StringComparison.Ordinal) && Levenshtein(left, right) <= 2;
    }

    public static IReadOnlyList<CustomerDuplicateCandidate> Discover(
        Guid tenantId,
        CustomerDuplicateSignals target,
        IEnumerable<CustomerDuplicateMatchRecord> records,
        Guid? excludeCustomerId,
        int limit,
        bool includeFuzzyNameDiscovery)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(records);
        CustomerRules.RequirePageSize(limit);
        var matches = new List<CustomerDuplicateCandidate>();
        foreach (var record in records)
        {
            if (excludeCustomerId == record.Customer.IndividualId || record.Customer.TenantId != tenantId) continue;
            // A consolidated source no longer exists as a candidate identity; its successor
            // carries the same signals and is matched on its own.
            if (record.Customer.RedirectTargetIndividualId.HasValue) continue;
            var evidence = Evidence(target, record.Signals, includeFuzzyNameDiscovery);
            if (evidence.Count == 0) continue;
            matches.Add(new(
                CustomerIdentityNormalization.StableGuid(tenantId, record.Customer.IndividualId),
                record.Customer,
                evidence,
                true));
        }
        return matches
            .OrderByDescending(candidate => candidate.Evidence.Count)
            .ThenBy(candidate => candidate.Customer.IndividualId)
            .Take(limit)
            .ToArray();
    }

    private static List<CustomerDuplicateEvidence> Evidence(
        CustomerDuplicateSignals target,
        CustomerDuplicateSignals candidate,
        bool includeFuzzy)
    {
        var evidence = new List<CustomerDuplicateEvidence>();
        if (target.NormalizedEmail is not null && target.NormalizedEmail == candidate.NormalizedEmail)
            evidence.Add(new(CustomerDuplicateEvidenceKind.NormalizedEmail));
        if (target.NormalizedPhone is not null && target.NormalizedPhone == candidate.NormalizedPhone)
            evidence.Add(new(CustomerDuplicateEvidenceKind.NormalizedPhone));
        if (target.NormalizedExternalRegistrationId is not null
            && target.NormalizedExternalRegistrationId == candidate.NormalizedExternalRegistrationId)
            evidence.Add(new(CustomerDuplicateEvidenceKind.ExternalRegistrationId));
        if (target.NormalizedName == candidate.NormalizedName)
            evidence.Add(new(CustomerDuplicateEvidenceKind.ExactName));
        if (target.OrganizationId.HasValue && target.OrganizationId == candidate.OrganizationId)
            evidence.Add(new(CustomerDuplicateEvidenceKind.OrganizationRelationship));
        if (target.ProgramId.HasValue && target.ProgramId == candidate.ProgramId)
            evidence.Add(new(CustomerDuplicateEvidenceKind.ProgramRelationship));
        if (includeFuzzy && IsFuzzyNameDiscovery(target.NormalizedName, candidate.NormalizedName))
            evidence.Add(new(CustomerDuplicateEvidenceKind.FuzzyNameDiscovery));
        return evidence;
    }

    private static int Levenshtein(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var row = 1; row <= left.Length; row++)
        {
            var current = new int[right.Length + 1]; current[0] = row;
            for (var column = 1; column <= right.Length; column++)
                current[column] = Math.Min(Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + (left[row - 1] == right[column - 1] ? 0 : 1));
            previous = current;
        }
        return previous[right.Length];
    }
}

public sealed record CustomerDuplicateSearchResult(
    IReadOnlyList<CustomerDuplicateCandidate> Candidates,
    bool HasMore);

public interface ICustomerDuplicateDiscoveryStore
{
    Task<CustomerDuplicateSearchResult> FindPotentialDuplicatesAsync(
        TenantContext context,
        FindCustomerDuplicatesRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerDuplicateResolutionSnapshot>> ReadResolutionsAsync(
        TenantContext context, Guid customerId, Guid? afterResolutionId, int limit, CancellationToken cancellationToken);
}

public enum CustomerDuplicateOutcome
{
    PotentialDuplicate = 1,
    KeepSeparate = 2,
    ConsolidateInto = 3,
    Dismiss = 4,
}

public sealed record ResolveCustomerDuplicateRequest(
    Guid CustomerId,
    Guid OtherCustomerId,
    long ExpectedCustomerRevision,
    long ExpectedOtherRevision,
    CustomerDuplicateOutcome Outcome,
    string? Reason = null);

// Revisions bind the two explicit physical identities. A redirected target returns
// RevisionConflict and its actual survivor ID; resubmit with that survivor's current
// revision. Receipt replay always returns the original outcome/survivor instead.
public sealed record ConsolidateCustomerDuplicateRequest(
    Guid SourceCustomerId,
    Guid CanonicalCustomerId,
    long ExpectedSourceRevision,
    long ExpectedCanonicalRevision,
    string? Reason = null);

public enum ResolveCustomerDuplicateStatus
{
    Resolved = 1,
    Replayed = 2,
    NotFound = 3,
    RevisionConflict = 4,
    AlreadyResolved = 5,
    InvalidOutcome = 6,
    IdempotencyKeyConflict = 7,
    // Either side was consolidated away from being a current customer. A review decision
    // cannot be recorded about an identity that no longer exists, so the caller must
    // deliberately resubmit against the current successor; retained receipts still replay.
    AlreadyRedirected = 8,
}

public enum ConsolidateCustomerDuplicateStatus
{
    Consolidated = 1,
    Replayed = 2,
    NotFound = 3,
    RevisionConflict = 4,
    AlreadyRedirected = 5,
    CycleDetected = 6,
    InvalidTarget = 7,
    IdempotencyKeyConflict = 8,
}

public sealed record CustomerDuplicateResolutionSnapshot(
    Guid ResolutionId,
    Guid CustomerId,
    Guid OtherCustomerId,
    CustomerDuplicateOutcome Outcome,
    Guid ActorAccountId,
    DateTimeOffset ResolvedAt,
    string? Reason,
    string Evidence = "");

public sealed record ResolveCustomerDuplicateResult(
    ResolveCustomerDuplicateStatus Status,
    CustomerDuplicateResolutionSnapshot? Resolution);

public sealed record ConsolidateCustomerDuplicateResult(
    ConsolidateCustomerDuplicateStatus Status,
    CustomerDuplicateResolutionSnapshot? Resolution,
    Guid? CanonicalCustomerId);

// These are deliberately separate ports. Resolve may record a review decision;
// only the consolidation port may create a redirect.
public interface ICustomerDuplicateResolutionStore
{
    Task<ResolveCustomerDuplicateResult> ResolveDuplicateAsync(
        TenantContext context,
        ResolveCustomerDuplicateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public interface ICustomerDuplicateConsolidationStore
{
    Task<ConsolidateCustomerDuplicateResult> ConsolidateDuplicateAsync(
        TenantContext context,
        ConsolidateCustomerDuplicateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public sealed class FindCustomerDuplicates(ICustomerDuplicateDiscoveryStore store)
{
    public Task<CustomerDuplicateSearchResult> ExecuteAsync(
        TenantContext context,
        FindCustomerDuplicatesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Signals);
        CustomerRules.RequirePageSize(request.Limit);
        if (request.ExcludeCustomerId is { } id) CustomerRules.RequireIdentity(id, "customer_id_invalid");
        return store.FindPotentialDuplicatesAsync(context, request, cancellationToken);
    }
}

public sealed class ResolveCustomerDuplicate(ICustomerDuplicateResolutionStore store)
{
    public Task<ResolveCustomerDuplicateResult> ExecuteAsync(
        TenantContext context,
        ResolveCustomerDuplicateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        CustomerRules.RequireIdentity(request.CustomerId, "customer_id_invalid");
        CustomerRules.RequireIdentity(request.OtherCustomerId, "other_customer_id_invalid");
        if (request.CustomerId == request.OtherCustomerId)
            throw new CustomerValidationException("duplicate_pair_invalid", "A customer cannot be compared with itself.");
        if (request.ExpectedCustomerRevision < 1 || request.ExpectedOtherRevision < 1)
            throw new CustomerValidationException("revision_invalid", "Expected revisions must be positive.");
        if (request.Outcome is CustomerDuplicateOutcome.ConsolidateInto)
            throw new CustomerValidationException("consolidation_requires_separate_authority",
                "Consolidation must use the Customers.Duplicates.Consolidate authority.");
        if (request.Outcome is not (CustomerDuplicateOutcome.PotentialDuplicate
            or CustomerDuplicateOutcome.KeepSeparate or CustomerDuplicateOutcome.Dismiss))
            throw new CustomerValidationException("duplicate_outcome_invalid", "The duplicate outcome is invalid.");
        return store.ResolveDuplicateAsync(context, request with { Reason = CustomerIdentityNormalization.Reason(request.Reason) },
            CustomerRules.NormalizeIdempotencyKey(idempotencyKey), cancellationToken);
    }
}

public sealed class ConsolidateCustomerDuplicate(ICustomerDuplicateConsolidationStore store)
{
    public Task<ConsolidateCustomerDuplicateResult> ExecuteAsync(
        TenantContext context,
        ConsolidateCustomerDuplicateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        CustomerRules.RequireIdentity(request.SourceCustomerId, "source_customer_id_invalid");
        CustomerRules.RequireIdentity(request.CanonicalCustomerId, "canonical_customer_id_invalid");
        if (request.SourceCustomerId == request.CanonicalCustomerId)
            throw new CustomerValidationException("consolidation_target_invalid", "A customer cannot redirect to itself.");
        if (request.ExpectedSourceRevision < 1 || request.ExpectedCanonicalRevision < 1)
            throw new CustomerValidationException("revision_invalid", "Expected revisions must be positive.");
        return store.ConsolidateDuplicateAsync(context, request with { Reason = CustomerIdentityNormalization.Reason(request.Reason) },
            CustomerRules.NormalizeIdempotencyKey(idempotencyKey), cancellationToken);
    }
}

public enum CustomerImportRowStatus
{
    Pending = 1,
    Imported = 2,
    MappedToExisting = 3,
    Rejected = 4,
    Failed = 5,
}

public sealed record CustomerImportRowPlan(
    int RowNumber,
    string SourceRowHash,
    CustomerImportRowStatus Status,
    CustomerIndividualIntent? Intent,
    string? ErrorCode,
    string? ErrorMessage,
    Guid? RowId = null,
    Guid? CustomerId = null,
    bool RequiresDecision = false,
    string? DuplicateEvidence = null,
    CustomerImportDecisionKind? Decision = null,
    Guid? MappingCustomerId = null,
    int Attempts = 0);

public sealed record CustomerImportPlan(
    Guid ImportId,
    string ContractVersion,
    string ManifestHash,
    long ByteLength,
    IReadOnlyList<CustomerImportRowPlan> Rows,
    string Fingerprint)
{
    public int PendingRowCount => Rows.Count(row => row.Status == CustomerImportRowStatus.Pending);
    public int RejectedRowCount => Rows.Count(row => row.Status == CustomerImportRowStatus.Rejected);
}

public enum CustomerImportRetention
{
    DefaultSevenDays = 1,
    TenantArchived = 2,
}

public sealed record CustomerImportSourcePolicy(string ProviderScope, long MaximumRetainedBytes)
{
    public static CustomerImportSourcePolicy Create(string providerScope, long maximumRetainedBytes)
    {
        if (string.IsNullOrWhiteSpace(providerScope) || providerScope.Length > 256 || maximumRetainedBytes <= 0)
            throw new CustomerValidationException("import_storage_policy_invalid", "Import source storage policy is invalid.");
        return new(providerScope.Trim(), maximumRetainedBytes);
    }
}

public enum CustomerImportSourceState
{
    Staged = 1,
    Quarantined = 2,
    Available = 3,
    Unavailable = 4,
    Orphaned = 5,
    Retired = 6,
    RetirementPending = 7,
}

public sealed record CustomerImportSourceSnapshot(
    string ObjectKey,
    string ProviderScope,
    long ByteLength,
    string Sha256,
    string ContentType,
    CustomerImportSourceState State,
    CustomerImportRetention Retention,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    string? FailureCode);

public sealed record CustomerImportSourceLease(
    Guid ReservationId,
    string ObjectKey,
    bool UploadRequired,
    CustomerImportSourceSnapshot? ExistingSource);

public sealed record CustomerImportSourceRetirementLease(
    string ObjectKey,
    string ProviderScope,
    long ByteLength,
    string Sha256,
    long Generation = 0,
    Guid LeaseId = default,
    DateTimeOffset? LeaseExpiresAt = null)
{
    public static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);
}

public enum CustomerImportSourceFailureKind
{
    ProvenAbsent = 1,
    OutcomeUnknown = 2,
}

public interface ICustomerImportSourceStore
{
    Task<CustomerImportSourceLease> BeginSourceAsync(
        TenantContext context,
        CustomerImportPlan plan,
        string idempotencyKey,
        CustomerImportRetention retention,
        CustomerImportSourcePolicy policy,
        CancellationToken cancellationToken);

    Task<CreateCustomerImportResult> CompleteSourceAsync(
        TenantContext context,
        CustomerImportPlan plan,
        string idempotencyKey,
        CustomerImportRetention retention,
        CustomerImportSourcePolicy policy,
        CustomerImportSourceLease lease,
        CancellationToken cancellationToken);

    Task MarkSourceFailureAsync(
        TenantContext context,
        CustomerImportSourceLease lease,
        CustomerImportSourceFailureKind failureKind,
        string failureCode,
        CancellationToken cancellationToken);

    Task<CustomerImportSourceSnapshot?> ReadSourceAsync(
        TenantContext context,
        Guid importId,
        CancellationToken cancellationToken);

    Task<CustomerImportSourceRetirementLease?> ClaimExpiredSourceRetirementAsync(
        TenantContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task CompleteSourceRetirementAsync(
        TenantContext context,
        CustomerImportSourceRetirementLease lease,
        bool deleted,
        string? failureCode,
        CancellationToken cancellationToken);
}

// Source retirement is hosted maintenance, not a tenant command. It is discovered
// through the bounded executor and still uses the same tenant-scoped PostgreSQL
// session and provider outcome contract.
public interface ICustomerImportSourceMaintenanceStore
{
    Task<CustomerImportSourceRetirementLease?> ClaimExpiredSourceRetirementAsync(
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task CompleteSourceRetirementAsync(
        Guid tenantId,
        CustomerImportSourceRetirementLease lease,
        bool deleted,
        string? failureCode,
        CancellationToken cancellationToken);
}

public sealed class ReconcileCustomerImportSource(
    ICustomerImportSourceMaintenanceStore source,
    IObjectStore objectStore)
{
    public async Task<bool> RetireOneExpiredAsync(
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        CustomerRules.RequireIdentity(tenantId, "tenant_id_invalid");
        var lease = await source.ClaimExpiredSourceRetirementAsync(tenantId, now, cancellationToken).ConfigureAwait(false);
        if (lease is null) return false;
        var result = await objectStore.DeleteAsync(ObjectStoreKey.Create(lease.ObjectKey), cancellationToken).ConfigureAwait(false);
        var deleted = result.Outcome is ObjectStoreDeleteOutcome.Deleted or ObjectStoreDeleteOutcome.NotFound;
        await source.CompleteSourceRetirementAsync(tenantId, lease, deleted,
            deleted ? null : result.Outcome.ToString(), cancellationToken).ConfigureAwait(false);
        return deleted;
    }
}

public sealed record CreateCustomerImportResult(
    Guid ImportId,
    bool Created,
    bool Replayed,
    bool IdempotencyKeyConflict,
    CustomerImportPlan Plan);

public sealed record ExecuteCustomerImportRequest(
    Guid ImportId,
    IReadOnlyDictionary<int, Guid>? ExplicitMappings = null,
    IReadOnlyList<CustomerImportDecision>? Decisions = null);

public enum CustomerImportDecisionKind { CreateNew = 1, MapToExisting = 2, Reject = 3 }
public sealed record CustomerImportDecision(int RowNumber, string SourceRowHash,
    CustomerImportDecisionKind Kind, Guid? CustomerId = null);
public sealed record CustomerImportRowPage(IReadOnlyList<CustomerImportRowPlan> Items, int? NextRowNumber);
public sealed record CustomerImportSummary(Guid ImportId, string ContractVersion, string ManifestHash,
    int RowCount, int Pending, int Imported, int MappedToExisting, int Rejected, int Failed,
    CustomerImportWorkSnapshot? Work, CustomerImportSourceSnapshot? Source = null);

public enum CustomerImportWorkStatus
{
    Accepted = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
}

public sealed record CustomerImportWorkSnapshot(
    Guid WorkId,
    Guid ImportId,
    CustomerImportWorkStatus Status,
    int ProcessedRows,
    int RemainingRows,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? LastError);

public sealed record ExecuteCustomerImportResult(
    CustomerImportWorkSnapshot Work,
    IReadOnlyList<CustomerImportRowPlan> Rows);

public interface ICustomerImportStore
{
    Task<CreateCustomerImportResult> CreateImportPlanAsync(
        TenantContext context,
        CustomerImportPlan plan,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<ExecuteCustomerImportResult> ExecuteImportAsync(
        TenantContext context,
        ExecuteCustomerImportRequest request,
        string idempotencyKey,
        long authorizationRevision,
        CancellationToken cancellationToken);

    Task<CustomerImportSummary?> ReadImportSummaryAsync(TenantContext context, Guid importId, CancellationToken cancellationToken);
    Task<CustomerImportRowPage> ReadImportRowsPageAsync(TenantContext context, Guid importId,
        int afterRowNumber, int limit, CancellationToken cancellationToken);
}

public sealed class CreateCustomerImport(
    ICustomerImportStore store,
    ICustomerImportSourceStore? sourceStore = null,
    IObjectStore? objectStore = null,
    CustomerImportSourcePolicy? sourcePolicy = null)
{
    public async Task<CreateCustomerImportResult> ExecuteAsync(
        TenantContext context,
        Stream csv,
        Guid? importId,
        string idempotencyKey,
        CancellationToken cancellationToken,
        CustomerImportRetention retention = CustomerImportRetention.DefaultSevenDays)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(csv);
        var planId = importId ?? Guid.CreateVersion7();
        if (sourceStore is null || objectStore is null || sourcePolicy is null)
        {
            var plan = await CustomerImportCsv.PlanAsync(csv, planId, cancellationToken).ConfigureAwait(false);
            return await store.CreateImportPlanAsync(context, plan,
                CustomerRules.NormalizeIdempotencyKey(idempotencyKey), cancellationToken).ConfigureAwait(false);
        }

        var stagedPath = Path.Combine(Path.GetTempPath(), $"application-import-{Guid.CreateVersion7():N}.tmp");
        try
        {
            await StageAsync(csv, stagedPath, cancellationToken).ConfigureAwait(false);
            await using var staged = new FileStream(stagedPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var plan = await CustomerImportCsv.PlanAsync(staged, planId, cancellationToken).ConfigureAwait(false);
            var normalizedKey = CustomerRules.NormalizeIdempotencyKey(idempotencyKey);
            var lease = await sourceStore.BeginSourceAsync(context, plan, normalizedKey, retention, sourcePolicy, cancellationToken)
                .ConfigureAwait(false);
            if (lease.UploadRequired)
            {
                staged.Position = 0;
                var put = await objectStore.PutAsync(new(ObjectStoreKey.Create(lease.ObjectKey), staged,
                    plan.ByteLength, plan.ManifestHash, "text/csv; charset=utf-8"), cancellationToken).ConfigureAwait(false);
                if (put.Outcome is not (ObjectStorePutOutcome.Created or ObjectStorePutOutcome.AlreadyExists))
                {
                    var failure = put.Outcome is ObjectStorePutOutcome.OutcomeUnknown or ObjectStorePutOutcome.TimedOut
                        ? CustomerImportSourceFailureKind.OutcomeUnknown
                        : CustomerImportSourceFailureKind.ProvenAbsent;
                    await sourceStore.MarkSourceFailureAsync(context, lease, failure, put.Outcome.ToString(), cancellationToken)
                        .ConfigureAwait(false);
                    throw new CustomerValidationException("import_source_unavailable", "The import source could not be retained safely.");
                }
            }

            try
            {
                return await sourceStore.CompleteSourceAsync(context, plan, normalizedKey, retention, sourcePolicy, lease, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    await sourceStore.MarkSourceFailureAsync(context, lease, CustomerImportSourceFailureKind.OutcomeUnknown,
                        "metadata_commit_unknown", CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception) { }
                throw;
            }
            catch (Exception)
            {
                try
                {
                    await sourceStore.MarkSourceFailureAsync(context, lease, CustomerImportSourceFailureKind.OutcomeUnknown,
                        "metadata_commit_unknown", CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception) { }
                throw;
            }
        }
        finally
        {
            try { File.Delete(stagedPath); } catch (IOException) { }
        }
    }

    private static async Task StageAsync(Stream source, string path, CancellationToken cancellationToken)
    {
        if (!source.CanRead) throw new CustomerValidationException("import_unreadable", "The import stream cannot be read.");
        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > CustomerImportCsv.MaxBytes)
                throw new CustomerValidationException("import_too_large", "The import exceeds the 10 MiB limit.");
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }
}

public sealed class ExecuteCustomerImport(ICustomerImportStore store, ICustomerImportAuthority authority)
{
    public async Task<ExecuteCustomerImportResult> ExecuteAsync(
        TenantContext context,
        ExecuteCustomerImportRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        CustomerRules.RequireIdentity(request.ImportId, "import_id_invalid");
        if (request.ExplicitMappings is not null)
        {
            foreach (var pair in request.ExplicitMappings)
            {
                if (pair.Key < 2 || pair.Value == Guid.Empty)
                    throw new CustomerValidationException("mapping_invalid", "Import mapping is invalid.");
            }
        }
        var current = await authority.CheckAsync(context.TenantId, context.AccountId, cancellationToken).ConfigureAwait(false);
        if (current is null || current.Context.TenantId != context.TenantId || current.Context.AccountId != context.AccountId
            || current.AuthorizationRevision < 1) throw new CustomerImportAuthorityException();
        return await store.ExecuteImportAsync(current.Context, request,
            CustomerRules.NormalizeIdempotencyKey(idempotencyKey), current.AuthorizationRevision, cancellationToken).ConfigureAwait(false);
    }
}

public static class CustomerImportCsv
{
    public const string ContractVersion = "customer-import/v1";
    public const int MaxRows = 10_000;
    public const int MaxBytes = 10 * 1024 * 1024;
    private const int MaximumRawFieldCharacters = 4096;
    public const string Template = "name,external_id,customer_type,email,phone,address_line1,address_line2,city,notes\r\n";

    private static readonly HashSet<string> AllowedHeaders = new(StringComparer.Ordinal)
    {
        "name", "external_id", "customer_type", "email", "phone", "address_line1",
        "address_line2", "city", "notes",
    };

    public static async Task<CustomerImportPlan> PlanAsync(
        Stream source,
        Guid importId,
        CancellationToken cancellationToken)
    {
        CustomerRules.RequireIdentity(importId, "import_id_invalid");
        if (!source.CanRead) throw new CustomerValidationException("import_unreadable", "The import stream cannot be read.");
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > MaxBytes)
                throw new CustomerValidationException("import_too_large", "The import exceeds the 10 MiB limit.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        var bytes = buffer.ToArray();
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException)
        { throw new CustomerValidationException("import_encoding_invalid", "The import must be UTF-8."); }

        var parsed = Parse(text.TrimStart('\uFEFF'));
        if (parsed.Count == 0)
            throw new CustomerValidationException("import_header_missing", "The import must contain a header.");
        var headers = parsed[0].Fields;
        if (!headers.Contains("name", StringComparer.Ordinal))
            throw new CustomerValidationException("import_name_header_missing", "The name header is required.");
        if (headers.Count != headers.Distinct(StringComparer.Ordinal).Count()
            || headers.Any(header => !AllowedHeaders.Contains(header)))
            throw new CustomerValidationException("import_header_invalid", "The import header contains unsupported or duplicate fields.");

        var rows = new List<CustomerImportRowPlan>(Math.Min(MaxRows, parsed.Count - 1));
        for (var index = 1; index < parsed.Count; index++)
        {
            if (rows.Count == MaxRows)
                throw new CustomerValidationException("import_row_limit_exceeded", "The import exceeds the 10,000 row limit.");
            var values = parsed[index].Fields;
            if (values.Count != headers.Count)
            {
                rows.Add(Rejected(index + 1, values, "row_shape_invalid", "The row has a different number of fields than the header."));
                continue;
            }
            var value = headers.Select((header, column) => (header, value: values[column]))
                .ToDictionary(item => item.header, item => item.value, StringComparer.Ordinal);
            try
            {
                var intent = CustomerIndividualIntent.Create(new(
                    Required(value, "name"), Optional(value, "email"), Optional(value, "phone"),
                    Optional(value, "external_id"), Optional(value, "address_line1"),
                    Optional(value, "address_line2"), Optional(value, "city"), Optional(value, "notes"),
                    Optional(value, "customer_type")));
                rows.Add(new(index + 1, RowHash(values), CustomerImportRowStatus.Pending, intent, null, null));
            }
            catch (CustomerValidationException exception)
            {
                rows.Add(Rejected(index + 1, values, exception.Code, exception.Message));
            }
        }

        var manifest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var fingerprint = CustomerRules.Fingerprint("customer-import", ContractVersion, manifest,
            string.Join('|', rows.Select(row => $"{row.RowNumber}:{row.SourceRowHash}:{(int)row.Status}")));
        return new(importId, ContractVersion, manifest, bytes.LongLength,
            new ReadOnlyCollection<CustomerImportRowPlan>(rows), fingerprint);
    }

    private static CustomerImportRowPlan Rejected(int rowNumber, IReadOnlyList<string> values, string code, string message) =>
        new(rowNumber, RowHash(values), CustomerImportRowStatus.Rejected, null, code, message);

    private static string Required(Dictionary<string, string> values, string key) => values[key];

    private static string? Optional(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && value.Length > 0 ? value : null;

    private static string RowHash(IEnumerable<string> values) =>
        CustomerIdentityNormalization.Fingerprint("csv-row/v1", values.ToArray());

    private sealed record ParsedRow(IReadOnlyList<string> Fields);

    private static List<ParsedRow> Parse(string text)
    {
        var rows = new List<ParsedRow>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var quoteClosed = false;
        var rowHasValue = false;
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (field.Length > MaximumRawFieldCharacters || fields.Count > AllowedHeaders.Count)
                throw new CustomerValidationException("import_csv_invalid", "CSV fields exceed the supported shape or size.");
            if (quoted)
            {
                if (current == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"') { field.Append('"'); index++; }
                    else { quoted = false; quoteClosed = true; }
                }
                else field.Append(current);
                continue;
            }
            if (quoteClosed && current is not (',' or '\r' or '\n'))
                throw new CustomerValidationException("import_csv_invalid", "Unexpected text after a quoted field.");
            if (current == '"')
            {
                if (field.Length != 0) throw new CustomerValidationException("import_csv_invalid", "Unexpected quote inside a field.");
                quoted = true; rowHasValue = true; continue;
            }
            if (current == ',') { fields.Add(field.ToString()); field.Clear(); quoteClosed = false; rowHasValue = true; continue; }
            if (current is '\r' or '\n')
            {
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                fields.Add(field.ToString()); field.Clear();
                if (rowHasValue || fields.Any(value => value.Length > 0)) rows.Add(new(fields.ToArray()));
                if (rows.Count > MaxRows + 1) throw new CustomerValidationException("import_row_limit_exceeded", "The import exceeds the row limit.");
                fields.Clear(); rowHasValue = false; quoteClosed = false; continue;
            }
            field.Append(current); rowHasValue = true;
        }
        if (quoted) throw new CustomerValidationException("import_csv_invalid", "The import contains an unterminated quoted field.");
        if (rowHasValue || field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString()); rows.Add(new(fields.ToArray()));
        }
        return rows;
    }
}
