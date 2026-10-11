CREATE SCHEMA IF NOT EXISTS quotations;
CREATE TABLE quotations.heads (
    tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id),
    id uuid NOT NULL,
    created_by uuid NOT NULL REFERENCES identity_access.accounts(id),
    created_at timestamptz NOT NULL,
    version bigint NOT NULL CHECK (version > 0),
    number bigint CHECK (number > 0),
    facts_version integer NOT NULL DEFAULT 1 CHECK (facts_version = 1),
    draft jsonb CHECK (draft IS NULL OR (jsonb_typeof(draft) = 'object' AND octet_length(draft::text) <= 8388608)),
    current_issued_id uuid,
    last_issued_revision bigint NOT NULL DEFAULT 0 CHECK (last_issued_revision >= 0),
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, number),
    CHECK (draft IS NOT NULL OR current_issued_id IS NOT NULL)
);
CREATE TABLE quotations.issued (
    tenant_id uuid NOT NULL,
    quotation_id uuid NOT NULL,
    id uuid NOT NULL,
    revision bigint NOT NULL CHECK (revision > 0),
    facts_version integer NOT NULL DEFAULT 1 CHECK (facts_version = 1),
    facts jsonb NOT NULL CHECK (jsonb_typeof(facts) = 'object' AND octet_length(facts::text) <= 8388608),
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, quotation_id, id),
    UNIQUE (tenant_id, quotation_id, revision),
    FOREIGN KEY (tenant_id, quotation_id) REFERENCES quotations.heads(tenant_id, id)
);
ALTER TABLE quotations.heads ADD CONSTRAINT current_quotation_revision
    FOREIGN KEY (tenant_id, id, current_issued_id) REFERENCES quotations.issued(tenant_id, quotation_id, id);
CREATE TABLE quotations.numbers (
    tenant_id uuid PRIMARY KEY REFERENCES tenancy.tenants(id),
    value bigint NOT NULL CHECK (value > 0)
);
CREATE TABLE quotations.receipts (
    tenant_id uuid NOT NULL REFERENCES tenancy.tenants(id),
    account_id uuid NOT NULL REFERENCES identity_access.accounts(id),
    operation text NOT NULL CHECK (operation IN ('create', 'revise', 'issue')),
    key text NOT NULL CHECK (length(key) BETWEEN 1 AND 128),
    fingerprint text NOT NULL CHECK (fingerprint ~ '^[0-9a-f]{64}$'),
    version integer NOT NULL DEFAULT 1 CHECK (version = 1),
    response jsonb NOT NULL CHECK (jsonb_typeof(response) = 'object' AND octet_length(response::text) <= 16777216),
    PRIMARY KEY (tenant_id, account_id, operation, key)
);
CREATE FUNCTION quotations.reject_fact_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Retained quotation facts cannot be rewritten' USING ERRCODE = '23514'; END;
$$;
CREATE TRIGGER immutable_issued BEFORE UPDATE OR DELETE ON quotations.issued
    FOR EACH ROW EXECUTE FUNCTION quotations.reject_fact_mutation();
CREATE TRIGGER immutable_receipt BEFORE UPDATE OR DELETE ON quotations.receipts
    FOR EACH ROW EXECUTE FUNCTION quotations.reject_fact_mutation();
CREATE FUNCTION quotations.guard_head() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' OR NEW.tenant_id <> OLD.tenant_id OR NEW.id <> OLD.id OR
       NEW.facts_version <> OLD.facts_version OR NEW.created_by <> OLD.created_by OR NEW.created_at <> OLD.created_at OR
       NEW.version <> OLD.version + 1 OR (OLD.number IS NOT NULL AND NEW.number IS DISTINCT FROM OLD.number) OR
       (NEW.current_issued_id IS DISTINCT FROM OLD.current_issued_id AND
        (NEW.current_issued_id IS NULL OR NEW.last_issued_revision <> OLD.last_issued_revision + 1)) OR
       (NEW.current_issued_id IS NOT DISTINCT FROM OLD.current_issued_id AND NEW.last_issued_revision <> OLD.last_issued_revision)
    THEN RAISE EXCEPTION 'Invalid quotation transition' USING ERRCODE = '23514'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER bounded_head_transition BEFORE UPDATE OR DELETE ON quotations.heads
    FOR EACH ROW EXECUTE FUNCTION quotations.guard_head();
CREATE FUNCTION quotations.guard_number() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' OR NEW.tenant_id <> OLD.tenant_id OR NEW.value <> OLD.value + 1
    THEN RAISE EXCEPTION 'Invalid quotation number transition' USING ERRCODE = '23514'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER monotonic_number BEFORE UPDATE OR DELETE ON quotations.numbers
    FOR EACH ROW EXECUTE FUNCTION quotations.guard_number();
ALTER TABLE quotations.heads ENABLE ROW LEVEL SECURITY;
ALTER TABLE quotations.heads FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_heads ON quotations.heads USING (tenant_id = nullif(current_setting('app.current_tenant', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.current_tenant', true), '')::uuid);
ALTER TABLE quotations.issued ENABLE ROW LEVEL SECURITY;
ALTER TABLE quotations.issued FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_issued ON quotations.issued USING (tenant_id = nullif(current_setting('app.current_tenant', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.current_tenant', true), '')::uuid);
ALTER TABLE quotations.numbers ENABLE ROW LEVEL SECURITY;
ALTER TABLE quotations.numbers FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_numbers ON quotations.numbers USING (tenant_id = nullif(current_setting('app.current_tenant', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.current_tenant', true), '')::uuid);
ALTER TABLE quotations.receipts ENABLE ROW LEVEL SECURITY;
ALTER TABLE quotations.receipts FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_receipts ON quotations.receipts USING (tenant_id = nullif(current_setting('app.current_tenant', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.current_tenant', true), '')::uuid);
