-- Apply after the IdentityAccess, PlatformAdministration, Tenancy, Profiles and Orders migrations.
-- AdminApi reads active principal/device registration, appends access audit, and owns
-- tenant/account onboarding plus membership and tenant-lifecycle write paths. It
-- cannot mutate bootstrap authority, identity account state or retained evidence.
DO $provision$
DECLARE
    runtime_role text := nullif(current_setting('app.provision_admin_api_role', true), '');
    role_record pg_roles%ROWTYPE;
    target_table text;
BEGIN
    IF runtime_role IS NULL THEN
        RAISE EXCEPTION 'Admin API runtime role was not supplied';
    END IF;

    SELECT * INTO role_record FROM pg_roles WHERE rolname = runtime_role;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Admin API runtime role does not exist';
    END IF;
    IF NOT role_record.rolcanlogin OR role_record.rolsuper OR role_record.rolbypassrls
       OR role_record.rolcreaterole OR role_record.rolcreatedb OR role_record.rolreplication THEN
        RAISE EXCEPTION 'Admin API runtime role has unsafe role attributes';
    END IF;
    IF EXISTS (
        SELECT 1 FROM pg_roles AS parent
        WHERE parent.oid <> role_record.oid
          AND pg_has_role(role_record.oid, parent.oid, 'MEMBER')) THEN
        RAISE EXCEPTION 'Admin API runtime role must not inherit or assume another role';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_database WHERE datname = current_database() AND datdba = role_record.oid)
       OR EXISTS (
           SELECT 1 FROM pg_namespace
           WHERE nspname = 'platform_administration' AND nspowner = role_record.oid)
       OR EXISTS (
           SELECT 1 FROM pg_class AS c
           JOIN pg_namespace AS n ON n.oid = c.relnamespace
           WHERE n.nspname IN ('identity_access', 'platform_administration', 'tenancy', 'profiles', 'orders')
             AND c.relowner = role_record.oid)
       OR EXISTS (
           SELECT 1 FROM pg_namespace
           WHERE nspname IN ('identity_access', 'tenancy', 'profiles', 'orders') AND nspowner = role_record.oid) THEN
        RAISE EXCEPTION 'Admin API runtime role must not own database/schema/tables';
    END IF;
    IF EXISTS (
        SELECT 1 FROM pg_namespace
        WHERE nspname IN ('profiles', 'orders') AND nspowner = role_record.oid) THEN
        RAISE EXCEPTION 'Admin API runtime role must not own profile or Order schemas';
    END IF;
    IF EXISTS (
        SELECT 1 FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        WHERE (n.nspname = 'profiles' AND c.relname IN
                ('policy_heads', 'policy_revisions', 'publications', 'authority', 'command_receipts')
            OR n.nspname = 'orders' AND c.relname IN ('program_order_metadata', 'order_drafts'))
          AND (NOT c.relrowsecurity OR NOT c.relforcerowsecurity)) THEN
        RAISE EXCEPTION 'AdminApi profile and Order tables must have forced row level security';
    END IF;

    EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), runtime_role);
    EXECUTE format('GRANT USAGE ON SCHEMA identity_access TO %I', runtime_role);
    EXECUTE format('GRANT USAGE ON SCHEMA platform_administration TO %I', runtime_role);
    EXECUTE format('GRANT USAGE ON SCHEMA tenancy TO %I', runtime_role);
    EXECUTE format('GRANT USAGE ON SCHEMA profiles TO %I', runtime_role);
    EXECUTE format('GRANT USAGE ON SCHEMA orders TO %I', runtime_role);
    FOREACH target_table IN ARRAY ARRAY[
        'identity_access.accounts',
        'identity_access.external_identity_bindings',
        'identity_access.account_onboarding_receipts',
        'identity_access.identity_link_receipts'] LOOP
        EXECUTE format('GRANT SELECT, INSERT ON TABLE %s TO %I', target_table, runtime_role);
    END LOOP;
    EXECUTE format(
        'GRANT EXECUTE ON FUNCTION identity_access.lock_account_for_identity_link(uuid) TO %I',
        runtime_role);
    FOREACH target_table IN ARRAY ARRAY[
        'platform_administration.principals',
        'platform_administration.admin_devices'] LOOP
        EXECUTE format('GRANT SELECT ON TABLE %s TO %I', target_table, runtime_role);
    END LOOP;
    EXECUTE format(
        'GRANT INSERT ON TABLE platform_administration.access_audit_events TO %I',
        runtime_role);
    EXECUTE format('GRANT SELECT, INSERT ON TABLE tenancy.tenants TO %I', runtime_role);
    EXECUTE format(
        'GRANT UPDATE (availability, revision, suspended_at) ON tenancy.tenants TO %I',
        runtime_role);
    EXECUTE format(
        'GRANT SELECT, INSERT ON TABLE tenancy.tenant_provisioning_receipts TO %I',
        runtime_role);
    EXECUTE format('GRANT SELECT, INSERT ON TABLE tenancy.memberships TO %I', runtime_role);
    EXECUTE format(
        'GRANT UPDATE (availability, revision, activated_at, suspended_at, removed_at) ON tenancy.memberships TO %I',
        runtime_role);
    EXECUTE format(
        'GRANT SELECT, INSERT ON TABLE tenancy.membership_lifecycle_receipts TO %I',
        runtime_role);
    EXECUTE format(
        'GRANT SELECT, INSERT ON TABLE tenancy.tenant_lifecycle_receipts TO %I',
        runtime_role);
    FOREACH target_table IN ARRAY ARRAY[
        'profiles.policy_heads', 'profiles.policy_revisions', 'profiles.publications',
        'profiles.authority', 'profiles.command_receipts'] LOOP
        EXECUTE format('GRANT SELECT ON TABLE %s TO %I', target_table, runtime_role);
    END LOOP;
    EXECUTE format('GRANT INSERT ON TABLE profiles.publications TO %I', runtime_role);
    EXECUTE format('GRANT INSERT ON TABLE profiles.authority TO %I', runtime_role);
    EXECUTE format('GRANT INSERT ON TABLE profiles.command_receipts TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (revision, active_profile_id, legacy_baseline_profile_id) ON TABLE profiles.authority TO %I', runtime_role);
    EXECUTE format('GRANT SELECT, INSERT ON TABLE orders.program_order_metadata TO %I', runtime_role);
    -- Read only create-receipt compatibility for explicit legacy assignment; no receipt writes.
    EXECUTE format('GRANT SELECT (tenant_id, order_id, operation, response_json) ON TABLE orders.command_receipts TO %I', runtime_role);
    EXECUTE format('GRANT SELECT (tenant_id, id, state, revision) ON TABLE orders.order_drafts TO %I', runtime_role);
    EXECUTE format('GRANT SELECT (tenant_id, revision) ON TABLE tenancy.tenant_authorization_state TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (revision) ON TABLE orders.order_drafts TO %I', runtime_role);
