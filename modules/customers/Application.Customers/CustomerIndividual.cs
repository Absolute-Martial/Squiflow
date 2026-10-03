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
    DateTimeOffset? AvailabilityChangedAt);

public sealed record CreateCustomerIndividualRequest(string DisplayName, string? Email, string? Phone);

public sealed record ChangeCustomerIndividualAvailabilityRequest(
    Guid IndividualId, long ExpectedRevision, CustomerIndividualAvailability Availability);

public enum CreateCustomerIndividualStatus
{
    Created = 1,
    Replayed = 2,
    IdempotencyKeyConflict = 3,
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

public sealed record ChangeCustomerIndividualAvailabilityResult(
    ChangeCustomerIndividualAvailabilityStatus Status, CustomerIndividualSnapshot? Individual);

public sealed record CustomerIndividualIntent(string DisplayName, string? Email, string? Phone, string Fingerprint)
{
    public static CustomerIndividualIntent Create(CreateCustomerIndividualRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = CustomerRules.NormalizeDisplayName(request.DisplayName);
        var email = NormalizeContact(request.Email, 254, "email_invalid");
        var phone = NormalizeContact(request.Phone, 32, "phone_invalid");
        if (email is not null && (email.Any(char.IsWhiteSpace)
            || !System.Net.Mail.MailAddress.TryCreate(email, out var parsed)
            || !string.Equals(parsed.Address, email, StringComparison.Ordinal)))
        {
            throw new CustomerValidationException("email_invalid", "Email must be a well-formed address.");
        }
        if (phone is not null && (!phone.Any(char.IsDigit)
            || !phone.All(ch => char.IsDigit(ch) || ch is '+' or '-' or ' ' or '(' or ')')))
        {
            throw new CustomerValidationException("phone_invalid", "Phone contains unsupported characters.");
        }
        return new(name, email, phone,
            CustomerRules.Fingerprint("individual", name, email ?? "", phone ?? ""));
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
