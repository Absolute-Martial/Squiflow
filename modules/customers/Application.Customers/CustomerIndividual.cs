using Application.Tenancy;

namespace Application.Customers;

public enum CustomerIndividualAvailability
{
    Active = 1,
    Inactive = 2,
}

public sealed record CustomerIndividualSnapshot(
    Guid IndividualId,
    Guid TenantId,
    string DisplayName,
    string? Email,
    string? Phone,
    CustomerIndividualAvailability Availability,
    long Revision,
    Guid CreatedByAccountId,
    DateTimeOffset CreatedAt,
    Guid? AvailabilityChangedByAccountId,
    DateTimeOffset? AvailabilityChangedAt,
    Guid? ContactChangedByAccountId = null,
    DateTimeOffset? ContactChangedAt = null,
    string CustomerType = "individual",
    string? ExternalRegistrationId = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? Notes = null,
    Guid? RedirectTargetIndividualId = null);

public sealed record CreateCustomerIndividualRequest(
    string DisplayName,
    string? Email,
    string? Phone,
    string? ExternalRegistrationId = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? Notes = null,
    string? CustomerType = null);

public sealed record EditCustomerIndividualContactRequest(
    Guid IndividualId,
    long ExpectedRevision,
    string DisplayName,
    string? Email,
    string? Phone);

public sealed record ChangeCustomerIndividualAvailabilityRequest(
    Guid IndividualId, long ExpectedRevision, CustomerIndividualAvailability Availability);

public enum CreateCustomerIndividualStatus
{
    Created = 1,
    Replayed = 2,
    IdempotencyKeyConflict = 3,
}

public enum EditCustomerIndividualContactStatus
{
    Changed = 1,
    Replayed = 2,
    NotFound = 3,
    RevisionConflict = 4,
    IdempotencyKeyConflict = 5,
    NoChange = 6,
}

public enum ChangeCustomerIndividualAvailabilityStatus
{
    Changed = 1,
    Replayed = 2,
    NotFound = 3,
    RevisionConflict = 4,
    IdempotencyKeyConflict = 5,
    AlreadyInState = 6,
}

public sealed record CreateCustomerIndividualResult(
    CreateCustomerIndividualStatus Status, CustomerIndividualSnapshot? Individual);

public sealed record EditCustomerIndividualContactResult(
    EditCustomerIndividualContactStatus Status, CustomerIndividualSnapshot? Individual);

public sealed record ChangeCustomerIndividualAvailabilityResult(
    ChangeCustomerIndividualAvailabilityStatus Status, CustomerIndividualSnapshot? Individual);

