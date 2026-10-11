using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Purchasing.Postgres.Migrations;

[DbContext(typeof(PurchasingDbContext))]
[Migration("20261010100000_FirstSupplierServicePurchasing")]
public sealed class FirstSupplierServicePurchasing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        using var resource = typeof(FirstSupplierServicePurchasing).Assembly.GetManifestResourceStream("Application.Purchasing.Postgres.Sql.FirstSupplierServicePurchasing.sql")
            ?? throw new InvalidOperationException("The purchasing migration resource is missing.");
        using var reader = new StreamReader(resource);
        migrationBuilder.Sql(reader.ReadToEnd());
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Supplier, payable and correction evidence is retained; destructive contraction requires a supported plan.");
}
