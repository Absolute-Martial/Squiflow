using Application.Customers.Postgres.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Application.Customers.Postgres.Tests;

public sealed partial class CustomerPostgresTests
{
    [Fact]
    public async Task DuplicateSignalsBackfillVisitsEveryTenantUnderARestrictedForcedRlsMigrator()
    {
        await MigrateAsync();
        await using (var context = CustomersPostgresMigrations.CreateContext(ConnectionString))
            await context.Database.MigrateAsync("20261006060007_CustomerContactsAndRepresentatives");
        var a = await SeedAsync(); var b = await SeedAsync();
        foreach (var (tenant, account) in new[] { a, b })
            await FixtureSqlAsync($"""
                INSERT INTO customers.individuals(tenant_id,id,display_name,email,phone,availability,revision,created_by_account_id,created_at)
                VALUES ('{tenant:D}','{Guid.NewGuid():D}','  Legacy Name  ',' Legacy@Example.test ','+1 (555) 0199',1,1,'{account:D}',clock_timestamp());
                """);
        await ApplyAsRestrictedMigratorAsync(new CustomerDuplicatesAndImports());
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var verify = connection.CreateCommand();
        verify.CommandText = """
            SELECT count(*) FROM customers.individuals WHERE normalized_name='LEGACY NAME'
              AND normalized_email='LEGACY@EXAMPLE.TEST' AND normalized_phone='15550199' AND customer_type='individual'
            """;
        Assert.Equal(2L, await verify.ExecuteScalarAsync());
        verify.CommandText = "SELECT relrowsecurity AND relforcerowsecurity FROM pg_class WHERE oid='customers.individuals'::regclass";
        Assert.Equal(true, await verify.ExecuteScalarAsync());
    }

    [Fact]
    public async Task RecoveryMigrationRecoversExactReceiptsAndLeavesAmbiguousArchiveStagingUnowned()
    {
        await MigrateAsync();
        await using (var context = CustomersPostgresMigrations.CreateContext(ConnectionString))
            await context.Database.MigrateAsync("20261007180000_CustomerImportRawSourceLifecycle");
        var a = await SeedAsync(); var b = await SeedAsync();
        var acceptedId = Guid.NewGuid(); var orphanId = Guid.NewGuid();
        var hash = new string('a', 64); var fingerprint = new string('b', 64);
        var acceptedKey = $"imports/{a.TenantId:D}/{acceptedId:N}/{hash}.csv";
        var orphanKey = $"imports/{b.TenantId:D}/{orphanId:N}/{hash}.csv";
        foreach (var (tenant, key, state) in new[] { (a.TenantId, acceptedKey, 3), (b.TenantId, orphanKey, 5) })
            await FixtureSqlAsync($"""
                INSERT INTO customers.import_source_objects(tenant_id,object_key,provider_scope,byte_length,sha256,content_type,state,retention,created_at)
                  VALUES ('{tenant:D}','{key}','test/legacy',42,'{hash}','text/csv', {state},2,clock_timestamp()-interval '8 days');
                INSERT INTO customers.object_storage_usage(tenant_id,provider_scope,maximum_bytes,reserved_bytes,retained_bytes,updated_at)
                  VALUES ('{tenant:D}','test/legacy',1000,{(state == 3 ? 0 : 42)},{(state == 3 ? 42 : 0)},clock_timestamp());
                INSERT INTO customers.object_storage_reservations(tenant_id,provider_scope,reservation_id,idempotency_key,fingerprint,object_key,byte_length,state,created_at,updated_at)
                  VALUES ('{tenant:D}','test/legacy','{Guid.NewGuid():D}','legacy-key','{fingerprint}','{key}',42,{(state == 3 ? 2 : 4)},clock_timestamp(),clock_timestamp());
                """);
        await FixtureSqlAsync($"""
            INSERT INTO customers.imports(tenant_id,import_id,account_id,idempotency_key,fingerprint,created_by_account_id,
              contract_version,manifest_hash,byte_length,row_count,created_at)
              VALUES ('{a.TenantId:D}','{acceptedId:D}','{a.AccountId:D}','legacy-key','{fingerprint}','{a.AccountId:D}',
                'customer-import/v1','{hash}',42,0,clock_timestamp());
            """);
        await ApplyAsRestrictedMigratorAsync(new CustomerImportUploadRecovery());
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var verify = connection.CreateCommand();
        verify.CommandText = "SELECT source_object_key FROM customers.imports WHERE tenant_id=@tenant";
        verify.Parameters.AddWithValue("tenant", a.TenantId);
        Assert.Equal(acceptedKey, await verify.ExecuteScalarAsync());
        verify.CommandText = "SELECT account_id FROM customers.object_storage_reservations WHERE tenant_id=@tenant";
        Assert.Equal(a.AccountId, await verify.ExecuteScalarAsync());
        verify.CommandText = "SELECT staging_expires_at IS NULL FROM customers.import_source_objects WHERE tenant_id=@tenant";
        Assert.Equal(true, await verify.ExecuteScalarAsync());
        verify.Parameters["tenant"].Value = b.TenantId;
        verify.CommandText = "SELECT account_id IS NULL AND import_id IS NULL FROM customers.object_storage_reservations WHERE tenant_id=@tenant";
        Assert.Equal(true, await verify.ExecuteScalarAsync());
        verify.CommandText = "SELECT staging_expires_at <= clock_timestamp() AND retention=2 FROM customers.import_source_objects WHERE tenant_id=@tenant";
        Assert.Equal(true, await verify.ExecuteScalarAsync());
        verify.CommandText = "SET ROLE application_recovery_migrator; SELECT tenant_id FROM customers.discover_runnable_import_tenants(NULL,50)";
        Assert.Equal(b.TenantId, await verify.ExecuteScalarAsync());
        verify.CommandText = "SELECT count(*) FROM customers.object_storage_reservations";
        Assert.Equal(0L, await verify.ExecuteScalarAsync());
    }

