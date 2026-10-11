CREATE SCHEMA IF NOT EXISTS fulfillment;
REVOKE ALL ON SCHEMA fulfillment FROM PUBLIC;
CREATE TABLE fulfillment.works (
    schema_version smallint NOT NULL DEFAULT 1 CHECK (schema_version=1),
    tenant_id uuid NOT NULL,
    order_id uuid NOT NULL,
    work_id uuid NOT NULL,
    revision bigint NOT NULL CHECK (revision>0),
    source jsonb NOT NULL CHECK (jsonb_typeof(source)='object' AND octet_length(source::text)<=65536),
    snapshot jsonb NOT NULL CHECK (jsonb_typeof(snapshot)='object' AND octet_length(snapshot::text)<=65536),
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL CHECK (updated_at>=created_at),
    PRIMARY KEY (tenant_id,order_id),
    UNIQUE (tenant_id,work_id),
    CHECK (tenant_id<>'00000000-0000-0000-0000-000000000000'::uuid AND order_id<>'00000000-0000-0000-0000-000000000000'::uuid AND work_id<>'00000000-0000-0000-0000-000000000000'::uuid)
);
CREATE INDEX work_inbox ON fulfillment.works(tenant_id,created_at DESC,order_id DESC);
CREATE TABLE fulfillment.events (
    schema_version smallint NOT NULL DEFAULT 1 CHECK (schema_version=1),
    tenant_id uuid NOT NULL,
    order_id uuid NOT NULL,
    event_id uuid NOT NULL,
    revision bigint NOT NULL CHECK (revision>0),
    actor_id uuid NOT NULL,
    authorization_revision bigint NOT NULL CHECK (authorization_revision>0),
    kind integer NOT NULL CHECK (kind BETWEEN 1 AND 6),
    target_event_id uuid,
    facts jsonb NOT NULL CHECK (jsonb_typeof(facts)='object' AND octet_length(facts::text)<=65536),
    recorded_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,order_id,event_id),
    UNIQUE (tenant_id,order_id,revision),
    FOREIGN KEY (tenant_id,order_id) REFERENCES fulfillment.works(tenant_id,order_id),
    FOREIGN KEY (tenant_id,order_id,target_event_id) REFERENCES fulfillment.events(tenant_id,order_id,event_id),
    CHECK (event_id<>'00000000-0000-0000-0000-000000000000'::uuid AND actor_id<>'00000000-0000-0000-0000-000000000000'::uuid)
);
CREATE INDEX event_dependencies ON fulfillment.events(tenant_id,order_id,target_event_id) WHERE target_event_id IS NOT NULL;
CREATE TABLE fulfillment.entry_activity (
    schema_version smallint NOT NULL DEFAULT 1 CHECK (schema_version=1),
    tenant_id uuid NOT NULL,
    order_id uuid NOT NULL,
    event_id uuid NOT NULL,
    active boolean NOT NULL,
    revision bigint NOT NULL,
    kind integer NOT NULL CHECK (kind BETWEEN 1 AND 6),
    target_event_id uuid,
    voided_by uuid,
    PRIMARY KEY (tenant_id,order_id,event_id),
    FOREIGN KEY (tenant_id,order_id,event_id) REFERENCES fulfillment.events(tenant_id,order_id,event_id),
    FOREIGN KEY (tenant_id,order_id,voided_by) REFERENCES fulfillment.events(tenant_id,order_id,event_id),
    CHECK (NOT active OR voided_by IS NULL)
);
CREATE INDEX activity_successors ON fulfillment.entry_activity(tenant_id,order_id,revision) WHERE active;
CREATE INDEX activity_dependents ON fulfillment.entry_activity(tenant_id,order_id,target_event_id,kind) WHERE active;
CREATE TABLE fulfillment.command_receipts (
    schema_version smallint NOT NULL DEFAULT 1 CHECK (schema_version=1),
    tenant_id uuid NOT NULL,
    actor_id uuid NOT NULL,
    operation integer NOT NULL CHECK (operation BETWEEN 1 AND 6),
    idempotency_key text NOT NULL CHECK (char_length(idempotency_key) BETWEEN 1 AND 128),
    fingerprint text NOT NULL CHECK (fingerprint ~ '^[0-9a-f]{64}$'),
    order_id uuid NOT NULL,
    revision bigint NOT NULL,
    response jsonb NOT NULL CHECK (jsonb_typeof(response)='object' AND octet_length(response::text)<=65536),
    PRIMARY KEY (tenant_id,actor_id,operation,idempotency_key),
    UNIQUE (tenant_id,order_id,revision),
    FOREIGN KEY (tenant_id,order_id,revision) REFERENCES fulfillment.events(tenant_id,order_id,revision)
);
ALTER TABLE fulfillment.works ENABLE ROW LEVEL SECURITY;
ALTER TABLE fulfillment.works FORCE ROW LEVEL SECURITY;
ALTER TABLE fulfillment.events ENABLE ROW LEVEL SECURITY;
ALTER TABLE fulfillment.events FORCE ROW LEVEL SECURITY;
ALTER TABLE fulfillment.entry_activity ENABLE ROW LEVEL SECURITY;
ALTER TABLE fulfillment.entry_activity FORCE ROW LEVEL SECURITY;
ALTER TABLE fulfillment.command_receipts ENABLE ROW LEVEL SECURITY;
ALTER TABLE fulfillment.command_receipts FORCE ROW LEVEL SECURITY;
CREATE POLICY works_tenant ON fulfillment.works USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid) WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
CREATE POLICY events_tenant ON fulfillment.events USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid) WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
CREATE POLICY activity_tenant ON fulfillment.entry_activity USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid) WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
CREATE POLICY receipts_tenant ON fulfillment.command_receipts USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid) WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
CREATE FUNCTION fulfillment.guard_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Fulfillment facts and receipts are immutable' USING ERRCODE='23514'; END; $$;
CREATE TRIGGER immutable_events BEFORE UPDATE OR DELETE ON fulfillment.events FOR EACH ROW EXECUTE FUNCTION fulfillment.guard_immutable();
CREATE TRIGGER immutable_receipts BEFORE UPDATE OR DELETE ON fulfillment.command_receipts FOR EACH ROW EXECUTE FUNCTION fulfillment.guard_immutable();
CREATE FUNCTION fulfillment.guard_work() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Fulfillment work is retained' USING ERRCODE='23514'; END IF;
    IF TG_OP='UPDATE' AND (ROW(NEW.schema_version,NEW.tenant_id,NEW.order_id,NEW.work_id,NEW.source,NEW.created_at) IS DISTINCT FROM ROW(OLD.schema_version,OLD.tenant_id,OLD.order_id,OLD.work_id,OLD.source,OLD.created_at) OR NEW.revision<>OLD.revision+1 OR NEW.updated_at<OLD.updated_at) THEN
        RAISE EXCEPTION 'Fulfillment source is immutable and revisions are consecutive' USING ERRCODE='23514';
    END IF;
    IF TG_OP='INSERT' AND NEW.revision<>1 THEN RAISE EXCEPTION 'Fulfillment work must start at revision one' USING ERRCODE='23514'; END IF;
    IF NOT NEW.source ?& ARRAY['tenantId','orderId','orderRevision','programId','linePosition','itemId','unitId','unitCode','unitPrecision','orderedQuantity','isCommitted','lineCount','isCatalog','isNonStock','hasConversion','isService'] OR
       NOT NEW.snapshot ?& ARRAY['source','workId','tenantId','orderId','revision','started','completedQuantity','handedOffQuantity','activeDispositionId','activeDispositionKind','createdAt','updatedAt','stage'] OR
       NEW.source->>'tenantId' IS DISTINCT FROM NEW.tenant_id::text OR NEW.source->>'orderId' IS DISTINCT FROM NEW.order_id::text OR
       (NEW.source->>'orderRevision')::bigint<1 OR (NEW.source->>'linePosition')::integer<>1 OR (NEW.source->>'lineCount')::integer<>1 OR
       NEW.source->'isCommitted' IS DISTINCT FROM 'true'::jsonb OR NEW.source->'isCatalog' IS DISTINCT FROM 'true'::jsonb OR
       NEW.source->'isNonStock' IS DISTINCT FROM 'true'::jsonb OR NEW.source->'isService' IS DISTINCT FROM 'true'::jsonb OR NEW.source->'hasConversion' IS DISTINCT FROM 'false'::jsonb OR
       (NEW.source->>'orderedQuantity')::numeric<=0 OR (NEW.source->>'unitPrecision')::integer NOT BETWEEN 0 AND 4 OR
       (NEW.snapshot->>'completedQuantity')::numeric<0 OR (NEW.snapshot->>'completedQuantity')::numeric>(NEW.source->>'orderedQuantity')::numeric OR
       (NEW.snapshot->>'handedOffQuantity')::numeric<0 OR (NEW.snapshot->>'handedOffQuantity')::numeric>(NEW.snapshot->>'completedQuantity')::numeric OR
       round((NEW.snapshot->>'completedQuantity')::numeric,(NEW.source->>'unitPrecision')::integer)<>(NEW.snapshot->>'completedQuantity')::numeric OR
       round((NEW.snapshot->>'handedOffQuantity')::numeric,(NEW.source->>'unitPrecision')::integer)<>(NEW.snapshot->>'handedOffQuantity')::numeric OR
       NEW.snapshot->'started' NOT IN ('true'::jsonb,'false'::jsonb) OR
       ((NEW.snapshot->'activeDispositionId'='null'::jsonb) <> (NEW.snapshot->'activeDispositionKind'='null'::jsonb)) THEN
        RAISE EXCEPTION 'Fulfillment source or quantities are invalid' USING ERRCODE='23514';
    END IF;
    IF NEW.snapshot->'source' IS DISTINCT FROM NEW.source OR NEW.snapshot->>'tenantId' IS DISTINCT FROM NEW.tenant_id::text OR
       NEW.snapshot->>'orderId' IS DISTINCT FROM NEW.order_id::text OR NEW.snapshot->>'workId' IS DISTINCT FROM NEW.work_id::text OR
       NEW.snapshot->>'revision' IS DISTINCT FROM NEW.revision::text OR
       (NEW.snapshot->>'createdAt')::timestamptz IS DISTINCT FROM NEW.created_at OR (NEW.snapshot->>'updatedAt')::timestamptz IS DISTINCT FROM NEW.updated_at THEN
        RAISE EXCEPTION 'Fulfillment snapshot does not match its protected header' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER guard_work BEFORE INSERT OR UPDATE OR DELETE ON fulfillment.works FOR EACH ROW EXECUTE FUNCTION fulfillment.guard_work();
