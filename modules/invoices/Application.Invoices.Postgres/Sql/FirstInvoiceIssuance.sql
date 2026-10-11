CREATE SCHEMA IF NOT EXISTS invoices;
REVOKE ALL ON SCHEMA invoices FROM PUBLIC;

CREATE FUNCTION invoices.valid_debtor(value jsonb) RETURNS boolean
LANGUAGE sql IMMUTABLE STRICT AS $body$
    SELECT jsonb_typeof(value)='object'
      AND octet_length(value::text)<=8192
      AND (SELECT count(*) FROM jsonb_object_keys(value))=9
      AND value ?& ARRAY['schemaVersion','kind','organizationId','organizationDisplayName','programId',
          'programDisplayName','individualId','observedRevision','displayName']
      AND value->'schemaVersion'='1'::jsonb
      AND value->'kind' IN ('1'::jsonb,'2'::jsonb,'3'::jsonb)
      AND CASE value->>'kind'
        WHEN '1' THEN jsonb_typeof(value->'organizationId')='string'
          AND jsonb_typeof(value->'organizationDisplayName')='string'
          AND length(value->>'organizationDisplayName') BETWEEN 1 AND 200
          AND value->'programId'='null'::jsonb AND value->'programDisplayName'='null'::jsonb
          AND value->'individualId'='null'::jsonb AND value->'observedRevision'='null'::jsonb
          AND value->'displayName'='null'::jsonb
        WHEN '2' THEN jsonb_typeof(value->'organizationId')='string'
          AND jsonb_typeof(value->'organizationDisplayName')='string'
          AND length(value->>'organizationDisplayName') BETWEEN 1 AND 200
          AND jsonb_typeof(value->'programId')='string' AND jsonb_typeof(value->'programDisplayName')='string'
          AND length(value->>'programDisplayName') BETWEEN 1 AND 200
          AND value->'individualId'='null'::jsonb AND value->'observedRevision'='null'::jsonb
          AND value->'displayName'='null'::jsonb
        WHEN '3' THEN value->'organizationId'='null'::jsonb AND value->'organizationDisplayName'='null'::jsonb
          AND value->'programId'='null'::jsonb AND value->'programDisplayName'='null'::jsonb
          AND jsonb_typeof(value->'individualId')='string' AND jsonb_typeof(value->'observedRevision')='number'
          AND jsonb_typeof(value->'displayName')='string' AND length(value->>'displayName') BETWEEN 1 AND 200
        ELSE false END;
$body$;

CREATE FUNCTION invoices.valid_receipt(value jsonb) RETURNS boolean
LANGUAGE sql IMMUTABLE STRICT AS $body$
    SELECT jsonb_typeof(value)='object' AND octet_length(value::text)<=4096
      AND (SELECT count(*) FROM jsonb_object_keys(value))=2
      AND value ?& ARRAY['schemaVersion','invoiceId'] AND value->'schemaVersion'='1'::jsonb
      AND jsonb_typeof(value->'invoiceId')='string';
$body$;

CREATE TABLE invoices.organization_ordinals (
    tenant_id uuid NOT NULL CHECK(tenant_id<>'00000000-0000-0000-0000-000000000000'),
    organization_id uuid NOT NULL CHECK(organization_id<>'00000000-0000-0000-0000-000000000000'),
    last_ordinal bigint NOT NULL CHECK(last_ordinal>0),
    PRIMARY KEY(tenant_id,organization_id)
);

