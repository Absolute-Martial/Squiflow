-- Apply after the IdentityAccess, PlatformAdministration and Tenancy migrations.
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
           WHERE n.nspname IN ('identity_access', 'platform_administration', 'tenancy')
             AND c.relowner = role_record.oid)
       OR EXISTS (
           SELECT 1 FROM pg_namespace
           WHERE nspname IN ('identity_access', 'tenancy') AND nspowner = role_record.oid) THEN
        RAISE EXCEPTION 'Admin API runtime role must not own database/schema/tables';
    END IF;

    EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), runtime_role);
    EXECUTE format('GRANT USAGE ON SCHEMA identity_access TO %I', runtime_role);
    EXECUTE format('GRANT USAGE ON SCHEMA platform_administration TO %I', runtime_role);
    EXECUTE format('GRANT USAGE ON SCHEMA tenancy TO %I', runtime_role);
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
           OR has_table_privilege(runtime_role, target_table, 'TRIGGER') THEN
            RAISE EXCEPTION 'Admin API lifecycle-receipt privileges are unsafe on %', target_table;
        END IF;
    END LOOP;
END
$verify$;
