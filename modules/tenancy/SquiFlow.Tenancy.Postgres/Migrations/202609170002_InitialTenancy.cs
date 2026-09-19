using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquiFlow.Tenancy.Postgres.Migrations;

public partial class InitialTenancy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "tenancy");

        migrationBuilder.CreateTable(
            name: "tenants",
            schema: "tenancy",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                availability = table.Column<short>(type: "smallint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                suspended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tenants", tenant => tenant.id);
                table.CheckConstraint("ck_tenants_availability", "availability IN (1, 2)");
                table.CheckConstraint("ck_tenants_display_name_not_blank", "btrim(display_name) <> ''");
            });

        migrationBuilder.CreateTable(
            name: "memberships",
            schema: "tenancy",
            columns: table => new
            {
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                availability = table.Column<short>(type: "smallint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                suspended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_memberships", membership => new { membership.tenant_id, membership.account_id });
                table.CheckConstraint("ck_memberships_availability", "availability IN (1, 2)");
                table.ForeignKey(
                    name: "fk_memberships_accounts_account_id",
                    column: membership => membership.account_id,
                    principalSchema: "identity_access",
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_memberships_tenants_tenant_id",
                    column: membership => membership.tenant_id,
                    principalSchema: "tenancy",
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_memberships_account_id_availability",
            schema: "tenancy",
            table: "memberships",
            columns: new[] { "account_id", "availability" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "memberships", schema: "tenancy");
        migrationBuilder.DropTable(name: "tenants", schema: "tenancy");
    }
}
