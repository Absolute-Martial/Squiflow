using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquiFlow.IdentityAccess.Postgres.Migrations;

public partial class InitialAccountBindings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "identity_access");

        migrationBuilder.CreateTable(
            name: "accounts",
            schema: "identity_access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                availability = table.Column<short>(type: "smallint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                disabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_accounts", account => account.id);
                table.CheckConstraint("ck_accounts_availability", "availability IN (1, 2)");
            });

        migrationBuilder.CreateTable(
            name: "external_identity_bindings",
            schema: "identity_access",
            columns: table => new
            {
                issuer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_external_identity_bindings", binding => new { binding.issuer, binding.subject });
                table.CheckConstraint(
                    "ck_external_identity_bindings_issuer_not_blank",
                    "btrim(issuer) <> ''");
                table.CheckConstraint(
                    "ck_external_identity_bindings_subject_not_blank",
                    "btrim(subject) <> ''");
                table.ForeignKey(
                    name: "fk_external_identity_bindings_accounts_account_id",
                    column: binding => binding.account_id,
                    principalSchema: "identity_access",
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_external_identity_bindings_account_id",
            schema: "identity_access",
            table: "external_identity_bindings",
            column: "account_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "external_identity_bindings",
            schema: "identity_access");

        migrationBuilder.DropTable(
            name: "accounts",
            schema: "identity_access");
    }
}
