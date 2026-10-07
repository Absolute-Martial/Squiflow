-- Apply after all registered module migrations, including Catalog and Pricing. This file is
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
            WHERE nspname IN ('identity_access', 'tenancy', 'customers', 'catalog', 'pricing', 'orders') AND nspowner = role_record.oid)
       OR EXISTS (
           SELECT 1 FROM pg_class AS c
           JOIN pg_namespace AS n ON n.oid = c.relnamespace
            WHERE n.nspname IN ('identity_access', 'tenancy', 'customers', 'catalog', 'pricing', 'orders')
             AND c.relowner = role_record.oid) THEN
        RAISE EXCEPTION 'CoreApi runtime role must not own the database, schemas or application tables';
    END IF;
    IF EXISTS (
        SELECT 1 FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        WHERE ((n.nspname = 'orders'
                AND c.relname IN ('order_drafts', 'order_draft_lines', 'command_receipts', 'quotation_origins'))
            OR (n.nspname = 'customers'
                AND c.relname IN ('organizations', 'programs', 'organization_receipts', 'program_receipts',
                                   'individuals', 'individual_command_receipts',
                                   'representatives', 'representative_command_receipts',
                                   'duplicate_cases', 'duplicate_command_receipts', 'customer_redirects',
                                   'imports', 'import_rows', 'import_work', 'object_storage_usage',
                                   'import_source_objects', 'object_storage_reservations'))
            OR (n.nspname = 'catalog' AND c.relname IN ('units', 'items', 'unit_conversions', 'command_receipts'))
            OR (n.nspname = 'pricing' AND c.relname IN ('price_revisions', 'command_receipts', 'override_policies')))
          AND (NOT c.relrowsecurity OR NOT c.relforcerowsecurity)) THEN
        RAISE EXCEPTION 'Tenant-owned runtime tables must have forced row level security';
    END IF;

    EXECUTE format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), runtime_role);
    FOREACH target_schema IN ARRAY ARRAY['identity_access', 'tenancy', 'customers', 'catalog', 'pricing', 'orders'] LOOP
        EXECUTE format('GRANT USAGE ON SCHEMA %I TO %I', target_schema, runtime_role);
    END LOOP;
    FOREACH target_table IN ARRAY ARRAY[
        'identity_access.accounts', 'identity_access.external_identity_bindings',
        'tenancy.tenants', 'tenancy.memberships'] LOOP
        EXECUTE format('GRANT SELECT ON TABLE %s TO %I', target_table, runtime_role);
    END LOOP;
    EXECUTE format('GRANT UPDATE (revision) ON TABLE tenancy.tenants TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (revision, is_initial_owner) ON TABLE tenancy.memberships TO %I', runtime_role);

    FOREACH target_table IN ARRAY ARRAY[
        'tenancy.tenant_authorization_state',
        'tenancy.tenant_authorization_proposals',
        'tenancy.tenant_permission_grants',
        'tenancy.custom_roles',
        'tenancy.custom_role_assignments',
        'tenancy.owner_transfer_receipts'] LOOP
        EXECUTE format('GRANT SELECT, INSERT ON TABLE %s TO %I', target_table, runtime_role);
    END LOOP;
    EXECUTE format('GRANT INSERT ON TABLE tenancy.tenant_authorization_events TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (revision, updated_at) ON TABLE tenancy.tenant_authorization_state TO %I', runtime_role);
    EXECUTE format(
        'GRANT UPDATE (status, applied_authorization_revision, attempt_count, failure_code, updated_at) ON TABLE tenancy.tenant_authorization_proposals TO %I',
        runtime_role);
    EXECUTE format(
        'GRANT UPDATE (is_active, revision, granted_at, revoked_at) ON TABLE tenancy.tenant_permission_grants TO %I',
        runtime_role);
    EXECUTE format(
        'GRANT UPDATE (name, availability, revision, permission_ids, updated_at, retired_at) ON TABLE tenancy.custom_roles TO %I',
        runtime_role);
    EXECUTE format(
        'GRANT UPDATE (availability, revision, assigned_at, removed_at) ON TABLE tenancy.custom_role_assignments TO %I',
        runtime_role);
    FOREACH target_table IN ARRAY ARRAY[
        'customers.organizations', 'customers.programs',
        'customers.organization_receipts', 'customers.program_receipts',
        'customers.individuals', 'customers.individual_command_receipts',
        'customers.representatives', 'customers.representative_command_receipts',
        'customers.duplicate_cases', 'customers.duplicate_command_receipts', 'customers.customer_redirects',
         'customers.imports', 'customers.import_rows', 'customers.import_work',
         'customers.object_storage_usage', 'customers.import_source_objects', 'customers.object_storage_reservations',
        'catalog.units', 'catalog.items', 'catalog.unit_conversions', 'catalog.command_receipts',
        'pricing.price_revisions', 'pricing.command_receipts', 'pricing.override_policies',
        'orders.order_drafts', 'orders.order_draft_lines', 'orders.command_receipts', 'orders.quotation_origins'] LOOP
        EXECUTE format('GRANT SELECT, INSERT ON TABLE %s TO %I', target_table, runtime_role);
    END LOOP;
    EXECUTE format(
        'GRANT UPDATE (summary, currency_code, total, customer_organization_id, customer_program_id, state, revision, abandoned_at, abandoned_by_account_id, committed_at, committed_by_account_id) ON TABLE orders.order_drafts TO %I',
        runtime_role);
    EXECUTE format(
        'GRANT UPDATE (display_name, email, phone, normalized_name, normalized_email, normalized_phone, redirect_target_individual_id, availability, revision, availability_changed_at, availability_changed_by_account_id, contact_changed_at, contact_changed_by_account_id) ON TABLE customers.individuals TO %I',
        runtime_role);
    EXECUTE format(
        'GRANT UPDATE (individual_id, availability, revision, changed_at, changed_by_account_id) ON TABLE customers.representatives TO %I',
        runtime_role);
    EXECUTE format('GRANT UPDATE (evidence, outcome, resolved_by_account_id, resolved_at, reason) ON TABLE customers.duplicate_cases TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (requires_decision, duplicate_evidence, decision, mapping_customer_id, status, customer_id, error_code, error_message, processed_at, attempts) ON TABLE customers.import_rows TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (status, completed_at, last_error, authorization_revision, generation, worker_id, lease_expires_at, next_attempt_at) ON TABLE customers.import_work TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (source_object_key) ON TABLE customers.imports TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (reserved_bytes, retained_bytes, updated_at) ON TABLE customers.object_storage_usage TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (state, retention, expires_at, failure_code, retirement_generation, retirement_lease_id, retirement_lease_expires_at) ON TABLE customers.import_source_objects TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (state, updated_at) ON TABLE customers.object_storage_reservations TO %I', runtime_role);
    EXECUTE format('GRANT EXECUTE ON FUNCTION customers.discover_runnable_import_tenants(uuid,integer) TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (name, status, revision, retired_at, retired_by_account_id) ON TABLE catalog.units TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (name, description, status, revision, retired_at, retired_by_account_id, availability, availability_changed_at, availability_changed_by_account_id) ON TABLE catalog.items TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (state, published_at, retired_at) ON TABLE pricing.price_revisions TO %I', runtime_role);
    EXECUTE format('GRANT USAGE ON SEQUENCE pricing.price_revision_number_seq TO %I', runtime_role);
    EXECUTE format('GRANT DELETE ON TABLE orders.order_draft_lines TO %I', runtime_role);
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
    FOREACH target_schema IN ARRAY ARRAY['identity_access', 'tenancy', 'customers', 'catalog', 'pricing', 'orders'] LOOP
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
            OR (n.nspname = 'customers' AND c.relname IN (
                'organizations', 'programs', 'organization_receipts', 'program_receipts',
                'individuals', 'individual_command_receipts',
                'representatives', 'representative_command_receipts', 'imports',
                'object_storage_usage', 'import_source_objects', 'object_storage_reservations'))
            OR (n.nspname = 'orders' AND c.relname IN ('order_drafts', 'order_draft_lines', 'command_receipts', 'quotation_origins')))
          AND a.attnum > 0 AND NOT a.attisdropped
    LOOP
        target_table := format('%I.%I', target_column.schema_name, target_column.table_name);
        IF NOT has_column_privilege(runtime_role, target_table, target_column.column_name, 'SELECT')
           OR (target_column.schema_name IN ('customers', 'orders')
               AND NOT has_column_privilege(runtime_role, target_table, target_column.column_name, 'INSERT'))
           OR (target_column.schema_name = 'identity_access'
               AND (has_column_privilege(runtime_role, target_table, target_column.column_name, 'INSERT')
                    OR has_column_privilege(runtime_role, target_table, target_column.column_name, 'UPDATE')))
           OR (target_column.schema_name = 'tenancy'
               AND has_column_privilege(runtime_role, target_table, target_column.column_name, 'INSERT'))
           OR (target_column.schema_name = 'tenancy'
               AND has_column_privilege(runtime_role, target_table, target_column.column_name, 'UPDATE')
                   <> ((target_column.table_name = 'tenants' AND target_column.column_name = 'revision')
                    OR (target_column.table_name = 'memberships'
                        AND target_column.column_name IN ('revision', 'is_initial_owner'))))
           OR (target_column.schema_name IN ('customers', 'orders')
               AND has_column_privilege(runtime_role, target_table, target_column.column_name, 'UPDATE')
                   <> ((target_column.schema_name = 'orders' AND target_column.table_name = 'order_drafts'
                       AND target_column.column_name IN (
                           'summary', 'currency_code', 'total',
                           'customer_organization_id', 'customer_program_id',
                           'state', 'revision', 'abandoned_at', 'abandoned_by_account_id',
                           'committed_at', 'committed_by_account_id'))
                    OR (target_column.schema_name = 'customers' AND target_column.table_name = 'individuals'
                        AND target_column.column_name IN ('display_name', 'email', 'phone', 'availability', 'revision',
                            'normalized_name', 'normalized_email', 'normalized_phone', 'redirect_target_individual_id',
                            'availability_changed_at', 'availability_changed_by_account_id',
                            'contact_changed_at', 'contact_changed_by_account_id'))
                     OR (target_column.schema_name = 'customers' AND target_column.table_name = 'representatives'
                         AND target_column.column_name IN ('individual_id', 'availability', 'revision',
                             'changed_at', 'changed_by_account_id'))
                     OR (target_column.schema_name = 'customers' AND target_column.table_name = 'imports'
                         AND target_column.column_name = 'source_object_key')
                     OR (target_column.schema_name = 'customers' AND target_column.table_name = 'object_storage_usage'
                         AND target_column.column_name IN ('reserved_bytes', 'retained_bytes', 'updated_at'))
                      OR (target_column.schema_name = 'customers' AND target_column.table_name = 'import_source_objects'
                          AND target_column.column_name IN ('state', 'retention', 'expires_at', 'failure_code',
                              'retirement_generation', 'retirement_lease_id', 'retirement_lease_expires_at'))
                     OR (target_column.schema_name = 'customers' AND target_column.table_name = 'object_storage_reservations'
                         AND target_column.column_name IN ('state', 'updated_at')))) THEN
            RAISE EXCEPTION 'CoreApi runtime column privileges are unsafe on %', target_table;
        END IF;
    END LOOP;

    FOREACH target_table IN ARRAY ARRAY[
        'identity_access.accounts', 'identity_access.external_identity_bindings',
        'tenancy.tenants', 'tenancy.memberships',
        'customers.organizations', 'customers.programs',
        'customers.organization_receipts', 'customers.program_receipts',
        'customers.individuals', 'customers.individual_command_receipts',
        'customers.representatives', 'customers.representative_command_receipts',
        'customers.duplicate_cases', 'customers.duplicate_command_receipts', 'customers.customer_redirects',
        'customers.imports', 'customers.import_rows', 'customers.import_work',
        'customers.object_storage_usage', 'customers.import_source_objects', 'customers.object_storage_reservations',
        'catalog.units', 'catalog.items', 'catalog.unit_conversions', 'catalog.command_receipts',
        'pricing.price_revisions', 'pricing.command_receipts', 'pricing.override_policies',
        'orders.order_drafts', 'orders.order_draft_lines', 'orders.command_receipts', 'orders.quotation_origins'] LOOP
        IF has_table_privilege(runtime_role, target_table, 'DELETE')
               <> (target_table = 'orders.order_draft_lines')
           OR has_table_privilege(runtime_role, target_table, 'TRUNCATE')
           OR has_table_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_table_privilege(runtime_role, target_table, 'TRIGGER') THEN
            RAISE EXCEPTION 'CoreApi runtime table privileges are unsafe on %', target_table;
        END IF;
    END LOOP;

    FOREACH target_table IN ARRAY ARRAY[
        'tenancy.tenant_authorization_state',
        'tenancy.tenant_authorization_proposals',
        'tenancy.tenant_permission_grants',
        'tenancy.custom_roles',
        'tenancy.custom_role_assignments',
        'tenancy.owner_transfer_receipts'] LOOP
        IF NOT has_table_privilege(runtime_role, target_table, 'SELECT')
           OR NOT has_table_privilege(runtime_role, target_table, 'INSERT')
           OR has_table_privilege(runtime_role, target_table, 'DELETE')
           OR has_table_privilege(runtime_role, target_table, 'TRUNCATE')
           OR has_table_privilege(runtime_role, target_table, 'REFERENCES')
           OR has_table_privilege(runtime_role, target_table, 'TRIGGER') THEN
            RAISE EXCEPTION 'CoreApi authorization-administration table privileges are unsafe on %', target_table;
        END IF;
    END LOOP;

    FOR target_column IN
        SELECT n.nspname AS schema_name, c.relname AS table_name, a.attname AS column_name
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        JOIN pg_attribute AS a ON a.attrelid = c.oid
        WHERE n.nspname = 'tenancy'
          AND c.relname IN (
              'tenant_authorization_state', 'tenant_authorization_proposals',
              'tenant_permission_grants', 'custom_roles', 'custom_role_assignments',
              'owner_transfer_receipts')
          AND a.attnum > 0 AND NOT a.attisdropped
    LOOP
        target_table := format('%I.%I', target_column.schema_name, target_column.table_name);
        IF has_column_privilege(runtime_role, target_table, target_column.column_name, 'UPDATE')
               <> ((target_column.table_name = 'tenant_authorization_state'
                    AND target_column.column_name IN ('revision', 'updated_at'))
                OR (target_column.table_name = 'tenant_authorization_proposals'
                    AND target_column.column_name IN (
                        'status', 'applied_authorization_revision', 'attempt_count', 'failure_code', 'updated_at'))
                OR (target_column.table_name = 'tenant_permission_grants'
                    AND target_column.column_name IN ('is_active', 'revision', 'granted_at', 'revoked_at'))
                OR (target_column.table_name = 'custom_roles'
                    AND target_column.column_name IN (
                        'name', 'availability', 'revision', 'permission_ids', 'updated_at', 'retired_at'))
                OR (target_column.table_name = 'custom_role_assignments'
                    AND target_column.column_name IN (
                        'availability', 'revision', 'assigned_at', 'removed_at'))) THEN
            RAISE EXCEPTION 'CoreApi authorization-administration UPDATE privileges are unsafe on %.%',
                target_table, target_column.column_name;
        END IF;
    END LOOP;
    IF has_table_privilege(runtime_role, 'tenancy.tenant_authorization_events', 'SELECT')
       OR NOT has_table_privilege(runtime_role, 'tenancy.tenant_authorization_events', 'INSERT')
       OR has_table_privilege(runtime_role, 'tenancy.tenant_authorization_events', 'UPDATE')
       OR has_table_privilege(runtime_role, 'tenancy.tenant_authorization_events', 'DELETE') THEN
        RAISE EXCEPTION 'CoreApi authorization-event privileges are unsafe';
    END IF;
    FOR target_column IN
        SELECT n.nspname AS schema_name, c.relname AS table_name, a.attname AS column_name
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        JOIN pg_attribute AS a ON a.attrelid = c.oid
        WHERE ((n.nspname = 'customers' AND c.relname IN (
                'duplicate_cases', 'duplicate_command_receipts', 'customer_redirects',
                'imports', 'import_rows', 'import_work', 'object_storage_usage',
                'import_source_objects', 'object_storage_reservations'))
            OR (n.nspname = 'catalog' AND c.relname IN ('units', 'items', 'unit_conversions', 'command_receipts'))
            OR (n.nspname = 'pricing' AND c.relname IN ('price_revisions', 'command_receipts', 'override_policies')))
          AND a.attnum > 0 AND NOT a.attisdropped
    LOOP
        target_table := format('%I.%I', target_column.schema_name, target_column.table_name);
        IF NOT has_column_privilege(runtime_role, target_table, target_column.column_name, 'SELECT')
           OR NOT has_column_privilege(runtime_role, target_table, target_column.column_name, 'INSERT')
           OR has_column_privilege(runtime_role, target_table, target_column.column_name, 'UPDATE')
                <> ((target_column.schema_name = 'customers' AND target_column.table_name = 'duplicate_cases'
                     AND target_column.column_name IN ('evidence', 'outcome', 'resolved_by_account_id', 'resolved_at', 'reason'))
                  OR (target_column.schema_name = 'customers' AND target_column.table_name = 'imports'
                      AND target_column.column_name = 'source_object_key')
                  OR (target_column.schema_name = 'customers' AND target_column.table_name = 'import_rows'
                     AND target_column.column_name IN ('requires_decision', 'duplicate_evidence', 'decision', 'mapping_customer_id',
                         'status', 'customer_id', 'error_code', 'error_message', 'processed_at', 'attempts'))
                  OR (target_column.schema_name = 'customers' AND target_column.table_name = 'import_work'
                      AND target_column.column_name IN ('status', 'completed_at', 'last_error', 'authorization_revision',
                          'generation', 'worker_id', 'lease_expires_at', 'next_attempt_at'))
                  OR (target_column.schema_name = 'customers' AND target_column.table_name = 'object_storage_usage'
                      AND target_column.column_name IN ('reserved_bytes', 'retained_bytes', 'updated_at'))
                   OR (target_column.schema_name = 'customers' AND target_column.table_name = 'import_source_objects'
                       AND target_column.column_name IN ('state', 'retention', 'expires_at', 'failure_code',
                           'retirement_generation', 'retirement_lease_id', 'retirement_lease_expires_at'))
                  OR (target_column.schema_name = 'customers' AND target_column.table_name = 'object_storage_reservations'
                      AND target_column.column_name IN ('state', 'updated_at'))
                 OR (target_column.schema_name = 'catalog' AND target_column.table_name = 'units'
                     AND target_column.column_name IN ('name', 'status', 'revision', 'retired_at', 'retired_by_account_id'))
                 OR (target_column.schema_name = 'catalog' AND target_column.table_name = 'items'
                     AND target_column.column_name IN ('name', 'description', 'status', 'revision', 'retired_at', 'retired_by_account_id',
                         'availability', 'availability_changed_at', 'availability_changed_by_account_id'))
                 OR (target_column.schema_name = 'pricing' AND target_column.table_name = 'price_revisions'
                     AND target_column.column_name IN ('state', 'published_at', 'retired_at'))) THEN
            RAISE EXCEPTION 'CoreApi commercial column privileges are unsafe on %.%', target_table, target_column.column_name;
        END IF;
    END LOOP;
    IF NOT has_function_privilege(runtime_role, 'customers.discover_runnable_import_tenants(uuid,integer)', 'EXECUTE')
       OR NOT has_sequence_privilege(runtime_role, 'pricing.price_revision_number_seq', 'USAGE')
       OR has_sequence_privilege(runtime_role, 'pricing.price_revision_number_seq', 'UPDATE') THEN
        RAISE EXCEPTION 'CoreApi commercial function/sequence privileges are unsafe';
    END IF;