END
$provision$;

DO $verify$
DECLARE
    runtime_role text := current_setting('app.provision_admin_api_role');
    target_table text;
BEGIN
    IF NOT has_database_privilege(runtime_role, current_database(), 'CONNECT')
       OR has_database_privilege(runtime_role, current_database(), 'CREATE') THEN
        RAISE EXCEPTION 'Admin API database privileges are unsafe';
    END IF;
    IF NOT has_schema_privilege(runtime_role, 'identity_access', 'USAGE')
       OR has_schema_privilege(runtime_role, 'identity_access', 'CREATE') THEN
        RAISE EXCEPTION 'Admin API identity-access schema privileges are unsafe';
    END IF;
    IF NOT has_schema_privilege(runtime_role, 'platform_administration', 'USAGE')
       OR has_schema_privilege(runtime_role, 'platform_administration', 'CREATE') THEN
        RAISE EXCEPTION 'Admin API schema privileges are unsafe';
    END IF;
    IF NOT has_schema_privilege(runtime_role, 'tenancy', 'USAGE')
       OR has_schema_privilege(runtime_role, 'tenancy', 'CREATE') THEN
        RAISE EXCEPTION 'Admin API tenancy schema privileges are unsafe';
    END IF;
    IF NOT has_schema_privilege(runtime_role, 'profiles', 'USAGE')
       OR has_schema_privilege(runtime_role, 'profiles', 'CREATE')
       OR NOT has_schema_privilege(runtime_role, 'orders', 'USAGE')
       OR has_schema_privilege(runtime_role, 'orders', 'CREATE') THEN
        RAISE EXCEPTION 'Admin API profile/Order schema privileges are unsafe';
    END IF;
    FOREACH target_table IN ARRAY ARRAY[
        'identity_access.accounts',
        'identity_access.external_identity_bindings',
        'identity_access.account_onboarding_receipts',
        'identity_access.identity_link_receipts'] LOOP
        IF NOT has_table_privilege(runtime_role, target_table, 'SELECT')
           OR NOT has_table_privilege(runtime_role, target_table, 'INSERT')
           OR has_table_privilege(runtime_role, target_table, 'UPDATE')
           OR has_any_column_privilege(runtime_role, target_table, 'UPDATE')
           OR has_table_privilege(runtime_role, target_table, 'DELETE')
           OR has_table_privilege(runtime_role, target_table, 'TRUNCATE')
           OR has_table_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_any_column_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_any_column_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_table_privilege(runtime_role, target_table, 'TRIGGER') THEN
            RAISE EXCEPTION 'Admin API identity-onboarding table privileges are unsafe on %', target_table;
        END IF;
    END LOOP;
    IF NOT has_function_privilege(
            runtime_role,
            'identity_access.lock_account_for_identity_link(uuid)',
            'EXECUTE') THEN
        RAISE EXCEPTION 'Admin API identity-link lock function privilege is missing';
    END IF;
    FOREACH target_table IN ARRAY ARRAY[
        'platform_administration.principals',
        'platform_administration.admin_devices'] LOOP
        IF NOT has_table_privilege(runtime_role, target_table, 'SELECT')
           OR has_table_privilege(runtime_role, target_table, 'INSERT')
           OR has_table_privilege(runtime_role, target_table, 'UPDATE')
           OR has_table_privilege(runtime_role, target_table, 'DELETE')
           OR has_table_privilege(runtime_role, target_table, 'TRUNCATE')
           OR has_table_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_any_column_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_table_privilege(runtime_role, target_table, 'TRIGGER') THEN
            RAISE EXCEPTION 'Admin API table privileges are unsafe on %', target_table;
        END IF;
    END LOOP;
    IF NOT has_table_privilege(
            runtime_role,
            'platform_administration.access_audit_events',
            'INSERT')
       OR has_table_privilege(runtime_role, 'platform_administration.access_audit_events', 'SELECT')
       OR has_table_privilege(runtime_role, 'platform_administration.access_audit_events', 'UPDATE')
       OR has_table_privilege(runtime_role, 'platform_administration.access_audit_events', 'DELETE')
       OR has_table_privilege(runtime_role, 'platform_administration.access_audit_events', 'TRUNCATE')
       OR has_table_privilege(runtime_role, 'platform_administration.access_audit_events', 'REFERENCES')
       OR has_table_privilege(runtime_role, 'platform_administration.access_audit_events', 'TRIGGER') THEN
        RAISE EXCEPTION 'Admin API access-audit privileges are unsafe';
    END IF;
    IF has_table_privilege(runtime_role, 'platform_administration.bootstrap_state', 'SELECT')
       OR has_table_privilege(runtime_role, 'platform_administration.audit_events', 'SELECT') THEN
        RAISE EXCEPTION 'Initial Admin API role has unnecessary bootstrap/audit table access';
    END IF;
    IF NOT has_table_privilege(runtime_role, 'tenancy.tenants', 'SELECT')
       OR NOT has_table_privilege(runtime_role, 'tenancy.tenants', 'INSERT')
       OR has_table_privilege(runtime_role, 'tenancy.tenants', 'UPDATE')
       OR NOT has_column_privilege(runtime_role, 'tenancy.tenants', 'availability', 'UPDATE')
       OR NOT has_column_privilege(runtime_role, 'tenancy.tenants', 'revision', 'UPDATE')
       OR NOT has_column_privilege(runtime_role, 'tenancy.tenants', 'suspended_at', 'UPDATE')
       OR has_column_privilege(runtime_role, 'tenancy.tenants', 'display_name', 'UPDATE')
       OR has_column_privilege(runtime_role, 'tenancy.tenants', 'created_at', 'UPDATE')
       OR has_table_privilege(runtime_role, 'tenancy.tenants', 'DELETE')
       OR has_table_privilege(runtime_role, 'tenancy.tenants', 'TRUNCATE')
       OR has_table_privilege(runtime_role, 'tenancy.tenants', 'REFERENCES')
       OR has_any_column_privilege(runtime_role, 'tenancy.tenants', 'REFERENCES')
       OR has_table_privilege(runtime_role, 'tenancy.tenants', 'TRIGGER') THEN
        RAISE EXCEPTION 'Admin API tenant-table privileges are unsafe';
    END IF;
    IF NOT has_table_privilege(runtime_role, 'tenancy.tenant_provisioning_receipts', 'SELECT')
       OR NOT has_table_privilege(runtime_role, 'tenancy.tenant_provisioning_receipts', 'INSERT')
       OR has_table_privilege(runtime_role, 'tenancy.tenant_provisioning_receipts', 'UPDATE')
       OR has_any_column_privilege(runtime_role, 'tenancy.tenant_provisioning_receipts', 'UPDATE')
       OR has_table_privilege(runtime_role, 'tenancy.tenant_provisioning_receipts', 'DELETE')
       OR has_table_privilege(runtime_role, 'tenancy.tenant_provisioning_receipts', 'TRUNCATE')
       OR has_table_privilege(runtime_role, 'tenancy.tenant_provisioning_receipts', 'REFERENCES')
       OR has_any_column_privilege(runtime_role, 'tenancy.tenant_provisioning_receipts', 'REFERENCES')
       OR has_table_privilege(runtime_role, 'tenancy.tenant_provisioning_receipts', 'TRIGGER') THEN
        RAISE EXCEPTION 'Admin API tenant-provisioning receipt privileges are unsafe';
    END IF;
    IF NOT has_table_privilege(runtime_role, 'tenancy.memberships', 'SELECT')
       OR NOT has_table_privilege(runtime_role, 'tenancy.memberships', 'INSERT')
       OR has_table_privilege(runtime_role, 'tenancy.memberships', 'UPDATE')
       OR NOT has_column_privilege(runtime_role, 'tenancy.memberships', 'availability', 'UPDATE')
       OR NOT has_column_privilege(runtime_role, 'tenancy.memberships', 'revision', 'UPDATE')
       OR NOT has_column_privilege(runtime_role, 'tenancy.memberships', 'activated_at', 'UPDATE')
       OR NOT has_column_privilege(runtime_role, 'tenancy.memberships', 'suspended_at', 'UPDATE')
       OR NOT has_column_privilege(runtime_role, 'tenancy.memberships', 'removed_at', 'UPDATE')
       OR has_column_privilege(runtime_role, 'tenancy.memberships', 'tenant_id', 'UPDATE')
       OR has_column_privilege(runtime_role, 'tenancy.memberships', 'account_id', 'UPDATE')
       OR has_column_privilege(runtime_role, 'tenancy.memberships', 'is_initial_owner', 'UPDATE')
       OR has_table_privilege(runtime_role, 'tenancy.memberships', 'DELETE')
       OR has_table_privilege(runtime_role, 'tenancy.memberships', 'TRUNCATE')
       OR has_table_privilege(runtime_role, 'tenancy.memberships', 'REFERENCES')
       OR has_any_column_privilege(runtime_role, 'tenancy.memberships', 'REFERENCES')
       OR has_table_privilege(runtime_role, 'tenancy.memberships', 'TRIGGER') THEN
        RAISE EXCEPTION 'Admin API tenant-membership privileges are unsafe';
    END IF;
    FOREACH target_table IN ARRAY ARRAY[
        'tenancy.membership_lifecycle_receipts',
        'tenancy.tenant_lifecycle_receipts'] LOOP
        IF NOT has_table_privilege(runtime_role, target_table, 'SELECT')
           OR NOT has_table_privilege(runtime_role, target_table, 'INSERT')
           OR has_table_privilege(runtime_role, target_table, 'UPDATE')
           OR has_any_column_privilege(runtime_role, target_table, 'UPDATE')
           OR has_table_privilege(runtime_role, target_table, 'DELETE')
           OR has_table_privilege(runtime_role, target_table, 'TRUNCATE')
           OR has_table_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_any_column_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_any_column_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_table_privilege(runtime_role, target_table, 'TRIGGER') THEN
            RAISE EXCEPTION 'Admin API lifecycle-receipt privileges are unsafe on %', target_table;
        END IF;
    END LOOP;
    IF has_table_privilege(runtime_role, 'orders.command_receipts', 'INSERT')
       OR has_any_column_privilege(runtime_role, 'orders.command_receipts', 'INSERT')
       OR has_any_column_privilege(runtime_role, 'orders.command_receipts', 'UPDATE')
       OR has_any_column_privilege(runtime_role, 'orders.command_receipts', 'REFERENCES')
       OR has_table_privilege(runtime_role, 'orders.command_receipts', 'DELETE')
       OR has_table_privilege(runtime_role, 'orders.command_receipts', 'TRUNCATE')
       OR has_table_privilege(runtime_role, 'orders.command_receipts', 'TRIGGER') THEN
        RAISE EXCEPTION 'Admin API legacy Order receipt privileges are unsafe';
    END IF;
