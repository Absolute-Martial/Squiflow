using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Invoices.Postgres.Migrations;

[DbContext(typeof(InvoicesDbContext))]
[Migration("20261015120000_CustomerSettlement")]
public sealed class CustomerSettlement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        using var resource = typeof(CustomerSettlement).Assembly.GetManifestResourceStream("Application.Invoices.Postgres.Sql.CustomerSettlement.sql")
            ?? throw new InvalidOperationException("The customer settlement migration resource is missing.");
        using var reader = new StreamReader(resource);
        migrationBuilder.Sql(reader.ReadToEnd());
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Customer financial history cannot be discarded by a destructive rollback.");
}