CREATE FUNCTION fulfillment.require_atomic_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE work fulfillment.works%ROWTYPE; event fulfillment.events%ROWTYPE; receipt fulfillment.command_receipts%ROWTYPE;
BEGIN
    SELECT * INTO work FROM fulfillment.works WHERE tenant_id=NEW.tenant_id AND order_id=NEW.order_id;
    SELECT * INTO event FROM fulfillment.events WHERE tenant_id=work.tenant_id AND order_id=work.order_id AND revision=work.revision;
    SELECT * INTO receipt FROM fulfillment.command_receipts WHERE tenant_id=work.tenant_id AND order_id=work.order_id AND revision=work.revision;
    IF event.event_id IS NULL OR receipt.order_id IS NULL OR receipt.actor_id<>event.actor_id OR
       receipt.response->>'schemaVersion' IS DISTINCT FROM '1' OR receipt.response->'work' IS DISTINCT FROM work.snapshot OR
       event.recorded_at IS DISTINCT FROM work.updated_at THEN
        RAISE EXCEPTION 'Fulfillment transition requires atomic event and receipt evidence' USING ERRCODE='23514';
    END IF;
    RETURN NULL;
END; $$;
CREATE CONSTRAINT TRIGGER require_atomic_evidence AFTER INSERT OR UPDATE ON fulfillment.works DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION fulfillment.require_atomic_evidence();

