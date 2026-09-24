-- Apply after the IdentityAccess, Tenancy and Orders migrations. This file is
-- intentionally usable inside one transaction by both psql and provider tests.
DO $provision$
DECLARE
    runtime_role text := nullif(current_setting('app.provision_runtime_role', true), '');
    role_record pg_roles%ROWTYPE;
    target_schema text;
    target_table text;
BEGIN
    IF runtime_role IS NULL THEN
        RAISE EXCEPTION 'CoreApi runtime role was not supplied';
    END IF;

    SELECT * INTO role_record FROM pg_roles WHERE rolname = runtime_role;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'CoreApi runtime role does not exist';
    END IF;
    IF NOT role_record.rolcanlogin OR role_record.rolsuper OR role_record.rolbypassrls
       OR role_record.rolcreaterole OR role_record.rolcreatedb OR role_record.rolreplication THEN
        RAISE EXCEPTION 'CoreApi runtime role has unsafe role attributes';
    END IF;
    IF EXISTS (
        SELECT 1 FROM pg_roles AS parent
        WHERE parent.oid <> role_record.oid
          AND pg_has_role(role_record.oid, parent.oid, 'MEMBER')) THEN
        RAISE EXCEPTION 'CoreApi runtime role must not inherit or assume another role';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_database WHERE datname = current_database() AND datdba = role_record.oid)
       OR EXISTS (
           SELECT 1 FROM pg_namespace
           WHERE nspname IN ('identity_access', 'tenancy', 'orders') AND nspowner = role_record.oid)
       OR EXISTS (
           SELECT 1 FROM pg_class AS c
           JOIN pg_namespace AS n ON n.oid = c.relnamespace
           WHERE n.nspname IN ('identity_access', 'tenancy', 'orders')
             AND c.relname IN ('accounts', 'external_identity_bindings', 'tenants', 'memberships',
                               'order_drafts', 'order_draft_lines', 'command_receipts')
             AND c.relowner = role_record.oid) THEN
        RAISE EXCEPTION 'CoreApi runtime role must not own the database, schemas or application tables';
    END IF;
    IF EXISTS (
        SELECT 1 FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        WHERE n.nspname = 'orders'
          AND c.relname IN ('order_drafts', 'order_draft_lines', 'command_receipts')
          AND (NOT c.relrowsecurity OR NOT c.relforcerowsecurity)) THEN
        RAISE EXCEPTION 'Orders tenant tables must have forced row level security';
    END IF;

    EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), runtime_role);
    FOREACH target_schema IN ARRAY ARRAY['identity_access', 'tenancy', 'orders'] LOOP
        EXECUTE format('GRANT USAGE ON SCHEMA %I TO %I', target_schema, runtime_role);
    END LOOP;
    FOREACH target_table IN ARRAY ARRAY[
        'identity_access.accounts', 'identity_access.external_identity_bindings',
        'tenancy.tenants', 'tenancy.memberships'] LOOP
        EXECUTE format('GRANT SELECT ON TABLE %s TO %I', target_table, runtime_role);
    END LOOP;
    FOREACH target_table IN ARRAY ARRAY[
        'orders.order_drafts', 'orders.order_draft_lines', 'orders.command_receipts'] LOOP
        EXECUTE format('GRANT SELECT, INSERT ON TABLE %s TO %I', target_table, runtime_role);
    END LOOP;
    EXECUTE format(
        'GRANT UPDATE (state, revision, abandoned_at, abandoned_by_account_id) ON TABLE orders.order_drafts TO %I',
        runtime_role);
END
$provision$;

DO $verify$
DECLARE
    runtime_role text := current_setting('app.provision_runtime_role');
    target_schema text;
    target_table text;
    target_column record;
BEGIN
    IF NOT has_database_privilege(runtime_role, current_database(), 'CONNECT')
       OR has_database_privilege(runtime_role, current_database(), 'CREATE') THEN
        RAISE EXCEPTION 'CoreApi runtime database privileges are unsafe';
    END IF;
    FOREACH target_schema IN ARRAY ARRAY['identity_access', 'tenancy', 'orders'] LOOP
        IF NOT has_schema_privilege(runtime_role, target_schema, 'USAGE')
           OR has_schema_privilege(runtime_role, target_schema, 'CREATE') THEN
            RAISE EXCEPTION 'CoreApi runtime schema privileges are unsafe';
        END IF;
    END LOOP;

    FOR target_column IN
        SELECT n.nspname AS schema_name, c.relname AS table_name, a.attname AS column_name
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        JOIN pg_attribute AS a ON a.attrelid = c.oid
        WHERE ((n.nspname = 'identity_access' AND c.relname IN ('accounts', 'external_identity_bindings'))
            OR (n.nspname = 'tenancy' AND c.relname IN ('tenants', 'memberships'))
            OR (n.nspname = 'orders' AND c.relname IN ('order_drafts', 'order_draft_lines', 'command_receipts')))
          AND a.attnum > 0 AND NOT a.attisdropped
    LOOP
        target_table := format('%I.%I', target_column.schema_name, target_column.table_name);
        IF NOT has_column_privilege(runtime_role, target_table, target_column.column_name, 'SELECT')
           OR (target_column.schema_name = 'orders'
               AND NOT has_column_privilege(runtime_role, target_table, target_column.column_name, 'INSERT'))
           OR (target_column.schema_name <> 'orders'
               AND (has_column_privilege(runtime_role, target_table, target_column.column_name, 'INSERT')
                    OR has_column_privilege(runtime_role, target_table, target_column.column_name, 'UPDATE')))
           OR (target_column.schema_name = 'orders'
               AND has_column_privilege(runtime_role, target_table, target_column.column_name, 'UPDATE')
                   <> (target_column.table_name = 'order_drafts'
                       AND target_column.column_name IN (
                           'state', 'revision', 'abandoned_at', 'abandoned_by_account_id'))) THEN
            RAISE EXCEPTION 'CoreApi runtime column privileges are unsafe on %', target_table;
        END IF;
    END LOOP;

    FOREACH target_table IN ARRAY ARRAY[
        'identity_access.accounts', 'identity_access.external_identity_bindings',
        'tenancy.tenants', 'tenancy.memberships',
        'orders.order_drafts', 'orders.order_draft_lines', 'orders.command_receipts'] LOOP
        IF has_table_privilege(runtime_role, target_table, 'DELETE')
           OR has_table_privilege(runtime_role, target_table, 'TRUNCATE')
           OR has_table_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_table_privilege(runtime_role, target_table, 'TRIGGER') THEN
            RAISE EXCEPTION 'CoreApi runtime table privileges are unsafe on %', target_table;
        END IF;
    END LOOP;
END
$verify$;