END
$verify$;


-- Quotation facts and links are append-only; only owned header/counter columns may change.
DO $quotations$
DECLARE runtime_role text := nullif(current_setting('app.provision_runtime_role', true), '');
    table_name text;
    target_column record;
BEGIN
    IF runtime_role IS NULL OR EXISTS (SELECT 1 FROM pg_namespace WHERE nspname='quotations'
        AND nspowner=(SELECT oid FROM pg_roles WHERE rolname=runtime_role)) OR EXISTS (
        SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='quotations'
        AND c.relowner=(SELECT oid FROM pg_roles WHERE rolname=runtime_role)) THEN
        RAISE EXCEPTION 'Unsafe quotation runtime owner';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
        WHERE n.nspname='quotations' AND c.relkind='r' AND (NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
        AND c.relname <> '__EFMigrationsHistory') THEN RAISE EXCEPTION 'Quotation RLS is required'; END IF;
    EXECUTE format('GRANT USAGE ON SCHEMA quotations TO %I', runtime_role);
    FOREACH table_name IN ARRAY ARRAY['heads','issued','numbers','receipts','responses','conversions'] LOOP
        EXECUTE format('REVOKE ALL ON TABLE quotations.%I FROM %I', table_name, runtime_role);
        EXECUTE format('GRANT SELECT, INSERT ON TABLE quotations.%I TO %I', table_name, runtime_role);
    END LOOP;
    EXECUTE format('GRANT UPDATE (version, number, draft, current_issued_id, last_issued_revision) ON quotations.heads TO %I', runtime_role);
    EXECUTE format('GRANT UPDATE (value) ON quotations.numbers TO %I', runtime_role);
    FOREACH table_name IN ARRAY ARRAY['heads','issued','numbers','receipts','responses','conversions'] LOOP
        IF NOT has_table_privilege(runtime_role, format('quotations.%I', table_name), 'SELECT') OR
           NOT has_table_privilege(runtime_role, format('quotations.%I', table_name), 'INSERT') OR
           has_table_privilege(runtime_role, format('quotations.%I', table_name), 'DELETE') OR
           has_table_privilege(runtime_role, format('quotations.%I', table_name), 'TRUNCATE') OR
           has_table_privilege(runtime_role, format('quotations.%I', table_name), 'UPDATE') OR
           has_table_privilege(runtime_role, format('quotations.%I', table_name), 'REFERENCES') OR
           has_table_privilege(runtime_role, format('quotations.%I', table_name), 'TRIGGER') THEN
            RAISE EXCEPTION 'Unsafe quotation runtime table privileges on quotations.%', table_name;
        END IF;
    END LOOP;
    FOR target_column IN
        SELECT c.relname AS table_name, a.attname AS column_name
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        JOIN pg_attribute AS a ON a.attrelid = c.oid
        WHERE n.nspname = 'quotations' AND c.relname IN ('heads','issued','numbers','receipts','responses','conversions')
          AND a.attnum > 0 AND NOT a.attisdropped
    LOOP
        IF has_column_privilege(runtime_role,
                format('quotations.%I', target_column.table_name), target_column.column_name, 'UPDATE')
           <> ((target_column.table_name = 'heads' AND target_column.column_name IN
                ('version','number','draft','current_issued_id','last_issued_revision')) OR
               (target_column.table_name = 'numbers' AND target_column.column_name = 'value')) THEN
            RAISE EXCEPTION 'Unsafe quotation runtime UPDATE privilege on quotations.%.%',
                target_column.table_name, target_column.column_name;
        END IF;
    END LOOP;
    IF has_schema_privilege(runtime_role,'quotations','CREATE') THEN
        RAISE EXCEPTION 'Unsafe quotation runtime schema privileges';
    END IF;
END
$quotations$;
