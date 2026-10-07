using Microsoft.EntityFrameworkCore;

namespace Application.Customers.Postgres;

public sealed class CustomerDbContext(DbContextOptions<CustomerDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        CustomerModelV202609240001.Build(modelBuilder);
        CustomerIndividualModel.Build(modelBuilder);
        CustomerRepresentativeModel.Build(modelBuilder);
        CustomerDuplicateModel.Build(modelBuilder);
        CustomerImportModel.Build(modelBuilder);
    }
}

internal sealed class CustomerOrganizationRow
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class CustomerProgramRow
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class OrganizationReceiptRow
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class ProgramReceiptRow
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public Guid ProgramId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class CustomerTenantReferenceRow
{
    public Guid Id { get; set; }
}

internal sealed class CustomerAccountReferenceRow
{
    public Guid Id { get; set; }
}

internal sealed class CustomerIndividualRow
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int Availability { get; set; }
    public long Revision { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? AvailabilityChangedByAccountId { get; set; }
    public DateTimeOffset? AvailabilityChangedAt { get; set; }
    public Guid? ContactChangedByAccountId { get; set; }
    public DateTimeOffset? ContactChangedAt { get; set; }
    public string CustomerType { get; set; } = "individual";
    public string? ExternalRegistrationId { get; set; }
    public string? NormalizedName { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? NormalizedPhone { get; set; }
    public string? NormalizedExternalRegistrationId { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Notes { get; set; }
    public Guid? RedirectTargetIndividualId { get; set; }
}

internal sealed class CustomerIndividualCommandReceiptRow
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public Guid IndividualId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int Availability { get; set; }
    public long Revision { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? AvailabilityChangedByAccountId { get; set; }
    public DateTimeOffset? AvailabilityChangedAt { get; set; }
    public Guid? ContactChangedByAccountId { get; set; }
    public DateTimeOffset? ContactChangedAt { get; set; }
}

internal sealed class CustomerDuplicateCaseRow
{
    public Guid TenantId { get; set; }
    public Guid CaseId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid OtherCustomerId { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public int? Outcome { get; set; }
    public Guid? ResolvedByAccountId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Reason { get; set; }
}

internal sealed class CustomerDuplicateReceiptRow
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public Guid ResolutionId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid OtherCustomerId { get; set; }
    public int Outcome { get; set; }
    public DateTimeOffset ResolvedAt { get; set; }
    public string? Reason { get; set; }
    public string Evidence { get; set; } = string.Empty;
}

internal sealed class CustomerRedirectRow
{
    public Guid TenantId { get; set; }
    public Guid SourceCustomerId { get; set; }
    public Guid CanonicalCustomerId { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class CustomerImportRow
{
    public Guid TenantId { get; set; }
    public Guid ImportId { get; set; }
    public Guid AccountId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public Guid CreatedByAccountId { get; set; }
    public string ContractVersion { get; set; } = string.Empty;
    public string ManifestHash { get; set; } = string.Empty;
    public long ByteLength { get; set; }
    public int RowCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class CustomerImportItemRow
{
    public Guid TenantId { get; set; }
    public Guid ImportId { get; set; }
    public int RowNumber { get; set; }
    public string SourceRowHash { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? CustomerType { get; set; }
    public string? ExternalRegistrationId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Notes { get; set; }
    public int Status { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public Guid RowId { get; set; }
    public bool RequiresDecision { get; set; }
    public string? DuplicateEvidence { get; set; }
    public int? Decision { get; set; }
    public Guid? MappingCustomerId { get; set; }
    public int Attempts { get; set; }
    public string? NameSignal { get; set; }
    public string? EmailSignal { get; set; }
    public string? PhoneSignal { get; set; }
    public string? ExternalIdSignal { get; set; }
}

internal sealed class CustomerImportWorkRow
{
    public Guid TenantId { get; set; }
    public Guid WorkId { get; set; }
    public Guid ImportId { get; set; }
    public Guid AccountId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastError { get; set; }
    public long AuthorizationRevision { get; set; }
    public long Generation { get; set; }
    public Guid? WorkerId { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
}

internal sealed class CustomerRepresentativeRow
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid IndividualId { get; set; }
    public int Availability { get; set; }
    public long Revision { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ChangedByAccountId { get; set; }
    public DateTimeOffset? ChangedAt { get; set; }
}

internal sealed class CustomerRepresentativeCommandReceiptRow
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public Guid RepresentativeId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid IndividualId { get; set; }
    public int Availability { get; set; }
    public long Revision { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ChangedByAccountId { get; set; }
    public DateTimeOffset? ChangedAt { get; set; }
}