-- Journal, activity projection and caller receipt are inseparable durable evidence.
CREATE FUNCTION fulfillment.guard_event() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE work fulfillment.works%ROWTYPE;
BEGIN
    SELECT * INTO work FROM fulfillment.works WHERE tenant_id=NEW.tenant_id AND order_id=NEW.order_id;
    IF NEW.revision IS DISTINCT FROM work.revision OR NEW.recorded_at IS DISTINCT FROM work.updated_at OR
       NOT NEW.facts ?& ARRAY['eventId','revision','actorAccountId','recordedAt','authorizationRevision','kind','quantity','recipient','reason','disposition','targetEventId','replacement'] OR
       NEW.facts->>'eventId' IS DISTINCT FROM NEW.event_id::text OR NEW.facts->>'revision' IS DISTINCT FROM NEW.revision::text OR
       NEW.facts->>'actorAccountId' IS DISTINCT FROM NEW.actor_id::text OR NEW.facts->>'authorizationRevision' IS DISTINCT FROM NEW.authorization_revision::text OR
       NEW.facts->>'kind' IS DISTINCT FROM NEW.kind::text OR (NEW.facts->>'recordedAt')::timestamptz IS DISTINCT FROM NEW.recorded_at OR
       (NEW.facts->>'targetEventId')::uuid IS DISTINCT FROM NEW.target_event_id OR
       ((NEW.kind IN (5,6)) <> (NEW.target_event_id IS NOT NULL)) THEN
        RAISE EXCEPTION 'Fulfillment event does not match its current protected header' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER guard_event BEFORE INSERT ON fulfillment.events FOR EACH ROW EXECUTE FUNCTION fulfillment.guard_event();
