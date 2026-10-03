-- Apply after the PlatformAdministration migration. The one-shot bootstrap role may
-- prepare/finalize bootstrap state but cannot migrate schema, delete records or mutate
-- administrator/device identity after preparation.
DO $provision$
DECLARE
    runtime_role text := nullif(current_setting('app.provision_admin_bootstrap_role', true), '');
    role_record pg_roles%ROWTYPE;
    target_table text;
BEGIN
    IF runtime_role IS NULL THEN
        RAISE EXCEPTION 'Admin bootstrap runtime role was not supplied';
    END IF;

    SELECT * INTO role_record FROM pg_roles WHERE rolname = runtime_role;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Admin bootstrap runtime role does not exist';
    END IF;
    IF NOT role_record.rolcanlogin OR role_record.rolsuper OR role_record.rolbypassrls
       OR role_record.rolcreaterole OR role_record.rolcreatedb OR role_record.rolreplication THEN
        RAISE EXCEPTION 'Admin bootstrap runtime role has unsafe role attributes';
    END IF;
    IF EXISTS (
        SELECT 1 FROM pg_roles AS parent
        WHERE parent.oid <> role_record.oid
          AND pg_has_role(role_record.oid, parent.oid, 'MEMBER')) THEN
        RAISE EXCEPTION 'Admin bootstrap runtime role must not inherit or assume another role';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_database WHERE datname = current_database() AND datdba = role_record.oid)
       OR EXISTS (
           SELECT 1 FROM pg_namespace
           WHERE nspname = 'platform_administration' AND nspowner = role_record.oid)
       OR EXISTS (
           SELECT 1 FROM pg_class AS c
           JOIN pg_namespace AS n ON n.oid = c.relnamespace
           WHERE n.nspname = 'platform_administration' AND c.relowner = role_record.oid) THEN
        RAISE EXCEPTION 'Admin bootstrap runtime role must not own database/schema/tables';
    END IF;

    EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), runtime_role);
    EXECUTE format('GRANT USAGE ON SCHEMA platform_administration TO %I', runtime_role);
    FOREACH target_table IN ARRAY ARRAY[
        'platform_administration.principals',
        'platform_administration.admin_devices',
        'platform_administration.bootstrap_state',
        'platform_administration.audit_events'] LOOP
        EXECUTE format('GRANT SELECT, INSERT ON TABLE %s TO %I', target_table, runtime_role);
    END LOOP;
    EXECUTE format(
        'GRANT UPDATE (status, completed_at) ON TABLE platform_administration.bootstrap_state TO %I',
        runtime_role);
END
$provision$;

DO $verify$
DECLARE
    runtime_role text := current_setting('app.provision_admin_bootstrap_role');
    target_table text;
BEGIN
    IF NOT has_database_privilege(runtime_role, current_database(), 'CONNECT')
       OR has_database_privilege(runtime_role, current_database(), 'CREATE') THEN
        RAISE EXCEPTION 'Admin bootstrap database privileges are unsafe';
    END IF;
    IF NOT has_schema_privilege(runtime_role, 'platform_administration', 'USAGE')
       OR has_schema_privilege(runtime_role, 'platform_administration', 'CREATE') THEN
        RAISE EXCEPTION 'Admin bootstrap schema privileges are unsafe';
    END IF;
    FOREACH target_table IN ARRAY ARRAY[
        'platform_administration.principals',
        'platform_administration.admin_devices',
        'platform_administration.bootstrap_state',
        'platform_administration.audit_events'] LOOP
        IF NOT has_table_privilege(runtime_role, target_table, 'SELECT')
           OR NOT has_table_privilege(runtime_role, target_table, 'INSERT')
           OR has_table_privilege(runtime_role, target_table, 'DELETE')
           OR has_table_privilege(runtime_role, target_table, 'TRUNCATE')
           OR has_table_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_table_privilege(runtime_role, target_table, 'TRIGGER') THEN
            RAISE EXCEPTION 'Admin bootstrap table privileges are unsafe on %', target_table;
        END IF;
    END LOOP;
    IF NOT has_column_privilege(
            runtime_role, 'platform_administration.bootstrap_state', 'status', 'UPDATE')
       OR NOT has_column_privilege(
            runtime_role, 'platform_administration.bootstrap_state', 'completed_at', 'UPDATE')
       OR has_column_privilege(
            runtime_role, 'platform_administration.bootstrap_state', 'principal_id', 'UPDATE')
       OR has_column_privilege(
            runtime_role, 'platform_administration.principals', 'availability', 'UPDATE')
       OR has_column_privilege(
            runtime_role, 'platform_administration.admin_devices', 'availability', 'UPDATE') THEN
        RAISE EXCEPTION 'Admin bootstrap update privileges are unsafe';
    END IF;
END
$verify$;
