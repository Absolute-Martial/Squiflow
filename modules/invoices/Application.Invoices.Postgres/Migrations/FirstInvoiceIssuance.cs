using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Invoices.Postgres.Migrations;

[DbContext(typeof(InvoicesDbContext))]
[Migration("20261010120000_FirstInvoiceIssuance")]
public sealed class FirstInvoiceIssuance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        using var resource = typeof(FirstInvoiceIssuance).Assembly.GetManifestResourceStream("Application.Invoices.Postgres.Sql.FirstInvoiceIssuance.sql")
            ?? throw new InvalidOperationException("The invoice migration resource is missing.");
        using var reader = new StreamReader(resource);
        migrationBuilder.Sql(reader.ReadToEnd());
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Issued invoices and issue receipts are immutable history; destructive contraction requires a supported plan.");
}
