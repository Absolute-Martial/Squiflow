using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.Tenancy;

namespace Application.Customers;

public sealed record CustomerOrganizationSnapshot(
    Guid OrganizationId,
    Guid TenantId,
    string DisplayName,
    DateTimeOffset CreatedAt);

public sealed record CustomerProgramSnapshot(
    Guid ProgramId,
    Guid TenantId,
    Guid OrganizationId,
    string DisplayName,
    DateTimeOffset CreatedAt);

public sealed record CreateCustomerOrganizationRequest(string DisplayName);

public sealed record CreateCustomerProgramRequest(Guid OrganizationId, string DisplayName);

public enum CreateCustomerOrganizationStatus
{
    Created = 1,
    Replayed = 2,
    IdempotencyKeyConflict = 3,
}

public enum CreateCustomerProgramStatus
{
    Created = 1,
    Replayed = 2,
    ParentNotFound = 3,
    IdempotencyKeyConflict = 4,
}

public sealed record CreateCustomerOrganizationResult(
    CreateCustomerOrganizationStatus Status,
    CustomerOrganizationSnapshot? Organization);

public sealed record CreateCustomerProgramResult(
    CreateCustomerProgramStatus Status,
    CustomerProgramSnapshot? Program);

public sealed record CustomerOrganizationIntent(string DisplayName, string Fingerprint)
{
    public static CustomerOrganizationIntent Create(CreateCustomerOrganizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = CustomerRules.NormalizeDisplayName(request.DisplayName);
        return new CustomerOrganizationIntent(name, CustomerRules.Fingerprint("organization", name));
    }
}

public sealed record CustomerProgramIntent(Guid OrganizationId, string DisplayName, string Fingerprint)
{
    public static CustomerProgramIntent Create(CreateCustomerProgramRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CustomerRules.RequireIdentity(request.OrganizationId, "organization_id_invalid");
        var name = CustomerRules.NormalizeDisplayName(request.DisplayName);
        return new CustomerProgramIntent(
            request.OrganizationId,
            name,
            CustomerRules.Fingerprint("program", request.OrganizationId.ToString("N"), name));
    }
}

public sealed record CustomerOrganizationPageCursor(DateTimeOffset CreatedAt, Guid OrganizationId);

public sealed record CustomerProgramPageCursor(DateTimeOffset CreatedAt, Guid ProgramId);

public sealed record ListCustomerOrganizationsRequest(int Limit, CustomerOrganizationPageCursor? After);

public sealed record ListCustomerProgramsRequest(
    Guid OrganizationId,
    int Limit,
    CustomerProgramPageCursor? After);

public sealed record CustomerOrganizationPage(
    IReadOnlyList<CustomerOrganizationSnapshot> Items,
    CustomerOrganizationPageCursor? NextCursor);

public sealed record CustomerProgramPage(
    IReadOnlyList<CustomerProgramSnapshot> Items,
    CustomerProgramPageCursor? NextCursor);

public sealed record CustomerOrderContext(Guid OrganizationId, Guid? ProgramId);

public sealed class CustomerValidationException(string code, string message) : ArgumentException(message)
{
    public string Code { get; } = code;
}