CREATE TABLE invoices.issued_invoices (
    tenant_id uuid NOT NULL,
    invoice_id uuid NOT NULL CHECK(invoice_id<>'00000000-0000-0000-0000-000000000000'),
    source_order_id uuid NOT NULL CHECK(source_order_id<>'00000000-0000-0000-0000-000000000000'),
    source_order_revision bigint NOT NULL CHECK(source_order_revision>0),
    organization_id uuid NOT NULL,
    program_id uuid CHECK(program_id IS NULL OR program_id<>'00000000-0000-0000-0000-000000000000'),
    organization_ordinal bigint NOT NULL CHECK(organization_ordinal>0),
    currency_code text NOT NULL CHECK(currency_code='NPR'),
    total numeric NOT NULL CHECK(total>=0 AND total<=999999999999999.9999 AND scale(total)<=4),
    line_count integer NOT NULL CHECK(line_count BETWEEN 1 AND 100),
    debtor jsonb NOT NULL CHECK(invoices.valid_debtor(debtor)),
    issued_by_account_id uuid NOT NULL CHECK(issued_by_account_id<>'00000000-0000-0000-0000-000000000000'),
    issued_at timestamptz NOT NULL,
    business_date date NOT NULL CHECK(business_date=(issued_at AT TIME ZONE 'UTC')::date),
    schema_version integer NOT NULL CHECK(schema_version=1),
    PRIMARY KEY(tenant_id,invoice_id),
    UNIQUE(tenant_id,source_order_id),
    UNIQUE(tenant_id,organization_id,organization_ordinal),
    FOREIGN KEY(tenant_id,organization_id) REFERENCES invoices.organization_ordinals(tenant_id,organization_id),
    CONSTRAINT debtor_organization_matches_source CHECK((debtor->>'kind')='3' OR debtor->>'organizationId'=organization_id::text),
    CONSTRAINT debtor_program_matches_source CHECK((debtor->>'kind')<>'2' OR (program_id IS NOT NULL AND debtor->>'programId'=program_id::text))
);

CREATE TABLE invoices.issued_lines (
    tenant_id uuid NOT NULL,
    invoice_id uuid NOT NULL,
    position integer NOT NULL CHECK(position BETWEEN 1 AND 100),
    description text NOT NULL CHECK(length(description) BETWEEN 1 AND 300),
    quantity numeric NOT NULL CHECK(quantity>0 AND quantity<=999999999999999.9999 AND scale(quantity)<=4),
    unit_code text NOT NULL CHECK(length(unit_code) BETWEEN 1 AND 64),
    unit_price numeric NOT NULL CHECK(unit_price>=0 AND unit_price<=999999999999999.9999 AND scale(unit_price)<=4),
    line_total numeric NOT NULL CHECK(line_total>=0 AND line_total<=999999999999999.9999 AND scale(line_total)<=4),
    PRIMARY KEY(tenant_id,invoice_id,position),
    FOREIGN KEY(tenant_id,invoice_id) REFERENCES invoices.issued_invoices(tenant_id,invoice_id)
);

CREATE TABLE invoices.issue_receipts (
    tenant_id uuid NOT NULL,
    actor_account_id uuid NOT NULL CHECK(actor_account_id<>'00000000-0000-0000-0000-000000000000'),
    idempotency_key text NOT NULL CHECK(length(idempotency_key) BETWEEN 1 AND 128),
    fingerprint text NOT NULL CHECK(fingerprint ~ '^[A-Fa-f0-9]{64}$'),
    invoice_id uuid NOT NULL,
    response jsonb NOT NULL CHECK(invoices.valid_receipt(response)),
    PRIMARY KEY(tenant_id,actor_account_id,idempotency_key),
    UNIQUE(tenant_id,invoice_id),
    FOREIGN KEY(tenant_id,invoice_id) REFERENCES invoices.issued_invoices(tenant_id,invoice_id),
    CHECK(response->>'invoiceId'=invoice_id::text)
);

CREATE FUNCTION invoices.reject_issued_mutation() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
    RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Issued invoice history is immutable.';
END;
$body$;

CREATE FUNCTION invoices.guard_counter() RETURNS trigger LANGUAGE plpgsql AS $body$
BEGIN
    IF TG_OP<>'UPDATE' OR NEW.tenant_id<>OLD.tenant_id OR NEW.organization_id<>OLD.organization_id
       OR NEW.last_ordinal<=OLD.last_ordinal THEN
        RAISE EXCEPTION USING ERRCODE='42501',MESSAGE='Invoice reference allocation cannot be reversed.';
    END IF;
    RETURN NEW;
END;
$body$;

