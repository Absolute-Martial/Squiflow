using Microsoft.EntityFrameworkCore;

namespace Application.Customers.Postgres;

public sealed class CustomerDbContext(DbContextOptions<CustomerDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        CustomerModelV202609240001.Build(modelBuilder);
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