// The adapter owns atomic receipt and entity persistence, including tenant isolation.
// Receipt scope is tenant, current account, operation kind, and normalized key.
public interface ICustomerStore
{
    Task<CreateCustomerOrganizationResult> CreateOrganizationAsync(
        TenantContext tenantContext,
        CustomerOrganizationIntent intent,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<CreateCustomerProgramResult> CreateProgramAsync(
        TenantContext tenantContext,
        CustomerProgramIntent intent,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<CustomerOrganizationSnapshot?> FindOrganizationAsync(
        TenantContext tenantContext,
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<CustomerProgramSnapshot?> FindProgramAsync(
        TenantContext tenantContext,
        Guid programId,
        CancellationToken cancellationToken);

    Task<CustomerOrganizationPage> ListOrganizationsAsync(
        TenantContext tenantContext,
        ListCustomerOrganizationsRequest request,
        CancellationToken cancellationToken);

    Task<CustomerProgramPage> ListProgramsAsync(
        TenantContext tenantContext,
        ListCustomerProgramsRequest request,
        CancellationToken cancellationToken);

    Task<CustomerOrderContext?> ResolveOrderContextAsync(
        TenantContext tenantContext,
        Guid organizationId,
        Guid? programId,
        CancellationToken cancellationToken);
}

public sealed class CreateCustomerOrganization(ICustomerStore store)
{
    public Task<CreateCustomerOrganizationResult> ExecuteAsync(
        TenantContext tenantContext,
        CreateCustomerOrganizationRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        var key = CustomerRules.NormalizeIdempotencyKey(idempotencyKey);
        var intent = CustomerOrganizationIntent.Create(request);
        return store.CreateOrganizationAsync(tenantContext, intent, key, cancellationToken);
    }
}

public sealed class CreateCustomerProgram(ICustomerStore store)
{
    public Task<CreateCustomerProgramResult> ExecuteAsync(
        TenantContext tenantContext,
        CreateCustomerProgramRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        var key = CustomerRules.NormalizeIdempotencyKey(idempotencyKey);
        var intent = CustomerProgramIntent.Create(request);
        return store.CreateProgramAsync(tenantContext, intent, key, cancellationToken);
    }
}

public sealed class GetCustomerOrganization(ICustomerStore store)
{
    public Task<CustomerOrganizationSnapshot?> ExecuteAsync(
        TenantContext tenantContext,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        CustomerRules.RequireIdentity(organizationId, "organization_id_invalid");
        return store.FindOrganizationAsync(tenantContext, organizationId, cancellationToken);
    }
}

public sealed class GetCustomerProgram(ICustomerStore store)
{
    public Task<CustomerProgramSnapshot?> ExecuteAsync(
        TenantContext tenantContext,
        Guid programId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        CustomerRules.RequireIdentity(programId, "program_id_invalid");
        return store.FindProgramAsync(tenantContext, programId, cancellationToken);
    }
}

public sealed class ListCustomerOrganizations(ICustomerStore store)
{
    public Task<CustomerOrganizationPage> ExecuteAsync(
        TenantContext tenantContext,
        ListCustomerOrganizationsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CustomerRules.RequirePageSize(request.Limit);
        if (request.After is { } cursor &&
            (cursor.OrganizationId == Guid.Empty || !CustomerRules.IsUtcTimestamp(cursor.CreatedAt)))
        {
            throw new CustomerValidationException("cursor_invalid", "The page cursor is invalid.");
        }

        return store.ListOrganizationsAsync(tenantContext, request, cancellationToken);
    }
}

public sealed class ListCustomerPrograms(ICustomerStore store)
{
    public Task<CustomerProgramPage> ExecuteAsync(
        TenantContext tenantContext,
        ListCustomerProgramsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(request);
        CustomerRules.RequireIdentity(request.OrganizationId, "organization_id_invalid");
        CustomerRules.RequirePageSize(request.Limit);
        if (request.After is { } cursor &&
            (cursor.ProgramId == Guid.Empty || !CustomerRules.IsUtcTimestamp(cursor.CreatedAt)))
        {
            throw new CustomerValidationException("cursor_invalid", "The page cursor is invalid.");
        }

        return store.ListProgramsAsync(tenantContext, request, cancellationToken);
    }
}

public sealed class ResolveCustomerOrderContext(ICustomerStore store)
{
    public Task<CustomerOrderContext?> ExecuteAsync(
        TenantContext tenantContext,
        Guid organizationId,
        Guid? programId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        CustomerRules.RequireIdentity(organizationId, "organization_id_invalid");
        if (programId.HasValue)
        {
            CustomerRules.RequireIdentity(programId.Value, "program_id_invalid");
        }

        return store.ResolveOrderContextAsync(tenantContext, organizationId, programId, cancellationToken);
    }
}

internal static class CustomerRules
{
    internal static string NormalizeDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CustomerValidationException(
                "display_name_invalid",
                "Display name is required and cannot exceed 200 characters.");
        }

        if (!HasWellFormedUtf16(value) || value.Any(char.IsControl))
        {
            throw new CustomerValidationException(
                "display_name_invalid",
                "Display name is required and cannot exceed 200 characters.");
        }

        var trimmed = value.Trim();
        var normalized = trimmed.Normalize(NormalizationForm.FormC);
        if (normalized.Length > 200)
        {
            throw new CustomerValidationException(
                "display_name_invalid",
                "Display name is required and cannot exceed 200 characters.");
        }

        return normalized;
    }

    internal static string NormalizeIdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CustomerValidationException(
                "idempotency_key_invalid",
                "Idempotency-Key is required and cannot exceed 128 characters.");
        }

        var normalized = value.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl) || !HasWellFormedUtf16(normalized))
        {
            throw new CustomerValidationException(
                "idempotency_key_invalid",
                "Idempotency-Key is required and cannot exceed 128 characters.");
        }

        return normalized;
    }

    internal static string Fingerprint(string kind, params string[] values)
    {
        var canonical = new StringBuilder("v1:").Append(kind).Append(':');
        foreach (var value in values)
        {
            canonical.Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(value);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    internal static void RequireIdentity(Guid identity, string code)
    {
        if (identity == Guid.Empty)
        {
            throw new CustomerValidationException(code, "Identity cannot be empty.");
        }
    }

    internal static void RequirePageSize(int limit)
    {
        if (limit is < 1 or > 50)
        {
            throw new CustomerValidationException("page_size_invalid", "Page size must be between 1 and 50.");
        }
    }

    internal static bool IsUtcTimestamp(DateTimeOffset timestamp) =>
        timestamp != default && timestamp.Offset == TimeSpan.Zero;

    private static bool HasWellFormedUtf16(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (++index == value.Length || !char.IsLowSurrogate(value[index]))
                {
                    return false;
                }
            }
            else if (char.IsLowSurrogate(value[index]))
            {
                return false;
            }
        }

        return true;
    }
}