public sealed record CustomerIndividualIntent(
    string DisplayName,
    string? Email,
    string? Phone,
    string CustomerType,
    string? ExternalRegistrationId,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Notes,
    string Fingerprint)
{
    public static CustomerIndividualIntent Create(CreateCustomerIndividualRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = CustomerRules.NormalizeDisplayName(request.DisplayName);
        var email = NormalizeContact(request.Email, 254, "email_invalid");
        var phone = NormalizeContact(request.Phone, 32, "phone_invalid");
        var externalId = NormalizeOptionalText(request.ExternalRegistrationId, 200, "external_registration_id_invalid");
        var addressLine1 = NormalizeOptionalText(request.AddressLine1, 200, "address_invalid");
        var addressLine2 = NormalizeOptionalText(request.AddressLine2, 200, "address_invalid");
        var city = NormalizeOptionalText(request.City, 120, "address_invalid");
        var notes = NormalizeOptionalText(request.Notes, 4000, "notes_invalid");
        var customerType = NormalizeCustomerType(request.CustomerType);
        if (email is not null && (email.Any(char.IsWhiteSpace)
            || !System.Net.Mail.MailAddress.TryCreate(email, out var parsed)
            || !string.Equals(parsed.Address, email, StringComparison.Ordinal)))
        {
            throw new CustomerValidationException("email_invalid", "Email must be a well-formed address.");
        }
        if (phone is not null) _ = CustomerIdentityNormalization.Phone(phone);
        _ = CustomerIdentityNormalization.Name(name);
        if (email is not null) _ = CustomerIdentityNormalization.Email(email);
        if (externalId is not null) _ = CustomerIdentityNormalization.ExternalRegistrationId(externalId);
        return new(name, email, phone, customerType, externalId, addressLine1, addressLine2, city, notes,
            CustomerRules.Fingerprint("individual", name, email ?? "", phone ?? "", customerType,
                externalId ?? "", addressLine1 ?? "", addressLine2 ?? "", city ?? "", notes ?? ""));
    }

    private static string NormalizeCustomerType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "individual";
        if (!CustomerRules.HasWellFormedUtf16(value) || value.Any(char.IsControl))
            throw new CustomerValidationException("customer_type_invalid", "Customer type is invalid.");
        var normalized = value.Trim().Normalize(System.Text.NormalizationForm.FormKC).ToLowerInvariant();
        if (normalized.Length is 0 or > 32)
            throw new CustomerValidationException("customer_type_invalid", "Customer type is invalid.");
        return normalized;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string code)
    {
        if (value is null) return null;
        if (string.IsNullOrWhiteSpace(value) || !CustomerRules.HasWellFormedUtf16(value)
            || value.Any(char.IsControl))
            throw new CustomerValidationException(code, "Text is invalid or blank.");
        var normalized = value.Trim().Normalize(System.Text.NormalizationForm.FormC);
        if (normalized.Length > maxLength)
            throw new CustomerValidationException(code, "Text is invalid or too long.");
        return normalized;
    }

    private static string? NormalizeContact(string? value, int maxLength, string code)
    {
        if (value is null) return null;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CustomerValidationException(code, "Contact text cannot be blank.");
        }
        if (!CustomerRules.HasWellFormedUtf16(value))
            throw new CustomerValidationException(code, "Contact text is invalid or too long.");
        var normalized = value.Trim().Normalize(System.Text.NormalizationForm.FormC);
        if (normalized.Length > maxLength || normalized.Any(char.IsControl))
        {
            throw new CustomerValidationException(code, "Contact text is invalid or too long.");
        }
        return normalized;
    }
}

public sealed record CustomerIndividualContactIntent(
    Guid IndividualId,
    long ExpectedRevision,
    string DisplayName,
    string? Email,
    string? Phone,
    string Fingerprint)
{
    public static CustomerIndividualContactIntent Create(EditCustomerIndividualContactRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CustomerRules.RequireIdentity(request.IndividualId, "individual_id_invalid");
        if (request.ExpectedRevision < 1)
            throw new CustomerValidationException("revision_invalid", "Expected revision must be positive.");

        var normalized = CustomerIndividualIntent.Create(
            new CreateCustomerIndividualRequest(request.DisplayName, request.Email, request.Phone));
        return new(
            request.IndividualId,
            request.ExpectedRevision,
            normalized.DisplayName,
            normalized.Email,
            normalized.Phone,
            CustomerRules.Fingerprint(
                "individual_contact",
                request.IndividualId.ToString("N"),
                request.ExpectedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                normalized.DisplayName,
                normalized.Email ?? "",
                normalized.Phone ?? ""));
    }
}

public sealed record CustomerDuplicateSignals(
    string NormalizedName,
    string? NormalizedEmail,
    string? NormalizedPhone,
    string? NormalizedExternalRegistrationId,
    Guid? OrganizationId,
    Guid? ProgramId)
{
    public static CustomerDuplicateSignals Create(
        string name,
        string? email = null,
        string? phone = null,
        string? externalRegistrationId = null,
        Guid? organizationId = null,
        Guid? programId = null)
    {
        var intent = CustomerIndividualIntent.Create(new(
            name, email, phone, externalRegistrationId, null, null, null, null, null));
        if (organizationId.HasValue) CustomerRules.RequireIdentity(organizationId.Value, "organization_id_invalid");
        if (programId.HasValue) CustomerRules.RequireIdentity(programId.Value, "program_id_invalid");
        return new(
            CustomerIdentityNormalization.Name(intent.DisplayName),
            intent.Email is null ? null : CustomerIdentityNormalization.Email(intent.Email),
            intent.Phone is null ? null : CustomerIdentityNormalization.Phone(intent.Phone),
            intent.ExternalRegistrationId is null ? null : CustomerIdentityNormalization.ExternalRegistrationId(intent.ExternalRegistrationId),
            organizationId,
            programId);
    }
}

