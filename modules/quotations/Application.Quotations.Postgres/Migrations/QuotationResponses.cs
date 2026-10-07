using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Application.Quotations.Postgres.Migrations;

[DbContext(typeof(QuotationDbContext))]
[Migration("202610070040_QuotationResponses")]
public sealed class QuotationResponses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(QuotationSql.Load("ResponsesSchema"));
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Quotation responses and conversion links must be retained.");
}