END
$verify$;

DO $profile_order_privileges$
DECLARE
    runtime_role text := current_setting('app.provision_admin_api_role');
    target_table text;
    target_column record;
BEGIN
    FOREACH target_table IN ARRAY ARRAY[
        'profiles.policy_heads', 'profiles.policy_revisions', 'profiles.publications',
        'profiles.authority', 'profiles.command_receipts'] LOOP
        IF NOT has_table_privilege(runtime_role, target_table, 'SELECT')
           OR has_table_privilege(runtime_role, target_table, 'INSERT')
                <> (target_table IN ('profiles.publications', 'profiles.authority', 'profiles.command_receipts'))
           OR has_table_privilege(runtime_role, target_table, 'UPDATE')
           OR has_table_privilege(runtime_role, target_table, 'DELETE')
           OR has_table_privilege(runtime_role, target_table, 'TRUNCATE')
           OR has_table_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_any_column_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_table_privilege(runtime_role, target_table, 'TRIGGER') THEN
            RAISE EXCEPTION 'Admin API profile table privileges are unsafe on %', target_table;
        END IF;
    END LOOP;
    FOR target_column IN
        SELECT c.relname AS table_name, a.attname AS column_name
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        JOIN pg_attribute AS a ON a.attrelid = c.oid
        WHERE n.nspname = 'profiles' AND c.relname IN
            ('policy_heads', 'policy_revisions', 'publications', 'authority', 'command_receipts')
          AND a.attnum > 0 AND NOT a.attisdropped
    LOOP
        target_table := format('profiles.%I', target_column.table_name);
        IF NOT has_column_privilege(runtime_role, target_table, target_column.column_name, 'SELECT')
           OR has_column_privilege(runtime_role, target_table, target_column.column_name, 'INSERT')
                <> (target_column.table_name IN ('publications', 'authority', 'command_receipts'))
           OR has_column_privilege(runtime_role, target_table, target_column.column_name, 'UPDATE')
                <> (target_column.table_name = 'authority'
                    AND target_column.column_name IN ('revision', 'active_profile_id', 'legacy_baseline_profile_id')) THEN
            RAISE EXCEPTION 'Admin API profile column privileges are unsafe on %.%', target_table, target_column.column_name;
        END IF;
    END LOOP;

    IF NOT has_table_privilege(runtime_role, 'orders.program_order_metadata', 'SELECT')
       OR NOT has_table_privilege(runtime_role, 'orders.program_order_metadata', 'INSERT')
       OR has_table_privilege(runtime_role, 'orders.program_order_metadata', 'UPDATE')
       OR has_table_privilege(runtime_role, 'orders.program_order_metadata', 'DELETE')
       OR has_table_privilege(runtime_role, 'orders.program_order_metadata', 'TRUNCATE')
       OR has_table_privilege(runtime_role, 'orders.program_order_metadata', 'REFERENCES')
       OR has_any_column_privilege(runtime_role, 'orders.program_order_metadata', 'REFERENCES')
       OR has_table_privilege(runtime_role, 'orders.program_order_metadata', 'TRIGGER') THEN
        RAISE EXCEPTION 'Admin API program-order metadata privileges are unsafe';
    END IF;
    FOR target_column IN
        SELECT a.attname AS column_name
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        JOIN pg_attribute AS a ON a.attrelid = c.oid
        WHERE n.nspname = 'orders' AND c.relname = 'program_order_metadata'
          AND a.attnum > 0 AND NOT a.attisdropped
    LOOP
        IF NOT has_column_privilege(runtime_role, 'orders.program_order_metadata', target_column.column_name, 'SELECT')
           OR NOT has_column_privilege(runtime_role, 'orders.program_order_metadata', target_column.column_name, 'INSERT')
           OR has_column_privilege(runtime_role, 'orders.program_order_metadata', target_column.column_name, 'UPDATE') THEN
            RAISE EXCEPTION 'Admin API program-order metadata column privileges are unsafe on %', target_column.column_name;
        END IF;
    END LOOP;

    IF has_table_privilege(runtime_role, 'orders.order_drafts', 'SELECT')
       OR has_table_privilege(runtime_role, 'orders.order_drafts', 'INSERT')
       OR has_table_privilege(runtime_role, 'orders.order_drafts', 'UPDATE')
       OR has_table_privilege(runtime_role, 'orders.order_drafts', 'DELETE')
       OR has_table_privilege(runtime_role, 'orders.order_drafts', 'TRUNCATE')
       OR has_table_privilege(runtime_role, 'orders.order_drafts', 'REFERENCES')
       OR has_any_column_privilege(runtime_role, 'orders.order_drafts', 'REFERENCES')
       OR has_table_privilege(runtime_role, 'orders.order_drafts', 'TRIGGER') THEN
        RAISE EXCEPTION 'Admin API Order head privileges are unsafe';
    END IF;
    FOR target_column IN
        SELECT a.attname AS column_name
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        JOIN pg_attribute AS a ON a.attrelid = c.oid
        WHERE n.nspname = 'orders' AND c.relname = 'order_drafts'
          AND a.attnum > 0 AND NOT a.attisdropped
    LOOP
        IF has_column_privilege(runtime_role, 'orders.order_drafts', target_column.column_name, 'SELECT')
                <> (target_column.column_name IN ('tenant_id', 'id', 'state', 'revision'))
           OR has_column_privilege(runtime_role, 'orders.order_drafts', target_column.column_name, 'INSERT')
           OR has_column_privilege(runtime_role, 'orders.order_drafts', target_column.column_name, 'UPDATE')
                <> (target_column.column_name = 'revision') THEN
            RAISE EXCEPTION 'Admin API Order head column privileges are unsafe on %', target_column.column_name;
        END IF;
    END LOOP;
    IF has_table_privilege(runtime_role, 'tenancy.tenant_authorization_state', 'SELECT')
       OR has_any_column_privilege(runtime_role, 'tenancy.tenant_authorization_state', 'INSERT')
       OR has_any_column_privilege(runtime_role, 'tenancy.tenant_authorization_state', 'UPDATE')
       OR has_any_column_privilege(runtime_role, 'tenancy.tenant_authorization_state', 'REFERENCES')
       OR has_table_privilege(runtime_role, 'tenancy.tenant_authorization_state', 'DELETE')
       OR has_table_privilege(runtime_role, 'tenancy.tenant_authorization_state', 'TRUNCATE')
       OR has_table_privilege(runtime_role, 'tenancy.tenant_authorization_state', 'TRIGGER') THEN
        RAISE EXCEPTION 'Admin API observed tenant authorization privileges are unsafe';
    END IF;
    FOR target_column IN
        SELECT a.attname AS column_name FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        JOIN pg_attribute a ON a.attrelid=c.oid
        WHERE n.nspname='tenancy' AND c.relname='tenant_authorization_state' AND a.attnum>0 AND NOT a.attisdropped
    LOOP
        IF has_column_privilege(runtime_role, 'tenancy.tenant_authorization_state', target_column.column_name, 'SELECT')
                <> (target_column.column_name IN ('tenant_id','revision')) THEN
            RAISE EXCEPTION 'Admin API observed tenant authorization column privileges are unsafe';
        END IF;
    END LOOP;
END
$profile_order_privileges$;
