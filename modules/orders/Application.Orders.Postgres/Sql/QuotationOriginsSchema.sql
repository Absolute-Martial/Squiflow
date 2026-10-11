CREATE TABLE orders.quotation_origins (
    tenant_id uuid NOT NULL,
    order_id uuid NOT NULL,
    quotation_id uuid NOT NULL,
    issued_revision_id uuid NOT NULL,
    number bigint NOT NULL CHECK (number > 0),
    revision_number bigint NOT NULL CHECK (revision_number > 0),
    PRIMARY KEY (tenant_id, order_id),
    UNIQUE (tenant_id, issued_revision_id),
    FOREIGN KEY (tenant_id, order_id) REFERENCES orders.order_drafts(tenant_id, id)
);
ALTER TABLE orders.quotation_origins ENABLE ROW LEVEL SECURITY;
ALTER TABLE orders.quotation_origins FORCE ROW LEVEL SECURITY;
CREATE POLICY quotation_origin_tenant ON orders.quotation_origins
    USING (tenant_id = nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.current_tenant',true),'')::uuid);
CREATE FUNCTION orders.require_new_quoted_order() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM 1 FROM orders.order_drafts WHERE tenant_id=NEW.tenant_id AND id=NEW.order_id AND state='draft' AND revision=1 FOR UPDATE;
    IF NOT FOUND OR EXISTS (SELECT 1 FROM orders.command_receipts WHERE tenant_id=NEW.tenant_id AND order_id=NEW.order_id) THEN
        RAISE EXCEPTION 'A quotation origin must be bound during Order creation' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER require_new_quoted_order BEFORE INSERT ON orders.quotation_origins
    FOR EACH ROW EXECUTE FUNCTION orders.require_new_quoted_order();
CREATE FUNCTION orders.protect_quotation_origin() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Quotation origins are immutable' USING ERRCODE = '23514'; END;
$$;
CREATE TRIGGER immutable_quotation_origin BEFORE UPDATE OR DELETE ON orders.quotation_origins
    FOR EACH ROW EXECUTE FUNCTION orders.protect_quotation_origin();
CREATE FUNCTION orders.protect_quoted_header() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM orders.quotation_origins WHERE tenant_id=OLD.tenant_id AND order_id=OLD.id) AND
       (TG_OP='DELETE' OR NEW.state='draft' OR
        ROW(NEW.tenant_id,NEW.id,NEW.created_by_account_id,NEW.created_at,NEW.summary,NEW.currency_code,NEW.total,
            NEW.customer_organization_id,NEW.customer_program_id) IS DISTINCT FROM
        ROW(OLD.tenant_id,OLD.id,OLD.created_by_account_id,OLD.created_at,OLD.summary,OLD.currency_code,OLD.total,
            OLD.customer_organization_id,OLD.customer_program_id)) THEN
        RAISE EXCEPTION 'An accepted quotation Order cannot be replaced' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER protect_quoted_header BEFORE UPDATE OR DELETE ON orders.order_drafts
    FOR EACH ROW EXECUTE FUNCTION orders.protect_quoted_header();
CREATE FUNCTION orders.protect_quoted_line() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE target_tenant uuid; target_order uuid;
BEGIN
    IF TG_OP='INSERT' THEN target_tenant:=NEW.tenant_id; target_order:=NEW.order_id;
    ELSE target_tenant:=OLD.tenant_id; target_order:=OLD.order_id; END IF;
    PERFORM 1 FROM orders.order_drafts WHERE tenant_id=target_tenant AND id=target_order FOR UPDATE;
    IF EXISTS (SELECT 1 FROM orders.quotation_origins WHERE tenant_id=target_tenant AND order_id=target_order) OR
       (TG_OP='UPDATE' AND EXISTS (SELECT 1 FROM orders.quotation_origins WHERE tenant_id=NEW.tenant_id AND order_id=NEW.order_id)) THEN
        RAISE EXCEPTION 'Accepted quotation Order lines cannot be replaced' USING ERRCODE='23514';
    END IF;
    IF TG_OP='DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER protect_quoted_line BEFORE INSERT OR UPDATE OR DELETE ON orders.order_draft_lines
    FOR EACH ROW EXECUTE FUNCTION orders.protect_quoted_line();