    private async Task ApplyAsRestrictedMigratorAsync(Migration migration)
    {
        await FixtureSqlAsync("""
            CREATE ROLE application_recovery_migrator NOLOGIN NOSUPERUSER NOBYPASSRLS;
            GRANT USAGE,CREATE ON SCHEMA customers TO application_recovery_migrator;
            GRANT USAGE ON SCHEMA identity_access,tenancy TO application_recovery_migrator;
            GRANT SELECT,REFERENCES ON ALL TABLES IN SCHEMA identity_access,tenancy TO application_recovery_migrator;
            DO $ownership$ DECLARE item record; BEGIN
              FOR item IN SELECT tablename FROM pg_tables WHERE schemaname='customers' LOOP
                EXECUTE format('ALTER TABLE customers.%I OWNER TO application_recovery_migrator',item.tablename);
              END LOOP;
              FOR item IN SELECT oid::regprocedure AS name FROM pg_proc WHERE pronamespace='customers'::regnamespace LOOP
                EXECUTE format('ALTER FUNCTION %s OWNER TO application_recovery_migrator',item.name);
              END LOOP;
            END $ownership$;
            """);
        await using var context = CustomersPostgresMigrations.CreateContext(ConnectionString);
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, context.Model);
        await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SET LOCAL ROLE application_recovery_migrator"; await command.ExecuteNonQueryAsync();
        foreach (var operation in commands)
        {
            command.CommandText = operation.CommandText;
            await command.ExecuteNonQueryAsync();
        }
        command.CommandText = "SELECT NOT rolbypassrls AND NOT rolsuper FROM pg_roles WHERE rolname=current_user";
        Assert.Equal(true, await command.ExecuteScalarAsync());
        command.CommandText = "SELECT COALESCE(current_setting('app.current_tenant',true),'')=''";
        Assert.Equal(true, await command.ExecuteScalarAsync());
        await transaction.CommitAsync();
    }
}
