CREATE TABLE quotations.responses (
    tenant_id uuid NOT NULL,
    quotation_id uuid NOT NULL,
    issued_revision_id uuid NOT NULL,
    kind smallint NOT NULL CHECK (kind IN (1,2,3)),
    facts_version integer NOT NULL DEFAULT 1 CHECK (facts_version=1),
    facts jsonb NOT NULL CHECK (jsonb_typeof(facts)='object' AND octet_length(facts::text)<=32768),
    PRIMARY KEY (tenant_id,issued_revision_id),
    FOREIGN KEY (tenant_id,quotation_id,issued_revision_id) REFERENCES quotations.issued(tenant_id,quotation_id,id)
);
CREATE TABLE quotations.conversions (
    tenant_id uuid NOT NULL,
    quotation_id uuid NOT NULL,
    issued_revision_id uuid NOT NULL,
    order_id uuid NOT NULL,
    facts_version integer NOT NULL DEFAULT 1 CHECK (facts_version=1),
    facts jsonb NOT NULL CHECK (jsonb_typeof(facts)='object' AND octet_length(facts::text)<=16777216),
    PRIMARY KEY (tenant_id,issued_revision_id),
    UNIQUE (tenant_id,quotation_id),
    UNIQUE (tenant_id,order_id),
    FOREIGN KEY (tenant_id,quotation_id,issued_revision_id) REFERENCES quotations.issued(tenant_id,quotation_id,id),
    FOREIGN KEY (tenant_id,issued_revision_id) REFERENCES quotations.responses(tenant_id,issued_revision_id),
    FOREIGN KEY (tenant_id,order_id) REFERENCES orders.order_drafts(tenant_id,id)
);
ALTER TABLE quotations.responses ENABLE ROW LEVEL SECURITY;
ALTER TABLE quotations.responses FORCE ROW LEVEL SECURITY;
ALTER TABLE quotations.conversions ENABLE ROW LEVEL SECURITY;
ALTER TABLE quotations.conversions FORCE ROW LEVEL SECURITY;
CREATE POLICY quotation_response_tenant ON quotations.responses
    USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
CREATE POLICY quotation_conversion_tenant ON quotations.conversions
    USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
CREATE TRIGGER immutable_response BEFORE UPDATE OR DELETE ON quotations.responses
    FOR EACH ROW EXECUTE FUNCTION quotations.reject_fact_mutation();
CREATE TRIGGER immutable_conversion BEFORE UPDATE OR DELETE ON quotations.conversions
    FOR EACH ROW EXECUTE FUNCTION quotations.reject_fact_mutation();
CREATE FUNCTION quotations.require_latest_response() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM 1 FROM quotations.heads WHERE tenant_id=NEW.tenant_id AND id=NEW.quotation_id
        AND current_issued_id=NEW.issued_revision_id FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'A response requires the latest issued revision' USING ERRCODE='23514'; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER require_latest_response BEFORE INSERT ON quotations.responses
    FOR EACH ROW EXECUTE FUNCTION quotations.require_latest_response();
CREATE FUNCTION quotations.require_accepted_conversion() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM 1 FROM quotations.heads WHERE tenant_id=NEW.tenant_id AND id=NEW.quotation_id
        AND current_issued_id=NEW.issued_revision_id FOR UPDATE;
    IF NOT FOUND OR NOT EXISTS (SELECT 1 FROM quotations.responses WHERE tenant_id=NEW.tenant_id AND issued_revision_id=NEW.issued_revision_id AND kind=1) THEN
        RAISE EXCEPTION 'A conversion requires the accepted issued revision' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER require_accepted_conversion BEFORE INSERT ON quotations.conversions
    FOR EACH ROW EXECUTE FUNCTION quotations.require_accepted_conversion();
ALTER TABLE quotations.receipts DROP CONSTRAINT receipts_operation_check;
ALTER TABLE quotations.receipts ADD CONSTRAINT receipts_operation_check CHECK (operation IN ('create','revise','issue','accept','reject','expire','convert'));
ALTER TABLE quotations.receipts DROP CONSTRAINT receipts_version_check;
ALTER TABLE quotations.receipts ADD CONSTRAINT receipts_version_check CHECK (version IN (1,2));
ALTER TABLE quotations.receipts DROP CONSTRAINT receipts_response_check;
ALTER TABLE quotations.receipts ADD CONSTRAINT receipts_response_check CHECK (jsonb_typeof(response)='object' AND
    octet_length(response::text)<=CASE WHEN version=1 THEN 16777216 ELSE 41943040 END);
CREATE OR REPLACE FUNCTION quotations.guard_head() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP='DELETE' OR NEW.tenant_id<>OLD.tenant_id OR NEW.id<>OLD.id OR
       NEW.facts_version<>OLD.facts_version OR NEW.created_by<>OLD.created_by OR NEW.created_at<>OLD.created_at OR
       NEW.version<>OLD.version+1 OR (OLD.number IS NOT NULL AND NEW.number IS DISTINCT FROM OLD.number) OR
       (NEW.current_issued_id IS DISTINCT FROM OLD.current_issued_id AND
        (NEW.current_issued_id IS NULL OR NEW.last_issued_revision<>OLD.last_issued_revision+1)) OR
       (NEW.current_issued_id IS NOT DISTINCT FROM OLD.current_issued_id AND NEW.last_issued_revision<>OLD.last_issued_revision) OR
       (EXISTS (SELECT 1 FROM quotations.responses WHERE tenant_id=OLD.tenant_id AND issued_revision_id=OLD.current_issued_id AND kind=1) AND
        (NEW.draft IS DISTINCT FROM OLD.draft OR NEW.current_issued_id IS DISTINCT FROM OLD.current_issued_id)) THEN
        RAISE EXCEPTION 'Invalid quotation transition' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;
