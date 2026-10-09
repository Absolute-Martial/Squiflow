CREATE SCHEMA IF NOT EXISTS purchasing;
REVOKE ALL ON SCHEMA purchasing FROM PUBLIC;
CREATE TABLE purchasing.suppliers (
    schema_version smallint NOT NULL DEFAULT 1 CHECK (schema_version=1),
    tenant_id uuid NOT NULL CHECK (tenant_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    supplier_id uuid NOT NULL CHECK (supplier_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    revision bigint NOT NULL CHECK (revision>0 AND revision<9223372036854775807),
    snapshot jsonb NOT NULL CHECK (jsonb_typeof(snapshot)='object' AND octet_length(snapshot::text)<=65536),
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL CHECK (updated_at>=created_at),
    PRIMARY KEY (tenant_id,supplier_id)
);
CREATE INDEX supplier_inbox ON purchasing.suppliers(tenant_id,created_at DESC,supplier_id DESC);
CREATE TABLE purchasing.purchases (
    schema_version smallint NOT NULL DEFAULT 1 CHECK (schema_version=1),
    tenant_id uuid NOT NULL CHECK (tenant_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    purchase_id uuid NOT NULL CHECK (purchase_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    supplier_id uuid NOT NULL,
    revision bigint NOT NULL CHECK (revision>0 AND revision<9223372036854775807),
    source jsonb NOT NULL CHECK (jsonb_typeof(source)='object' AND octet_length(source::text)<=65536),
    snapshot jsonb NOT NULL CHECK (jsonb_typeof(snapshot)='object' AND octet_length(snapshot::text)<=65536),
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL CHECK (updated_at>=created_at),
    PRIMARY KEY (tenant_id,purchase_id),
    FOREIGN KEY (tenant_id,supplier_id) REFERENCES purchasing.suppliers(tenant_id,supplier_id)
);
CREATE INDEX purchase_inbox ON purchasing.purchases(tenant_id,created_at DESC,purchase_id DESC);
CREATE INDEX supplier_payables ON purchasing.purchases(tenant_id,supplier_id);
CREATE TABLE purchasing.supplier_events (
    schema_version smallint NOT NULL DEFAULT 1 CHECK (schema_version=1),
    tenant_id uuid NOT NULL,
    supplier_id uuid NOT NULL,
    event_id uuid NOT NULL CHECK (event_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    revision bigint NOT NULL CHECK (revision>0),
    actor_id uuid NOT NULL CHECK (actor_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    authorization_revision bigint NOT NULL CHECK (authorization_revision>0),
    kind integer NOT NULL CHECK (kind BETWEEN 1 AND 3),
    facts jsonb NOT NULL CHECK (jsonb_typeof(facts)='object' AND octet_length(facts::text)<=65536),
    snapshot jsonb NOT NULL CHECK (jsonb_typeof(snapshot)='object' AND octet_length(snapshot::text)<=65536),
    recorded_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,supplier_id,event_id),
    UNIQUE (tenant_id,supplier_id,revision),
    FOREIGN KEY (tenant_id,supplier_id) REFERENCES purchasing.suppliers(tenant_id,supplier_id)
);
CREATE TABLE purchasing.supplier_command_receipts (
    schema_version smallint NOT NULL DEFAULT 1 CHECK (schema_version=1),
    tenant_id uuid NOT NULL,
    actor_id uuid NOT NULL,
    operation integer NOT NULL CHECK (operation BETWEEN 1 AND 3),
    idempotency_key text NOT NULL CHECK (char_length(idempotency_key) BETWEEN 1 AND 128),
    fingerprint text NOT NULL CHECK (fingerprint ~ '^[0-9a-f]{64}$'),
    supplier_id uuid NOT NULL,
    revision bigint NOT NULL,
    response jsonb NOT NULL CHECK (jsonb_typeof(response)='object' AND octet_length(response::text)<=65536),
    PRIMARY KEY (tenant_id,actor_id,operation,idempotency_key),
    UNIQUE (tenant_id,supplier_id,revision),
    FOREIGN KEY (tenant_id,supplier_id,revision) REFERENCES purchasing.supplier_events(tenant_id,supplier_id,revision)
);
CREATE TABLE purchasing.purchase_events (
    schema_version smallint NOT NULL DEFAULT 1 CHECK (schema_version=1),
    tenant_id uuid NOT NULL,
    purchase_id uuid NOT NULL,
    event_id uuid NOT NULL CHECK (event_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    revision bigint NOT NULL CHECK (revision>0),
    actor_id uuid NOT NULL CHECK (actor_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    authorization_revision bigint NOT NULL CHECK (authorization_revision>0),
    kind integer NOT NULL CHECK (kind BETWEEN 1 AND 6),
    facts jsonb NOT NULL CHECK (jsonb_typeof(facts)='object' AND octet_length(facts::text)<=65536),
    snapshot jsonb NOT NULL CHECK (jsonb_typeof(snapshot)='object' AND octet_length(snapshot::text)<=65536),
    recorded_at timestamptz NOT NULL,
    target_event_id uuid,
    PRIMARY KEY (tenant_id,purchase_id,event_id),
    UNIQUE (tenant_id,purchase_id,revision),
    FOREIGN KEY (tenant_id,purchase_id) REFERENCES purchasing.purchases(tenant_id,purchase_id)
    ,FOREIGN KEY (tenant_id,purchase_id,target_event_id) REFERENCES purchasing.purchase_events(tenant_id,purchase_id,event_id)
);
CREATE TABLE purchasing.purchase_command_receipts (
    schema_version smallint NOT NULL DEFAULT 1 CHECK (schema_version=1),
    tenant_id uuid NOT NULL,
    actor_id uuid NOT NULL,
    operation integer NOT NULL CHECK (operation BETWEEN 1 AND 6),
    idempotency_key text NOT NULL CHECK (char_length(idempotency_key) BETWEEN 1 AND 128),
    fingerprint text NOT NULL CHECK (fingerprint ~ '^[0-9a-f]{64}$'),
    purchase_id uuid NOT NULL,
    revision bigint NOT NULL,
    response jsonb NOT NULL CHECK (jsonb_typeof(response)='object' AND octet_length(response::text)<=65536),
    PRIMARY KEY (tenant_id,actor_id,operation,idempotency_key),
    UNIQUE (tenant_id,purchase_id,revision),
    FOREIGN KEY (tenant_id,purchase_id,revision) REFERENCES purchasing.purchase_events(tenant_id,purchase_id,revision)
);
ALTER TABLE purchasing.suppliers ENABLE ROW LEVEL SECURITY;
ALTER TABLE purchasing.suppliers FORCE ROW LEVEL SECURITY;
CREATE POLICY suppliers_tenant ON purchasing.suppliers
    USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
ALTER TABLE purchasing.purchases ENABLE ROW LEVEL SECURITY;
ALTER TABLE purchasing.purchases FORCE ROW LEVEL SECURITY;
CREATE POLICY purchases_tenant ON purchasing.purchases
    USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
ALTER TABLE purchasing.supplier_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE purchasing.supplier_events FORCE ROW LEVEL SECURITY;
CREATE POLICY supplier_events_tenant ON purchasing.supplier_events
    USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
ALTER TABLE purchasing.purchase_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE purchasing.purchase_events FORCE ROW LEVEL SECURITY;
CREATE POLICY purchase_events_tenant ON purchasing.purchase_events
    USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
ALTER TABLE purchasing.supplier_command_receipts ENABLE ROW LEVEL SECURITY;
ALTER TABLE purchasing.supplier_command_receipts FORCE ROW LEVEL SECURITY;
CREATE POLICY supplier_command_receipts_tenant ON purchasing.supplier_command_receipts
    USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
ALTER TABLE purchasing.purchase_command_receipts ENABLE ROW LEVEL SECURITY;
ALTER TABLE purchasing.purchase_command_receipts FORCE ROW LEVEL SECURITY;
CREATE POLICY purchase_command_receipts_tenant ON purchasing.purchase_command_receipts
    USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
CREATE FUNCTION purchasing.guard_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Purchasing journal facts and command receipts are immutable' USING ERRCODE='23514'; END; $$;
CREATE TRIGGER immutable_supplier_events BEFORE UPDATE OR DELETE ON purchasing.supplier_events FOR EACH ROW EXECUTE FUNCTION purchasing.guard_immutable();
CREATE TRIGGER immutable_purchase_events BEFORE UPDATE OR DELETE ON purchasing.purchase_events FOR EACH ROW EXECUTE FUNCTION purchasing.guard_immutable();
CREATE TRIGGER immutable_supplier_command_receipts BEFORE UPDATE OR DELETE ON purchasing.supplier_command_receipts FOR EACH ROW EXECUTE FUNCTION purchasing.guard_immutable();
CREATE TRIGGER immutable_purchase_command_receipts BEFORE UPDATE OR DELETE ON purchasing.purchase_command_receipts FOR EACH ROW EXECUTE FUNCTION purchasing.guard_immutable();

CREATE FUNCTION purchasing.valid_supplier_snapshot(value jsonb) RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $$
BEGIN
    RETURN coalesce(value ?& ARRAY['supplierId','tenantId','revision','name','contact','status','createdByAccountId','createdAt','updatedByAccountId','updatedAt','schemaVersion'] AND
        value->>'schemaVersion'='1' AND (value->>'supplierId')::uuid<>'00000000-0000-0000-0000-000000000000'::uuid AND
        (value->>'tenantId')::uuid<>'00000000-0000-0000-0000-000000000000'::uuid AND
        (value->>'revision')::bigint BETWEEN 1 AND 9223372036854775806 AND (value->>'status')::integer IN (1,2) AND
        char_length(value->>'name') BETWEEN 1 AND 128 AND value->>'name' !~ '[[:cntrl:]]' AND
        (value->'contact'='null'::jsonb OR char_length(value->>'contact') BETWEEN 1 AND 512 AND value->>'contact' !~ '[[:cntrl:]]') AND
        (value->>'createdByAccountId')::uuid<>'00000000-0000-0000-0000-000000000000'::uuid AND
        (value->>'updatedByAccountId')::uuid<>'00000000-0000-0000-0000-000000000000'::uuid AND
        (value->>'updatedAt')::timestamptz >= (value->>'createdAt')::timestamptz, false);
EXCEPTION WHEN OTHERS THEN RETURN false;
END; $$;

CREATE FUNCTION purchasing.valid_purchase_snapshot(projection jsonb) RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE receipt jsonb; payment jsonb; refund jsonb; parent jsonb;
    precision integer; requested numeric; received numeric:=0; cost numeric:=0;
    paid numeric:=0; refunded numeric:=0; allocated numeric; returned numeric;
BEGIN
    IF NOT coalesce(projection ?& ARRAY['purchaseId','tenantId','revision','supplier','unit','order','description','requestedQuantity','receipts','payments','refunds','cancelledQuantity','createdByAccountId','createdAt','updatedByAccountId','updatedAt','schemaVersion','receivedQuantity','totalCost','paidAmount','refundedAmount','outstandingPayable','remainingQuantity'] AND
        projection->>'schemaVersion'='1' AND purchasing.valid_supplier_snapshot(projection->'supplier') AND
        projection->'supplier'->>'status'='1' AND projection->'supplier'->>'tenantId'=projection->>'tenantId' AND
        (projection->>'purchaseId')::uuid<>'00000000-0000-0000-0000-000000000000'::uuid AND
        (projection->>'tenantId')::uuid<>'00000000-0000-0000-0000-000000000000'::uuid AND
        (projection->>'revision')::bigint BETWEEN 1 AND 9223372036854775806 AND
        char_length(projection->>'description') BETWEEN 1 AND 512 AND projection->>'description' !~ '[[:cntrl:]]' AND
        (projection->>'createdByAccountId')::uuid<>'00000000-0000-0000-0000-000000000000'::uuid AND
        (projection->>'updatedByAccountId')::uuid<>'00000000-0000-0000-0000-000000000000'::uuid AND
        (projection->>'updatedAt')::timestamptz >= (projection->>'createdAt')::timestamptz AND
        projection->'unit' ?& ARRAY['tenantId','unitId','unitCode','unitPrecision','unitRevision','isActive'] AND
        projection->'unit'->>'tenantId'=projection->>'tenantId' AND projection->'unit'->'isActive'='true'::jsonb AND
        (projection->'unit'->>'unitId')::uuid<>'00000000-0000-0000-0000-000000000000'::uuid AND
        char_length(projection->'unit'->>'unitCode') BETWEEN 1 AND 128 AND (projection->'unit'->>'unitRevision')::bigint>0 AND
        jsonb_typeof(projection->'receipts')='array' AND jsonb_typeof(projection->'payments')='array' AND jsonb_typeof(projection->'refunds')='array' AND
        jsonb_array_length(projection->'receipts')+jsonb_array_length(projection->'payments')+jsonb_array_length(projection->'refunds')<=128, false) THEN RETURN false; END IF;
    IF projection->'order'<>'null'::jsonb AND NOT coalesce(projection->'order' ?& ARRAY['orderId','linePosition','orderRevision'] AND
        (projection->'order'->>'orderId')::uuid<>'00000000-0000-0000-0000-000000000000'::uuid AND
        (projection->'order'->>'linePosition')::integer BETWEEN 1 AND 100 AND
        (projection->'order'->>'orderRevision')::bigint BETWEEN 1 AND 9223372036854775806,false) THEN RETURN false; END IF;
    precision:=(projection->'unit'->>'unitPrecision')::integer; requested:=(projection->>'requestedQuantity')::numeric;
    IF precision NOT BETWEEN 0 AND 4 OR requested<=0 OR requested>9999999999999999999.999999999 OR round(requested,precision)<>requested THEN RETURN false; END IF;
    IF EXISTS (SELECT 1 FROM jsonb_array_elements((projection->'receipts')||(projection->'payments')||(projection->'refunds')) x
        WHERE NOT coalesce(x ?& ARRAY['entryId','effectiveEventId','isVoided'] AND
          (x->>'entryId')::uuid<>'00000000-0000-0000-0000-000000000000'::uuid AND
          (x->>'effectiveEventId')::uuid<>'00000000-0000-0000-0000-000000000000'::uuid AND
          x->'isVoided' IN ('true'::jsonb,'false'::jsonb),false)) OR
       (SELECT count(*)<>count(DISTINCT x->>'entryId') OR count(*)<>count(DISTINCT x->>'effectiveEventId')
        FROM jsonb_array_elements((projection->'receipts')||(projection->'payments')||(projection->'refunds')) x) THEN RETURN false; END IF;
    FOR receipt IN SELECT * FROM jsonb_array_elements(projection->'receipts') LOOP
        IF NOT coalesce(receipt ?& ARRAY['quantity','cost'] AND (receipt->>'quantity')::numeric>0 AND
            (receipt->>'quantity')::numeric<=9999999999999999999.999999999 AND
            round((receipt->>'quantity')::numeric,precision)=(receipt->>'quantity')::numeric AND
            (receipt->>'cost')::numeric>0 AND (receipt->>'cost')::numeric<=99999999999999999.99 AND
            round((receipt->>'cost')::numeric,2)=(receipt->>'cost')::numeric,false) THEN RETURN false; END IF;
        IF receipt->'isVoided'='false'::jsonb THEN received:=received+(receipt->>'quantity')::numeric; cost:=cost+(receipt->>'cost')::numeric; END IF;
    END LOOP;
    FOR payment IN SELECT * FROM jsonb_array_elements(projection->'payments') LOOP
        SELECT x INTO parent FROM jsonb_array_elements(projection->'receipts') x WHERE x->>'entryId'=payment->>'receiptId';
        IF parent IS NULL OR NOT coalesce(payment ?& ARRAY['receiptId','amount','method','reference'] AND
            (payment->>'amount')::numeric>0 AND (payment->>'amount')::numeric<=99999999999999999.99 AND
            round((payment->>'amount')::numeric,2)=(payment->>'amount')::numeric AND (payment->>'method')::integer IN (1,2) AND
            (payment->'reference'='null'::jsonb OR char_length(payment->>'reference') BETWEEN 1 AND 128 AND payment->>'reference' !~ '[[:cntrl:]]'),false) OR
            payment->'isVoided'='false'::jsonb AND parent->'isVoided'='true'::jsonb THEN RETURN false; END IF;
        IF payment->'isVoided'='false'::jsonb THEN paid:=paid+(payment->>'amount')::numeric; END IF;
    END LOOP;
    FOR refund IN SELECT * FROM jsonb_array_elements(projection->'refunds') LOOP
        SELECT x INTO parent FROM jsonb_array_elements(projection->'payments') x WHERE x->>'entryId'=refund->>'paymentId';
        IF parent IS NULL OR NOT coalesce(refund ?& ARRAY['paymentId','amount','method','reference'] AND
            (refund->>'amount')::numeric>0 AND (refund->>'amount')::numeric<=99999999999999999.99 AND
            round((refund->>'amount')::numeric,2)=(refund->>'amount')::numeric AND (refund->>'method')::integer IN (1,2) AND
            (refund->'reference'='null'::jsonb OR char_length(refund->>'reference') BETWEEN 1 AND 128 AND refund->>'reference' !~ '[[:cntrl:]]'),false) OR
            refund->'isVoided'='false'::jsonb AND parent->'isVoided'='true'::jsonb THEN RETURN false; END IF;
        IF refund->'isVoided'='false'::jsonb THEN refunded:=refunded+(refund->>'amount')::numeric; END IF;
    END LOOP;
    FOR payment IN SELECT * FROM jsonb_array_elements(projection->'payments') LOOP
        SELECT coalesce(sum((x->>'amount')::numeric),0) INTO returned FROM jsonb_array_elements(projection->'refunds') x
            WHERE x->>'paymentId'=payment->>'entryId' AND x->'isVoided'='false'::jsonb;
        IF returned>(CASE WHEN payment->'isVoided'='true'::jsonb THEN 0 ELSE (payment->>'amount')::numeric END) THEN RETURN false; END IF;
    END LOOP;
    FOR receipt IN SELECT * FROM jsonb_array_elements(projection->'receipts') LOOP
        SELECT coalesce(sum((x->>'amount')::numeric),0) INTO allocated FROM jsonb_array_elements(projection->'payments') x
            WHERE x->>'receiptId'=receipt->>'entryId' AND x->'isVoided'='false'::jsonb;
        SELECT coalesce(sum((r->>'amount')::numeric),0) INTO returned FROM jsonb_array_elements(projection->'refunds') r
            JOIN jsonb_array_elements(projection->'payments') p ON p->>'entryId'=r->>'paymentId'
            WHERE p->>'receiptId'=receipt->>'entryId' AND p->'isVoided'='false'::jsonb AND r->'isVoided'='false'::jsonb;
        IF allocated-returned>(CASE WHEN receipt->'isVoided'='true'::jsonb THEN 0 ELSE (receipt->>'cost')::numeric END) THEN RETURN false; END IF;
    END LOOP;
    IF projection->'cancelledQuantity'<>'null'::jsonb AND ((projection->>'cancelledQuantity')::numeric<=0 OR
        round((projection->>'cancelledQuantity')::numeric,precision)<>(projection->>'cancelledQuantity')::numeric) THEN RETURN false; END IF;
    RETURN coalesce(received+coalesce((projection->>'cancelledQuantity')::numeric,0)<=requested AND
        (projection->>'receivedQuantity')::numeric=received AND (projection->>'totalCost')::numeric=cost AND
        (projection->>'paidAmount')::numeric=paid AND (projection->>'refundedAmount')::numeric=refunded AND
        (projection->>'outstandingPayable')::numeric=cost-paid+refunded AND
        (projection->>'remainingQuantity')::numeric=(CASE WHEN projection->'cancelledQuantity'='null'::jsonb THEN requested-received ELSE 0 END),false);
EXCEPTION WHEN OTHERS THEN RETURN false;
END; $$;

CREATE FUNCTION purchasing.guard_supplier() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Supplier history is retained' USING ERRCODE='23514'; END IF;
    IF NOT purchasing.valid_supplier_snapshot(NEW.snapshot) OR NEW.snapshot->>'supplierId' IS DISTINCT FROM NEW.supplier_id::text OR
       NEW.snapshot->>'tenantId' IS DISTINCT FROM NEW.tenant_id::text OR NEW.snapshot->>'revision' IS DISTINCT FROM NEW.revision::text OR
       (NEW.snapshot->>'createdAt')::timestamptz IS DISTINCT FROM NEW.created_at OR (NEW.snapshot->>'updatedAt')::timestamptz IS DISTINCT FROM NEW.updated_at THEN
        RAISE EXCEPTION 'Supplier projection is invalid' USING ERRCODE='23514'; END IF;
    IF TG_OP='INSERT' AND (NEW.revision<>1 OR NEW.snapshot->>'status'<>'1' OR NEW.snapshot->>'createdByAccountId' IS DISTINCT FROM NEW.snapshot->>'updatedByAccountId') OR
       TG_OP='UPDATE' AND (ROW(NEW.schema_version,NEW.tenant_id,NEW.supplier_id,NEW.created_at) IS DISTINCT FROM ROW(OLD.schema_version,OLD.tenant_id,OLD.supplier_id,OLD.created_at) OR
           NEW.revision<>OLD.revision+1 OR NEW.updated_at<OLD.updated_at OR
           OLD.snapshot->>'status'='2' AND NEW.snapshot->>'status'<>'2' OR
           NEW.snapshot->'createdByAccountId' IS DISTINCT FROM OLD.snapshot->'createdByAccountId') THEN
        RAISE EXCEPTION 'Supplier revisions and original identity are protected' USING ERRCODE='23514'; END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER guard_supplier BEFORE INSERT OR UPDATE OR DELETE ON purchasing.suppliers FOR EACH ROW EXECUTE FUNCTION purchasing.guard_supplier();

CREATE FUNCTION purchasing.guard_purchase() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Purchase history is retained' USING ERRCODE='23514'; END IF;
    IF NOT purchasing.valid_purchase_snapshot(NEW.snapshot) THEN
        RAISE EXCEPTION 'Purchase business projection is invalid' USING ERRCODE='23514'; END IF;
    IF NEW.snapshot->>'purchaseId' IS DISTINCT FROM NEW.purchase_id::text OR
       NEW.snapshot->>'tenantId' IS DISTINCT FROM NEW.tenant_id::text OR NEW.snapshot->>'revision' IS DISTINCT FROM NEW.revision::text OR
       (NEW.snapshot->>'createdAt')::timestamptz IS DISTINCT FROM NEW.created_at OR (NEW.snapshot->>'updatedAt')::timestamptz IS DISTINCT FROM NEW.updated_at OR
       NEW.source IS DISTINCT FROM jsonb_build_object('supplier',NEW.snapshot->'supplier','unit',NEW.snapshot->'unit','order',NEW.snapshot->'order') OR
       NEW.snapshot->'supplier'->>'supplierId' IS DISTINCT FROM NEW.supplier_id::text THEN
        RAISE EXCEPTION 'Purchase projection or source is invalid' USING ERRCODE='23514'; END IF;
    IF TG_OP='INSERT' AND (NEW.revision<>1 OR NEW.snapshot->>'createdByAccountId' IS DISTINCT FROM NEW.snapshot->>'updatedByAccountId' OR
        NEW.snapshot->'receipts'<>'[]'::jsonb OR NEW.snapshot->'payments'<>'[]'::jsonb OR NEW.snapshot->'refunds'<>'[]'::jsonb OR NEW.snapshot->'cancelledQuantity'<>'null'::jsonb) OR
       TG_OP='UPDATE' AND (ROW(NEW.schema_version,NEW.tenant_id,NEW.purchase_id,NEW.supplier_id,NEW.source,NEW.created_at) IS DISTINCT FROM
            ROW(OLD.schema_version,OLD.tenant_id,OLD.purchase_id,OLD.supplier_id,OLD.source,OLD.created_at) OR
            NEW.revision<>OLD.revision+1 OR NEW.updated_at<OLD.updated_at OR
            NEW.snapshot->'createdByAccountId' IS DISTINCT FROM OLD.snapshot->'createdByAccountId' OR
            NEW.snapshot->'description' IS DISTINCT FROM OLD.snapshot->'description' OR
            NEW.snapshot->'requestedQuantity' IS DISTINCT FROM OLD.snapshot->'requestedQuantity' OR
            OLD.snapshot->'cancelledQuantity'<>'null'::jsonb AND NEW.snapshot->'cancelledQuantity' IS DISTINCT FROM OLD.snapshot->'cancelledQuantity') THEN
        RAISE EXCEPTION 'Purchase revisions and retained request source are protected' USING ERRCODE='23514'; END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER guard_purchase BEFORE INSERT OR UPDATE OR DELETE ON purchasing.purchases FOR EACH ROW EXECUTE FUNCTION purchasing.guard_purchase();
CREATE FUNCTION purchasing.guard_supplier_event() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE head purchasing.suppliers%ROWTYPE;
BEGIN
    SELECT * INTO head FROM purchasing.suppliers WHERE tenant_id=NEW.tenant_id AND supplier_id=NEW.supplier_id;
    IF head.supplier_id IS NULL OR NEW.snapshot IS DISTINCT FROM head.snapshot OR NEW.revision IS DISTINCT FROM head.revision OR
       NEW.recorded_at IS DISTINCT FROM head.updated_at OR NEW.actor_id::text IS DISTINCT FROM head.snapshot->>'updatedByAccountId' OR
       NOT NEW.facts ?& ARRAY['eventId','revision','actorAccountId','recordedAt','authorizationRevision','kind','schemaVersion'] OR
       NEW.facts->>'schemaVersion' IS DISTINCT FROM '1' OR NEW.facts->>'eventId' IS DISTINCT FROM NEW.event_id::text OR
       NEW.facts->>'revision' IS DISTINCT FROM NEW.revision::text OR NEW.facts->>'actorAccountId' IS DISTINCT FROM NEW.actor_id::text OR
       NEW.facts->>'authorizationRevision' IS DISTINCT FROM NEW.authorization_revision::text OR NEW.facts->>'kind' IS DISTINCT FROM NEW.kind::text OR
       (NEW.facts->>'recordedAt')::timestamptz IS DISTINCT FROM NEW.recorded_at OR (NEW.kind=1)<>(NEW.revision=1) THEN
        RAISE EXCEPTION 'Purchasing journal does not match its protected head' USING ERRCODE='23514'; END IF;
    IF NEW.facts->'supplier' IS DISTINCT FROM head.snapshot OR NEW.kind=1 AND head.snapshot->>'status'<>'1' OR
       NEW.kind=3 AND head.snapshot->>'status'<>'2' THEN
        RAISE EXCEPTION 'Supplier journal does not match its retained identity' USING ERRCODE='23514'; END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER guard_supplier_event BEFORE INSERT ON purchasing.supplier_events FOR EACH ROW EXECUTE FUNCTION purchasing.guard_supplier_event();
CREATE FUNCTION purchasing.guard_supplier_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE head purchasing.suppliers%ROWTYPE; entry purchasing.supplier_events%ROWTYPE;
BEGIN
    SELECT * INTO head FROM purchasing.suppliers WHERE tenant_id=NEW.tenant_id AND supplier_id=NEW.supplier_id;
    SELECT * INTO entry FROM purchasing.supplier_events WHERE tenant_id=NEW.tenant_id AND supplier_id=NEW.supplier_id AND revision=NEW.revision;
    IF head.supplier_id IS NULL OR entry.event_id IS NULL OR NEW.revision IS DISTINCT FROM head.revision OR
       NEW.actor_id IS DISTINCT FROM entry.actor_id OR NEW.operation IS DISTINCT FROM entry.kind OR
       NEW.response->>'schemaVersion' IS DISTINCT FROM '1' OR NEW.response->>'operation' IS DISTINCT FROM NEW.operation::text OR
       NEW.response->'snapshot' IS DISTINCT FROM head.snapshot OR entry.snapshot IS DISTINCT FROM head.snapshot THEN
        RAISE EXCEPTION 'Purchasing command receipt does not match its atomic transition' USING ERRCODE='23514'; END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER guard_supplier_receipt BEFORE INSERT ON purchasing.supplier_command_receipts FOR EACH ROW EXECUTE FUNCTION purchasing.guard_supplier_receipt();
CREATE FUNCTION purchasing.require_supplier_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE head purchasing.suppliers%ROWTYPE; entry purchasing.supplier_events%ROWTYPE; receipt purchasing.supplier_command_receipts%ROWTYPE;
BEGIN
    SELECT * INTO head FROM purchasing.suppliers WHERE tenant_id=NEW.tenant_id AND supplier_id=NEW.supplier_id;
    SELECT * INTO entry FROM purchasing.supplier_events WHERE tenant_id=NEW.tenant_id AND supplier_id=NEW.supplier_id AND revision=head.revision;
    SELECT * INTO receipt FROM purchasing.supplier_command_receipts WHERE tenant_id=NEW.tenant_id AND supplier_id=NEW.supplier_id AND revision=head.revision;
    IF entry.event_id IS NULL OR receipt.supplier_id IS NULL OR entry.snapshot IS DISTINCT FROM head.snapshot OR
       entry.recorded_at IS DISTINCT FROM head.updated_at OR entry.actor_id IS DISTINCT FROM receipt.actor_id OR
       entry.kind IS DISTINCT FROM receipt.operation OR receipt.response->'snapshot' IS DISTINCT FROM head.snapshot THEN
        RAISE EXCEPTION 'Purchasing projection requires atomic journal and receipt evidence' USING ERRCODE='23514'; END IF;
    RETURN NULL;
END; $$;
CREATE CONSTRAINT TRIGGER require_supplier_evidence AFTER INSERT OR UPDATE ON purchasing.suppliers DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION purchasing.require_supplier_evidence();
CREATE FUNCTION purchasing.require_supplier_journal() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE receipt purchasing.supplier_command_receipts%ROWTYPE;
BEGIN
    SELECT * INTO receipt FROM purchasing.supplier_command_receipts WHERE tenant_id=NEW.tenant_id AND supplier_id=NEW.supplier_id AND revision=NEW.revision;
    IF receipt.supplier_id IS NULL OR receipt.actor_id IS DISTINCT FROM NEW.actor_id OR receipt.operation IS DISTINCT FROM NEW.kind OR
       receipt.response->'snapshot' IS DISTINCT FROM NEW.snapshot THEN
        RAISE EXCEPTION 'Purchasing journal requires its exact retained command receipt' USING ERRCODE='23514'; END IF;
    RETURN NULL;
END; $$;
CREATE CONSTRAINT TRIGGER require_supplier_journal AFTER INSERT ON purchasing.supplier_events DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION purchasing.require_supplier_journal();
CREATE FUNCTION purchasing.guard_purchase_event() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE head purchasing.purchases%ROWTYPE;
BEGIN
    SELECT * INTO head FROM purchasing.purchases WHERE tenant_id=NEW.tenant_id AND purchase_id=NEW.purchase_id;
    IF head.purchase_id IS NULL OR NEW.snapshot IS DISTINCT FROM head.snapshot OR NEW.revision IS DISTINCT FROM head.revision OR
       NEW.recorded_at IS DISTINCT FROM head.updated_at OR NEW.actor_id::text IS DISTINCT FROM head.snapshot->>'updatedByAccountId' OR
       NOT NEW.facts ?& ARRAY['eventId','revision','actorAccountId','recordedAt','authorizationRevision','kind','schemaVersion'] OR
       NEW.facts->>'schemaVersion' IS DISTINCT FROM '1' OR NEW.facts->>'eventId' IS DISTINCT FROM NEW.event_id::text OR
       NEW.facts->>'revision' IS DISTINCT FROM NEW.revision::text OR NEW.facts->>'actorAccountId' IS DISTINCT FROM NEW.actor_id::text OR
       NEW.facts->>'authorizationRevision' IS DISTINCT FROM NEW.authorization_revision::text OR NEW.facts->>'kind' IS DISTINCT FROM NEW.kind::text OR
       (NEW.facts->>'recordedAt')::timestamptz IS DISTINCT FROM NEW.recorded_at OR (NEW.kind=1)<>(NEW.revision=1) THEN
        RAISE EXCEPTION 'Purchasing journal does not match its protected head' USING ERRCODE='23514'; END IF;
    IF NOT NEW.facts ?& ARRAY['facts','reason','targetEventId','replacement'] OR
       (NEW.facts->>'targetEventId')::uuid IS DISTINCT FROM NEW.target_event_id OR (NEW.kind=5)<>(NEW.target_event_id IS NOT NULL) OR
       NEW.kind<>5 AND NEW.facts->'replacement'<>'null'::jsonb OR
       NEW.kind=5 AND (NEW.facts->'facts'<>'null'::jsonb OR char_length(NEW.facts->>'reason') NOT BETWEEN 1 AND 512) OR
       NEW.kind IN (1,2,3,4,6) AND NEW.facts->'facts'->>'kind' IS DISTINCT FROM NEW.kind::text OR
       NEW.kind IN (1,2,3,4) AND NEW.facts->'reason'<>'null'::jsonb OR
       NEW.kind=6 AND (char_length(NEW.facts->>'reason') NOT BETWEEN 1 AND 512 OR
           NEW.facts->'facts'->'quantity' IS DISTINCT FROM head.snapshot->'cancelledQuantity') THEN
        RAISE EXCEPTION 'Purchase journal shape or correction link is invalid' USING ERRCODE='23514'; END IF;
    IF NEW.kind=5 AND NOT EXISTS (SELECT 1 FROM purchasing.purchase_events e WHERE e.tenant_id=NEW.tenant_id AND
         e.purchase_id=NEW.purchase_id AND e.event_id=NEW.target_event_id AND e.revision<NEW.revision) THEN
        RAISE EXCEPTION 'Purchase correction requires its earlier exact target' USING ERRCODE='23514'; END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER guard_purchase_event BEFORE INSERT ON purchasing.purchase_events FOR EACH ROW EXECUTE FUNCTION purchasing.guard_purchase_event();
CREATE FUNCTION purchasing.guard_purchase_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE head purchasing.purchases%ROWTYPE; entry purchasing.purchase_events%ROWTYPE;
BEGIN
    SELECT * INTO head FROM purchasing.purchases WHERE tenant_id=NEW.tenant_id AND purchase_id=NEW.purchase_id;
    SELECT * INTO entry FROM purchasing.purchase_events WHERE tenant_id=NEW.tenant_id AND purchase_id=NEW.purchase_id AND revision=NEW.revision;
    IF head.purchase_id IS NULL OR entry.event_id IS NULL OR NEW.revision IS DISTINCT FROM head.revision OR
       NEW.actor_id IS DISTINCT FROM entry.actor_id OR NEW.operation IS DISTINCT FROM entry.kind OR
       NEW.response->>'schemaVersion' IS DISTINCT FROM '1' OR NEW.response->>'operation' IS DISTINCT FROM NEW.operation::text OR
       NEW.response->'snapshot' IS DISTINCT FROM head.snapshot OR entry.snapshot IS DISTINCT FROM head.snapshot THEN
        RAISE EXCEPTION 'Purchasing command receipt does not match its atomic transition' USING ERRCODE='23514'; END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER guard_purchase_receipt BEFORE INSERT ON purchasing.purchase_command_receipts FOR EACH ROW EXECUTE FUNCTION purchasing.guard_purchase_receipt();
CREATE FUNCTION purchasing.require_purchase_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE head purchasing.purchases%ROWTYPE; entry purchasing.purchase_events%ROWTYPE; receipt purchasing.purchase_command_receipts%ROWTYPE;
BEGIN
    SELECT * INTO head FROM purchasing.purchases WHERE tenant_id=NEW.tenant_id AND purchase_id=NEW.purchase_id;
    SELECT * INTO entry FROM purchasing.purchase_events WHERE tenant_id=NEW.tenant_id AND purchase_id=NEW.purchase_id AND revision=head.revision;
    SELECT * INTO receipt FROM purchasing.purchase_command_receipts WHERE tenant_id=NEW.tenant_id AND purchase_id=NEW.purchase_id AND revision=head.revision;
    IF entry.event_id IS NULL OR receipt.purchase_id IS NULL OR entry.snapshot IS DISTINCT FROM head.snapshot OR
       entry.recorded_at IS DISTINCT FROM head.updated_at OR entry.actor_id IS DISTINCT FROM receipt.actor_id OR
       entry.kind IS DISTINCT FROM receipt.operation OR receipt.response->'snapshot' IS DISTINCT FROM head.snapshot THEN
        RAISE EXCEPTION 'Purchasing projection requires atomic journal and receipt evidence' USING ERRCODE='23514'; END IF;
    RETURN NULL;
END; $$;
CREATE CONSTRAINT TRIGGER require_purchase_evidence AFTER INSERT OR UPDATE ON purchasing.purchases DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION purchasing.require_purchase_evidence();
CREATE FUNCTION purchasing.require_purchase_journal() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE receipt purchasing.purchase_command_receipts%ROWTYPE;
BEGIN
    SELECT * INTO receipt FROM purchasing.purchase_command_receipts WHERE tenant_id=NEW.tenant_id AND purchase_id=NEW.purchase_id AND revision=NEW.revision;
    IF receipt.purchase_id IS NULL OR receipt.actor_id IS DISTINCT FROM NEW.actor_id OR receipt.operation IS DISTINCT FROM NEW.kind OR
       receipt.response->'snapshot' IS DISTINCT FROM NEW.snapshot THEN
        RAISE EXCEPTION 'Purchasing journal requires its exact retained command receipt' USING ERRCODE='23514'; END IF;
    RETURN NULL;
END; $$;
CREATE CONSTRAINT TRIGGER require_purchase_journal AFTER INSERT ON purchasing.purchase_events DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION purchasing.require_purchase_journal();

-- Each effective entry remains tied to the exact append-only journal snapshot that produced it.
CREATE FUNCTION purchasing.require_purchase_entry_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE projection jsonb; element jsonb; effective purchasing.purchase_events%ROWTYPE;
    origin purchasing.purchase_events%ROWTYPE; previous jsonb; target jsonb; expected_kind integer; array_name text;
BEGIN
    SELECT snapshot INTO projection FROM purchasing.purchases WHERE tenant_id=NEW.tenant_id AND purchase_id=NEW.purchase_id;
    FOREACH array_name IN ARRAY ARRAY['receipts','payments','refunds'] LOOP
        expected_kind:=CASE array_name WHEN 'receipts' THEN 2 WHEN 'payments' THEN 3 ELSE 4 END;
        FOR element IN SELECT * FROM jsonb_array_elements(projection->array_name) LOOP
            SELECT * INTO origin FROM purchasing.purchase_events WHERE tenant_id=NEW.tenant_id AND purchase_id=NEW.purchase_id AND event_id=(element->>'entryId')::uuid;
            SELECT * INTO effective FROM purchasing.purchase_events WHERE tenant_id=NEW.tenant_id AND purchase_id=NEW.purchase_id AND event_id=(element->>'effectiveEventId')::uuid;
            IF origin.event_id IS NULL OR origin.kind<>expected_kind OR effective.event_id IS NULL OR
                effective.kind NOT IN (expected_kind,5) OR NOT EXISTS (SELECT 1 FROM jsonb_array_elements(effective.snapshot->array_name) x WHERE x=element) THEN
                RAISE EXCEPTION 'Purchase entry must match its original and effective journal evidence' USING ERRCODE='23514'; END IF;
            IF effective.event_id=origin.event_id AND (element->'isVoided'<>'false'::jsonb OR
                expected_kind=2 AND (effective.facts->'facts'->'quantity' IS DISTINCT FROM element->'quantity' OR effective.facts->'facts'->'amount' IS DISTINCT FROM element->'cost') OR
                expected_kind IN (3,4) AND (effective.facts->'facts'->'amount' IS DISTINCT FROM element->'amount' OR
                    effective.facts->'facts'->'method' IS DISTINCT FROM element->'method' OR effective.facts->'facts'->'reference' IS DISTINCT FROM element->'reference' OR
                    effective.facts->'facts'->(CASE expected_kind WHEN 3 THEN 'receiptId' ELSE 'paymentId' END) IS DISTINCT FROM element->(CASE expected_kind WHEN 3 THEN 'receiptId' ELSE 'paymentId' END))) THEN
                RAISE EXCEPTION 'Purchase entry does not match its recorded financial fact' USING ERRCODE='23514'; END IF;
            IF effective.kind=5 THEN
                SELECT snapshot INTO previous FROM purchasing.purchase_events WHERE tenant_id=NEW.tenant_id AND purchase_id=NEW.purchase_id AND revision=effective.revision-1;
                SELECT x INTO target FROM jsonb_array_elements(previous->array_name) x WHERE x->>'effectiveEventId'=effective.target_event_id::text AND x->'isVoided'='false'::jsonb;
                IF target IS NULL OR target->'entryId' IS DISTINCT FROM element->'entryId' OR
                    element->'isVoided' IS DISTINCT FROM to_jsonb(effective.facts->'replacement'='null'::jsonb) OR
                    effective.facts->'replacement'<>'null'::jsonb AND (effective.facts->'replacement'->>'kind' IS DISTINCT FROM expected_kind::text OR
                       expected_kind=2 AND (effective.facts->'replacement'->'quantity' IS DISTINCT FROM element->'quantity' OR effective.facts->'replacement'->'amount' IS DISTINCT FROM element->'cost') OR
                       expected_kind IN (3,4) AND (effective.facts->'replacement'->'amount' IS DISTINCT FROM element->'amount' OR
                          effective.facts->'replacement'->'method' IS DISTINCT FROM element->'method' OR effective.facts->'replacement'->'reference' IS DISTINCT FROM element->'reference' OR
                          effective.facts->'replacement'->(CASE expected_kind WHEN 3 THEN 'receiptId' ELSE 'paymentId' END) IS DISTINCT FROM element->(CASE expected_kind WHEN 3 THEN 'receiptId' ELSE 'paymentId' END))) THEN
                    RAISE EXCEPTION 'Purchase correction must preserve its exact stable dependency and append its replacement' USING ERRCODE='23514'; END IF;
            END IF;
        END LOOP;
    END LOOP;
    RETURN NULL;
END; $$;
CREATE CONSTRAINT TRIGGER require_purchase_entry_evidence AFTER INSERT OR UPDATE ON purchasing.purchases DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION purchasing.require_purchase_entry_evidence();
