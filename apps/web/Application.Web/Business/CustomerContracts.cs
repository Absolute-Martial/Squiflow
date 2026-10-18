using System.Text.Json.Serialization;

namespace Application.Web.Business;

public sealed record OrganizationRecord([property: JsonRequired] Guid OrganizationId, [property: JsonRequired] Guid TenantId,
    string DisplayName, [property: JsonRequired] DateTimeOffset CreatedAt);
public sealed record ProgramRecord([property: JsonRequired] Guid ProgramId, [property: JsonRequired] Guid TenantId,
    [property: JsonRequired] Guid OrganizationId, string DisplayName, [property: JsonRequired] DateTimeOffset CreatedAt);
public sealed record OrganizationPage(OrganizationRecord[] Items, string? NextCursor);
public sealed record ProgramPage(ProgramRecord[] Items, string? NextCursor);
public sealed record IndividualRecord([property: JsonRequired] Guid IndividualId, string DisplayName, string? Email, string? Phone,
    string Availability, [property: JsonRequired] long Revision, [property: JsonRequired] DateTimeOffset CreatedAt,
    DateTimeOffset? AvailabilityChangedAt, DateTimeOffset? ContactChangedAt, Guid? RedirectTargetIndividualId);

public sealed record IndividualAvailability([property: JsonRequired] Guid IndividualId, string Availability,
    [property: JsonRequired] long Revision, [property: JsonRequired] DateTimeOffset AvailabilityChangedAt);

public static class CustomerContracts
{
    public static bool Organization(OrganizationRecord value, Guid tenant, Guid? id = null) =>
        value.OrganizationId != Guid.Empty && value.TenantId == tenant && (!id.HasValue || value.OrganizationId == id) &&
        BusinessValidation.Text(value.DisplayName, 200) && value.CreatedAt != default && value.CreatedAt.Offset == TimeSpan.Zero;

    public static bool Program(ProgramRecord value, Guid tenant, Guid organization, Guid? id = null) =>
        value.ProgramId != Guid.Empty && value.TenantId == tenant && value.OrganizationId == organization &&
        (!id.HasValue || value.ProgramId == id) && BusinessValidation.Text(value.DisplayName, 200) &&
        value.CreatedAt != default && value.CreatedAt.Offset == TimeSpan.Zero;

    public static bool Individual(IndividualRecord value, Guid? id = null) => value.IndividualId != Guid.Empty &&
        (!id.HasValue || value.IndividualId == id) && BusinessValidation.Text(value.DisplayName, 200) &&
        BusinessValidation.Text(value.Email, 254, false) && BusinessValidation.Text(value.Phone, 32, false) &&
        value.Availability is "active" or "inactive" && value.Revision > 0 && value.CreatedAt != default &&
        value.CreatedAt.Offset == TimeSpan.Zero && value.RedirectTargetIndividualId != Guid.Empty && value.RedirectTargetIndividualId != value.IndividualId;
}

public sealed record RepresentativeRecord([property: JsonRequired] Guid RepresentativeId, [property: JsonRequired] Guid OrganizationId,
    Guid? ProgramId, [property: JsonRequired] Guid IndividualId, string Availability, [property: JsonRequired] long Revision,
    [property: JsonRequired] DateTimeOffset CreatedAt, DateTimeOffset? ChangedAt);