CREATE FUNCTION invoices.require_complete_issue() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE retained invoices.issued_invoices; count_lines bigint; sum_lines numeric; count_receipts bigint; allocated bigint;
BEGIN
    SELECT * INTO retained FROM invoices.issued_invoices
      WHERE tenant_id=NEW.tenant_id AND invoice_id=NEW.invoice_id;
    SELECT count(*),coalesce(sum(line_total),0) INTO count_lines,sum_lines FROM invoices.issued_lines
      WHERE tenant_id=NEW.tenant_id AND invoice_id=NEW.invoice_id;
    SELECT count(*) INTO count_receipts FROM invoices.issue_receipts
      WHERE tenant_id=NEW.tenant_id AND invoice_id=NEW.invoice_id AND actor_account_id=retained.issued_by_account_id;
    SELECT last_ordinal INTO allocated FROM invoices.organization_ordinals
      WHERE tenant_id=NEW.tenant_id AND organization_id=retained.organization_id;
    IF retained.invoice_id IS NULL OR count_lines<>retained.line_count OR sum_lines<>retained.total OR count_receipts<>1
       OR allocated IS NULL OR allocated<retained.organization_ordinal THEN
        RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Invoice issue facts must be committed together.';
    END IF;
    RETURN NULL;
END;
$body$;

CREATE TRIGGER immutable_invoices BEFORE UPDATE OR DELETE ON invoices.issued_invoices
FOR EACH ROW EXECUTE FUNCTION invoices.reject_issued_mutation();
CREATE TRIGGER immutable_lines BEFORE UPDATE OR DELETE ON invoices.issued_lines
FOR EACH ROW EXECUTE FUNCTION invoices.reject_issued_mutation();
CREATE TRIGGER immutable_receipts BEFORE UPDATE OR DELETE ON invoices.issue_receipts
FOR EACH ROW EXECUTE FUNCTION invoices.reject_issued_mutation();
CREATE TRIGGER monotonic_counter BEFORE UPDATE OR DELETE ON invoices.organization_ordinals
FOR EACH ROW EXECUTE FUNCTION invoices.guard_counter();
CREATE CONSTRAINT TRIGGER complete_invoice AFTER INSERT ON invoices.issued_invoices
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION invoices.require_complete_issue();
CREATE CONSTRAINT TRIGGER complete_lines AFTER INSERT ON invoices.issued_lines
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION invoices.require_complete_issue();
CREATE CONSTRAINT TRIGGER complete_receipt AFTER INSERT ON invoices.issue_receipts
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION invoices.require_complete_issue();

ALTER TABLE invoices.organization_ordinals ENABLE ROW LEVEL SECURITY;
ALTER TABLE invoices.organization_ordinals FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_ordinals ON invoices.organization_ordinals USING(tenant_id::text=current_setting('app.current_tenant',true))
WITH CHECK(tenant_id::text=current_setting('app.current_tenant',true));
ALTER TABLE invoices.issued_invoices ENABLE ROW LEVEL SECURITY;
ALTER TABLE invoices.issued_invoices FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_invoices ON invoices.issued_invoices USING(tenant_id::text=current_setting('app.current_tenant',true))
WITH CHECK(tenant_id::text=current_setting('app.current_tenant',true));
ALTER TABLE invoices.issued_lines ENABLE ROW LEVEL SECURITY;
ALTER TABLE invoices.issued_lines FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_lines ON invoices.issued_lines USING(tenant_id::text=current_setting('app.current_tenant',true))
WITH CHECK(tenant_id::text=current_setting('app.current_tenant',true));
ALTER TABLE invoices.issue_receipts ENABLE ROW LEVEL SECURITY;
ALTER TABLE invoices.issue_receipts FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_receipts ON invoices.issue_receipts USING(tenant_id::text=current_setting('app.current_tenant',true))
WITH CHECK(tenant_id::text=current_setting('app.current_tenant',true));
REVOKE ALL ON ALL TABLES IN SCHEMA invoices FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA invoices FROM PUBLIC;
