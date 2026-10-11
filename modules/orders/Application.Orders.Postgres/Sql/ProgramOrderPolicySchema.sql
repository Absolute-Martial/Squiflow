CREATE TABLE orders.program_order_metadata (
    tenant_id uuid NOT NULL,
    order_id uuid NOT NULL,
    profile_id uuid NOT NULL,
    policy_revision_id uuid NOT NULL,
    require_reference boolean NOT NULL,
    external_reference text NULL CHECK (external_reference IS NULL OR
        (char_length(external_reference) BETWEEN 1 AND 128 AND external_reference !~ '[[:cntrl:]]')),
    baseline_principal_id uuid NULL,
    baseline_device_id uuid NULL,
    baseline_key text NULL,
    baseline_fingerprint text NULL,
    baseline_observed_revision bigint NULL,
    bound_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,order_id),
    UNIQUE (tenant_id,baseline_principal_id,baseline_key),
    FOREIGN KEY (tenant_id,order_id) REFERENCES orders.order_drafts(tenant_id,id),
    FOREIGN KEY (tenant_id,profile_id) REFERENCES profiles.publications(tenant_id,profile_id),
    FOREIGN KEY (tenant_id,policy_revision_id) REFERENCES profiles.policy_revisions(tenant_id,policy_id),
    CHECK ((baseline_principal_id IS NULL AND baseline_device_id IS NULL AND baseline_key IS NULL
        AND baseline_fingerprint IS NULL AND baseline_observed_revision IS NULL) OR
        (baseline_principal_id IS NOT NULL AND baseline_device_id IS NOT NULL AND baseline_key IS NOT NULL
        AND baseline_fingerprint IS NOT NULL AND baseline_observed_revision IS NOT NULL
        AND baseline_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid
        AND baseline_device_id <> '00000000-0000-0000-0000-000000000000'::uuid
        AND char_length(baseline_key) BETWEEN 1 AND 128 AND baseline_fingerprint ~ '^[0-9a-f]{64}$'
        AND baseline_observed_revision > 0 AND NOT require_reference))
);
ALTER TABLE orders.program_order_metadata ENABLE ROW LEVEL SECURITY;
ALTER TABLE orders.program_order_metadata FORCE ROW LEVEL SECURITY;
CREATE POLICY program_order_metadata_tenant ON orders.program_order_metadata
    USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
CREATE FUNCTION orders.guard_program_order_metadata() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE current_state text; current_revision bigint;
BEGIN
    IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Order policy evidence cannot be deleted' USING ERRCODE='23514'; END IF;
    SELECT state,revision INTO current_state,current_revision FROM orders.order_drafts
        WHERE tenant_id=NEW.tenant_id AND id=NEW.order_id FOR UPDATE;
    IF current_state IS DISTINCT FROM 'draft' THEN
        RAISE EXCEPTION 'Order reference changes require a draft' USING ERRCODE='23514';
    END IF;
    IF TG_OP='UPDATE' AND ROW(NEW.tenant_id,NEW.order_id,NEW.profile_id,NEW.policy_revision_id,NEW.require_reference,
        NEW.baseline_principal_id,NEW.baseline_device_id,NEW.baseline_key,NEW.baseline_fingerprint,NEW.baseline_observed_revision,NEW.bound_at)
        IS DISTINCT FROM ROW(OLD.tenant_id,OLD.order_id,OLD.profile_id,OLD.policy_revision_id,OLD.require_reference,
        OLD.baseline_principal_id,OLD.baseline_device_id,OLD.baseline_key,OLD.baseline_fingerprint,OLD.baseline_observed_revision,OLD.bound_at) THEN
        RAISE EXCEPTION 'Order profile binding is immutable' USING ERRCODE='23514';
    END IF;
    IF TG_OP='INSERT' AND NEW.baseline_principal_id IS NULL AND (current_revision<>1 OR EXISTS (
        SELECT 1 FROM orders.command_receipts WHERE tenant_id=NEW.tenant_id AND order_id=NEW.order_id)) THEN
        RAISE EXCEPTION 'Existing work requires an explicit baseline assignment' USING ERRCODE='23514';
    END IF;
    IF TG_OP='INSERT' AND NEW.baseline_observed_revision IS NOT NULL AND NEW.baseline_observed_revision<>current_revision THEN
        RAISE EXCEPTION 'Order baseline revision does not match' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER guard_program_order_metadata BEFORE INSERT OR UPDATE OR DELETE ON orders.program_order_metadata
    FOR EACH ROW EXECUTE FUNCTION orders.guard_program_order_metadata();

-- A reference edit advances only the draft revision; every accepted header and line fact stays frozen.
CREATE OR REPLACE FUNCTION orders.protect_quoted_header() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM orders.quotation_origins WHERE tenant_id=OLD.tenant_id AND order_id=OLD.id) THEN
        IF TG_OP='DELETE' THEN
            RAISE EXCEPTION 'An accepted quotation Order cannot be replaced' USING ERRCODE='23514';
        END IF;
        IF ROW(NEW.tenant_id,NEW.id,NEW.created_by_account_id,NEW.created_at,NEW.summary,NEW.currency_code,NEW.total,
            NEW.customer_organization_id,NEW.customer_program_id) IS DISTINCT FROM
            ROW(OLD.tenant_id,OLD.id,OLD.created_by_account_id,OLD.created_at,OLD.summary,OLD.currency_code,OLD.total,
            OLD.customer_organization_id,OLD.customer_program_id)
            OR (NEW.state='draft' AND NOT (OLD.state='draft' AND NEW.revision=OLD.revision+1
                AND (to_jsonb(NEW)-'revision') IS NOT DISTINCT FROM (to_jsonb(OLD)-'revision')
                AND EXISTS (SELECT 1 FROM orders.program_order_metadata WHERE tenant_id=OLD.tenant_id AND order_id=OLD.id))) THEN
            RAISE EXCEPTION 'An accepted quotation Order cannot be replaced' USING ERRCODE='23514';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;