public sealed record CustomerIndividualAvailabilityIntent(
    Guid IndividualId, long ExpectedRevision, CustomerIndividualAvailability Availability, string Fingerprint)
{
    public static CustomerIndividualAvailabilityIntent Create(ChangeCustomerIndividualAvailabilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CustomerRules.RequireIdentity(request.IndividualId, "individual_id_invalid");
        if (request.ExpectedRevision < 1)
            throw new CustomerValidationException("revision_invalid", "Expected revision must be positive.");
        if (!Enum.IsDefined(request.Availability))
            throw new CustomerValidationException("availability_invalid", "Availability is invalid.");
        return new(request.IndividualId, request.ExpectedRevision, request.Availability,
            CustomerRules.Fingerprint("individual_availability", request.IndividualId.ToString("N"),
                request.ExpectedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ((int)request.Availability).ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
}

public interface ICustomerIndividualStore
{
    Task<CreateCustomerIndividualResult> CreateIndividualAsync(TenantContext context,
        CustomerIndividualIntent intent, string idempotencyKey, CancellationToken cancellationToken);

    Task<CustomerIndividualSnapshot?> FindIndividualAsync(TenantContext context,
        Guid individualId, CancellationToken cancellationToken);

    Task<ChangeCustomerIndividualAvailabilityResult> ChangeIndividualAvailabilityAsync(TenantContext context,
        CustomerIndividualAvailabilityIntent intent, string idempotencyKey, CancellationToken cancellationToken);
}

public interface ICustomerIndividualContactStore
{
    Task<EditCustomerIndividualContactResult> EditIndividualContactAsync(
        TenantContext context,
        CustomerIndividualContactIntent intent,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public sealed class CreateCustomerIndividual(ICustomerIndividualStore store)
{
    public Task<CreateCustomerIndividualResult> ExecuteAsync(TenantContext context,
        CreateCustomerIndividualRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var key = CustomerRules.NormalizeIdempotencyKey(idempotencyKey);
        return store.CreateIndividualAsync(context, CustomerIndividualIntent.Create(request), key, cancellationToken);
    }
}

public sealed class GetCustomerIndividual(ICustomerIndividualStore store)
{
    public Task<CustomerIndividualSnapshot?> ExecuteAsync(TenantContext context,
        Guid individualId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        CustomerRules.RequireIdentity(individualId, "individual_id_invalid");
        return store.FindIndividualAsync(context, individualId, cancellationToken);
    }
}

public sealed class EditCustomerIndividualContact(ICustomerIndividualContactStore store)
{
    public Task<EditCustomerIndividualContactResult> ExecuteAsync(
        TenantContext context,
        EditCustomerIndividualContactRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var key = CustomerRules.NormalizeIdempotencyKey(idempotencyKey);
        return store.EditIndividualContactAsync(
            context,
            CustomerIndividualContactIntent.Create(request),
            key,
            cancellationToken);
    }
}

public sealed class ChangeCustomerIndividualAvailability(ICustomerIndividualStore store)
{
    public Task<ChangeCustomerIndividualAvailabilityResult> ExecuteAsync(TenantContext context,
        ChangeCustomerIndividualAvailabilityRequest request, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var key = CustomerRules.NormalizeIdempotencyKey(idempotencyKey);
        return store.ChangeIndividualAvailabilityAsync(context,
            CustomerIndividualAvailabilityIntent.Create(request), key, cancellationToken);
    }
}
