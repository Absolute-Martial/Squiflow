using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Quotations.Postgres.Migrations;

[DbContext(typeof(QuotationDbContext))]
[Migration("202610070020_QuotationFacts")]
public sealed class QuotationFacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(QuotationSql.Load("Schema"));
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Quotation history is retained; destructive rollback requires an explicit supported contraction plan.");
}
