using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.PlatformAdministration.Postgres.Migrations;

[DbContext(typeof(PlatformAdministrationDbContext))]
[Migration("202610190001_AdminAuditReads")]
public sealed class AdminAuditReads : Migration
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => PlatformAdministrationModel.Build(modelBuilder);

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE INDEX ix_platform_admin_access_audit_position
                ON platform_administration.access_audit_events (occurred_at DESC, id DESC);
            CREATE INDEX ix_platform_admin_access_audit_principal_position
                ON platform_administration.access_audit_events (principal_id, occurred_at DESC, id DESC);
            CREATE FUNCTION platform_administration.read_access_audit(
                p_before_at timestamptz, p_before_id uuid, p_principal uuid, p_outcome smallint, p_limit integer)
            RETURNS TABLE (id uuid, principal_id uuid, device_id uuid, operation varchar,
                           outcome smallint, reason varchar, occurred_at timestamptz)
            LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog
            AS $read$
                SELECT e.id, e.principal_id, e.device_id, e.operation, e.outcome, e.reason, e.occurred_at
                FROM platform_administration.access_audit_events e
                WHERE p_limit BETWEEN 1 AND 101
                  AND ((p_before_at IS NULL AND p_before_id IS NULL)
                       OR (p_before_at IS NOT NULL AND p_before_id IS NOT NULL
                           AND (e.occurred_at, e.id) < (p_before_at, p_before_id)))
                  AND (p_principal IS NULL OR e.principal_id = p_principal)
                  AND (p_outcome IS NULL OR e.outcome = p_outcome)
                ORDER BY e.occurred_at DESC, e.id DESC
                LIMIT greatest(0, least(coalesce(p_limit, 0), 101))
            $read$;
            REVOKE ALL ON FUNCTION platform_administration.read_access_audit(timestamptz, uuid, uuid, smallint, integer) FROM PUBLIC;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP FUNCTION platform_administration.read_access_audit(timestamptz, uuid, uuid, smallint, integer);
            DROP INDEX platform_administration.ix_platform_admin_access_audit_position;
            DROP INDEX platform_administration.ix_platform_admin_access_audit_principal_position;
            """);
    }
}