CREATE FUNCTION fulfillment.guard_activity() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE event fulfillment.events%ROWTYPE; correction fulfillment.events%ROWTYPE;
BEGIN
    IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Fulfillment activity is retained' USING ERRCODE='23514'; END IF;
    SELECT * INTO event FROM fulfillment.events WHERE tenant_id=NEW.tenant_id AND order_id=NEW.order_id AND event_id=NEW.event_id;
    IF TG_OP='INSERT' THEN
        IF NEW.voided_by IS NOT NULL OR NEW.revision IS DISTINCT FROM event.revision OR
           NEW.kind IS DISTINCT FROM coalesce((event.facts->'replacement'->>'kind')::integer,event.kind) OR
           NEW.target_event_id IS DISTINCT FROM (CASE WHEN event.kind=6 THEN NULL ELSE event.target_event_id END) OR
           NEW.active IS DISTINCT FROM (event.kind<>6 OR event.facts->'replacement'<>'null'::jsonb) THEN
            RAISE EXCEPTION 'Fulfillment activity does not match its immutable event' USING ERRCODE='23514';
        END IF;
    ELSE
        SELECT * INTO correction FROM fulfillment.events WHERE tenant_id=NEW.tenant_id AND order_id=NEW.order_id AND event_id=NEW.voided_by;
        IF ROW(NEW.schema_version,NEW.tenant_id,NEW.order_id,NEW.event_id,NEW.revision,NEW.kind,NEW.target_event_id) IS DISTINCT FROM
           ROW(OLD.schema_version,OLD.tenant_id,OLD.order_id,OLD.event_id,OLD.revision,OLD.kind,OLD.target_event_id) OR
           NOT OLD.active OR OLD.voided_by IS NOT NULL OR NEW.active OR correction.kind IS DISTINCT FROM 6 OR
           correction.target_event_id IS DISTINCT FROM NEW.event_id OR correction.revision<=event.revision THEN
            RAISE EXCEPTION 'Fulfillment activity can only be voided by its linked correction' USING ERRCODE='23514';
        END IF;
    END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER guard_activity BEFORE INSERT OR UPDATE OR DELETE ON fulfillment.entry_activity FOR EACH ROW EXECUTE FUNCTION fulfillment.guard_activity();
CREATE FUNCTION fulfillment.guard_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE work fulfillment.works%ROWTYPE; event fulfillment.events%ROWTYPE;
BEGIN
    SELECT * INTO work FROM fulfillment.works WHERE tenant_id=NEW.tenant_id AND order_id=NEW.order_id;
    SELECT * INTO event FROM fulfillment.events WHERE tenant_id=NEW.tenant_id AND order_id=NEW.order_id AND revision=NEW.revision;
    IF NEW.revision IS DISTINCT FROM work.revision OR NEW.actor_id IS DISTINCT FROM event.actor_id OR NEW.operation IS DISTINCT FROM event.kind OR
       NEW.response->>'schemaVersion' IS DISTINCT FROM '1' OR NEW.response->>'operation' IS DISTINCT FROM NEW.operation::text OR
       NEW.response->'work' IS DISTINCT FROM work.snapshot THEN
        RAISE EXCEPTION 'Fulfillment receipt does not match its atomic transition' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER guard_receipt BEFORE INSERT ON fulfillment.command_receipts FOR EACH ROW EXECUTE FUNCTION fulfillment.guard_receipt();
CREATE FUNCTION fulfillment.require_journal_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE receipt fulfillment.command_receipts%ROWTYPE; activity fulfillment.entry_activity%ROWTYPE; target fulfillment.entry_activity%ROWTYPE;
BEGIN
    SELECT * INTO receipt FROM fulfillment.command_receipts WHERE tenant_id=NEW.tenant_id AND order_id=NEW.order_id AND revision=NEW.revision;
    SELECT * INTO activity FROM fulfillment.entry_activity WHERE tenant_id=NEW.tenant_id AND order_id=NEW.order_id AND event_id=NEW.event_id;
    IF receipt.order_id IS NULL OR activity.event_id IS NULL OR receipt.actor_id<>NEW.actor_id OR receipt.operation<>NEW.kind THEN
        RAISE EXCEPTION 'Fulfillment journal requires its receipt and activity evidence' USING ERRCODE='23514';
    END IF;
    IF NEW.kind=6 THEN
        SELECT * INTO target FROM fulfillment.entry_activity WHERE tenant_id=NEW.tenant_id AND order_id=NEW.order_id AND event_id=NEW.target_event_id;
        IF target.active IS DISTINCT FROM false OR target.voided_by IS DISTINCT FROM NEW.event_id THEN
            RAISE EXCEPTION 'Fulfillment correction must void its exact target' USING ERRCODE='23514';
        END IF;
    END IF;
    RETURN NULL;
END; $$;
CREATE CONSTRAINT TRIGGER require_journal_evidence AFTER INSERT ON fulfillment.events DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION fulfillment.require_journal_evidence();
