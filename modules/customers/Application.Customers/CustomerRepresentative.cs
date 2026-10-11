using System.Globalization;
using Application.Tenancy;

namespace Application.Customers;

public enum CustomerRepresentativeAvailability
{
    Active = 1,
    Inactive = 2,
}

public sealed record CustomerRepresentativeSnapshot(
    Guid RepresentativeId,
    Guid TenantId,
    Guid OrganizationId,
    Guid? ProgramId,
    Guid IndividualId,
    CustomerRepresentativeAvailability Availability,
    long Revision,
    Guid CreatedByAccountId,
    DateTimeOffset CreatedAt,
    Guid? ChangedByAccountId,
    DateTimeOffset? ChangedAt);

public sealed record LinkCustomerRepresentativeRequest(
    Guid OrganizationId,
    Guid? ProgramId,
    Guid IndividualId);

public sealed record UnlinkCustomerRepresentativeRequest(
    Guid OrganizationId,
    Guid RepresentativeId,
    long ExpectedRevision);

public enum LinkCustomerRepresentativeStatus
{
    Created = 1,
    Replayed = 2,
    TargetNotFound = 3,
    IndividualNotFound = 4,
    IndividualInactive = 5,
    AlreadyLinked = 6,
    IdempotencyKeyConflict = 7,
}

public enum UnlinkCustomerRepresentativeStatus
{
    Changed = 1,
    Replayed = 2,
    NotFound = 3,
    RevisionConflict = 4,
    AlreadyInactive = 5,
    IdempotencyKeyConflict = 6,
}

public sealed record LinkCustomerRepresentativeResult(
    LinkCustomerRepresentativeStatus Status,
    CustomerRepresentativeSnapshot? Representative);

public sealed record UnlinkCustomerRepresentativeResult(
    UnlinkCustomerRepresentativeStatus Status,
    CustomerRepresentativeSnapshot? Representative);

public sealed record CustomerRepresentativeLinkIntent(
    Guid OrganizationId,
    Guid? ProgramId,
    Guid IndividualId,
    string Fingerprint)
{
    public static CustomerRepresentativeLinkIntent Create(LinkCustomerRepresentativeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CustomerRules.RequireIdentity(request.OrganizationId, "organization_id_invalid");
        CustomerRules.RequireIdentity(request.IndividualId, "individual_id_invalid");
        if (request.ProgramId.HasValue)
            CustomerRules.RequireIdentity(request.ProgramId.Value, "program_id_invalid");

        return new(
            request.OrganizationId,
            request.ProgramId,
            request.IndividualId,
            CustomerRules.Fingerprint(
                "representative_link",
                request.OrganizationId.ToString("N"),
                request.ProgramId?.ToString("N") ?? "",
                request.IndividualId.ToString("N")));
    }
}

public sealed record CustomerRepresentativeUnlinkIntent(
    Guid OrganizationId,
    Guid RepresentativeId,
    long ExpectedRevision,
    string Fingerprint)
{
    public static CustomerRepresentativeUnlinkIntent Create(UnlinkCustomerRepresentativeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CustomerRules.RequireIdentity(request.OrganizationId, "organization_id_invalid");
        CustomerRules.RequireIdentity(request.RepresentativeId, "representative_id_invalid");
        if (request.ExpectedRevision < 1)
            throw new CustomerValidationException("revision_invalid", "Expected revision must be positive.");

        return new(
            request.OrganizationId,
            request.RepresentativeId,
            request.ExpectedRevision,
            CustomerRules.Fingerprint(
                "representative_unlink",
                request.OrganizationId.ToString("N"),
                request.RepresentativeId.ToString("N"),
                request.ExpectedRevision.ToString(CultureInfo.InvariantCulture)));
    }
}

public interface ICustomerRepresentativeStore
{
    Task<LinkCustomerRepresentativeResult> LinkRepresentativeAsync(
        TenantContext context,
        CustomerRepresentativeLinkIntent intent,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<CustomerRepresentativeSnapshot?> FindRepresentativeAsync(
        TenantContext context,
        Guid representativeId,
        CancellationToken cancellationToken);

    Task<UnlinkCustomerRepresentativeResult> UnlinkRepresentativeAsync(
        TenantContext context,
        CustomerRepresentativeUnlinkIntent intent,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public sealed class LinkCustomerRepresentative(ICustomerRepresentativeStore store)
{
    public Task<LinkCustomerRepresentativeResult> ExecuteAsync(
        TenantContext context,
        LinkCustomerRepresentativeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var key = CustomerRules.NormalizeIdempotencyKey(idempotencyKey);
        return store.LinkRepresentativeAsync(
            context,
            CustomerRepresentativeLinkIntent.Create(request),
            key,
            cancellationToken);
    }
}

public sealed class GetCustomerRepresentative(ICustomerRepresentativeStore store)
{
    public Task<CustomerRepresentativeSnapshot?> ExecuteAsync(
        TenantContext context,
        Guid representativeId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        CustomerRules.RequireIdentity(representativeId, "representative_id_invalid");
        return store.FindRepresentativeAsync(context, representativeId, cancellationToken);
    }
}

public sealed class UnlinkCustomerRepresentative(ICustomerRepresentativeStore store)
{
    public Task<UnlinkCustomerRepresentativeResult> ExecuteAsync(
        TenantContext context,
        UnlinkCustomerRepresentativeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var key = CustomerRules.NormalizeIdempotencyKey(idempotencyKey);
        return store.UnlinkRepresentativeAsync(
            context,
            CustomerRepresentativeUnlinkIntent.Create(request),
            key,
            cancellationToken);
    }
}
