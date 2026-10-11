ALTER TABLE orders.program_order_metadata ADD COLUMN require_approval boolean NOT NULL DEFAULT false;
ALTER TABLE orders.program_order_metadata ADD CONSTRAINT legacy_baseline_without_approval
    CHECK (baseline_principal_id IS NULL OR NOT require_approval);
CREATE OR REPLACE FUNCTION orders.guard_program_order_metadata() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE current_state text; current_revision bigint;
BEGIN
    IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Order policy evidence cannot be deleted' USING ERRCODE='23514'; END IF;
    SELECT state,revision INTO current_state,current_revision FROM orders.order_drafts
        WHERE tenant_id=NEW.tenant_id AND id=NEW.order_id FOR UPDATE;
    IF current_state IS DISTINCT FROM 'draft' THEN
        RAISE EXCEPTION 'Order reference changes require a draft' USING ERRCODE='23514';
    END IF;
    IF TG_OP='UPDATE' AND ROW(NEW.tenant_id,NEW.order_id,NEW.profile_id,NEW.policy_revision_id,NEW.require_reference,NEW.require_approval,
        NEW.baseline_principal_id,NEW.baseline_device_id,NEW.baseline_key,NEW.baseline_fingerprint,NEW.baseline_observed_revision,NEW.bound_at)
        IS DISTINCT FROM ROW(OLD.tenant_id,OLD.order_id,OLD.profile_id,OLD.policy_revision_id,OLD.require_reference,OLD.require_approval,
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

CREATE TABLE orders.approval_requests (
    tenant_id uuid NOT NULL,
    request_id uuid NOT NULL CHECK (request_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    order_id uuid NOT NULL,
    requester_id uuid NOT NULL CHECK (requester_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    assigned_id uuid NOT NULL CHECK (assigned_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    revision_author_id uuid NOT NULL CHECK (revision_author_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    order_revision bigint NOT NULL CHECK (order_revision>0),
    subject jsonb NOT NULL CHECK (jsonb_typeof(subject)='object'),
    subject_fingerprint text NOT NULL CHECK (subject_fingerprint ~ '^[0-9a-f]{64}$'),
    revision bigint NOT NULL CHECK (revision>0),
    state text NOT NULL CHECK (state IN ('pending','approved','rejected','canceled','superseded')),
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL CHECK (updated_at>=created_at),
    PRIMARY KEY (tenant_id,request_id),
    FOREIGN KEY (tenant_id,order_id) REFERENCES orders.order_drafts(tenant_id,id),
    CHECK (assigned_id<>requester_id AND assigned_id<>revision_author_id)
);
CREATE UNIQUE INDEX approval_one_pending ON orders.approval_requests(tenant_id,order_id) WHERE state='pending';
CREATE INDEX approval_inbox_assignee ON orders.approval_requests(tenant_id,assigned_id,created_at DESC,request_id DESC) WHERE state='pending';
CREATE INDEX approval_inbox_requester ON orders.approval_requests(tenant_id,requester_id,created_at DESC,request_id DESC) WHERE state='pending';
CREATE INDEX approval_inbox_recovery ON orders.approval_requests(tenant_id,created_at DESC,request_id DESC) WHERE state='pending';
CREATE TABLE orders.approval_events (
    tenant_id uuid NOT NULL,
    request_id uuid NOT NULL,
    request_revision bigint NOT NULL CHECK (request_revision>0),
    actor_id uuid NOT NULL CHECK (actor_id<>'00000000-0000-0000-0000-000000000000'::uuid),
    authorization_revision bigint NOT NULL CHECK (authorization_revision>0),
    operation text NOT NULL CHECK (operation IN ('submit','approve','reject','reassign','cancel','recover-cancel','supersede')),
    command_scope text GENERATED ALWAYS AS (CASE WHEN operation IN ('approve','reject') THEN 'decide' ELSE operation END) STORED,
    idempotency_key text NOT NULL CHECK (char_length(idempotency_key) BETWEEN 1 AND 128),
    fingerprint text NOT NULL CHECK (fingerprint ~ '^[0-9a-f]{64}$'),
    response_json jsonb NOT NULL CHECK (jsonb_typeof(response_json)='object'),
    recorded_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,request_id,request_revision),
    UNIQUE (tenant_id,actor_id,command_scope,idempotency_key),
    FOREIGN KEY (tenant_id,request_id) REFERENCES orders.approval_requests(tenant_id,request_id)
);
CREATE TABLE orders.approval_commit_evidence (
    tenant_id uuid NOT NULL,
    order_id uuid NOT NULL,
    request_id uuid NOT NULL,
    request_revision bigint NOT NULL,
    order_revision bigint NOT NULL CHECK (order_revision>0),
    subject_fingerprint text NOT NULL CHECK (subject_fingerprint ~ '^[0-9a-f]{64}$'),
    response_json jsonb NOT NULL CHECK (jsonb_typeof(response_json)='object'),
    PRIMARY KEY (tenant_id,order_id),
    FOREIGN KEY (tenant_id,order_id) REFERENCES orders.order_drafts(tenant_id,id),
    FOREIGN KEY (tenant_id,request_id,request_revision) REFERENCES orders.approval_events(tenant_id,request_id,request_revision)
);
ALTER TABLE orders.approval_requests ENABLE ROW LEVEL SECURITY;
ALTER TABLE orders.approval_requests FORCE ROW LEVEL SECURITY;
ALTER TABLE orders.approval_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE orders.approval_events FORCE ROW LEVEL SECURITY;
ALTER TABLE orders.approval_commit_evidence ENABLE ROW LEVEL SECURITY;
ALTER TABLE orders.approval_commit_evidence FORCE ROW LEVEL SECURITY;
CREATE POLICY approval_requests_tenant ON orders.approval_requests
    USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
CREATE POLICY approval_events_tenant ON orders.approval_events
    USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
CREATE POLICY approval_commit_evidence_tenant ON orders.approval_commit_evidence
    USING (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid)
    WITH CHECK (tenant_id=nullif(current_setting('app.current_tenant',true),'')::uuid);
CREATE FUNCTION orders.guard_approval_request() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE current_state text; current_revision bigint;
BEGIN
    IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Approval evidence cannot be deleted' USING ERRCODE='23514'; END IF;
    SELECT state,revision INTO current_state,current_revision FROM orders.order_drafts
        WHERE tenant_id=NEW.tenant_id AND id=NEW.order_id FOR UPDATE;
    IF TG_OP='INSERT' AND (NEW.revision<>1 OR NEW.state<>'pending' OR current_state IS DISTINCT FROM 'draft' OR current_revision<>NEW.order_revision) THEN
        RAISE EXCEPTION 'Approval submission must bind the exact draft' USING ERRCODE='23514';
    END IF;
    IF TG_OP='UPDATE' THEN
        IF ROW(NEW.tenant_id,NEW.request_id,NEW.order_id,NEW.requester_id,NEW.revision_author_id,NEW.order_revision,NEW.subject,NEW.subject_fingerprint,NEW.created_at)
            IS DISTINCT FROM ROW(OLD.tenant_id,OLD.request_id,OLD.order_id,OLD.requester_id,OLD.revision_author_id,OLD.order_revision,OLD.subject,OLD.subject_fingerprint,OLD.created_at)
            OR OLD.state<>'pending' OR NEW.revision<>OLD.revision+1 OR
            (NEW.state<>'pending' AND NEW.assigned_id<>OLD.assigned_id) OR NEW.updated_at<OLD.updated_at THEN
            RAISE EXCEPTION 'Approval transition or immutable subject is invalid' USING ERRCODE='23514';
        END IF;
        IF NEW.state IN ('approved','rejected') AND (current_state IS DISTINCT FROM 'draft' OR current_revision<>NEW.order_revision) THEN
            RAISE EXCEPTION 'Approval decision requires the exact draft' USING ERRCODE='23514';
        END IF;
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER guard_approval_request BEFORE INSERT OR UPDATE OR DELETE ON orders.approval_requests
    FOR EACH ROW EXECUTE FUNCTION orders.guard_approval_request();
CREATE FUNCTION orders.guard_approval_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'Approval history and commitment evidence are immutable' USING ERRCODE='23514';
END;
$$;
CREATE TRIGGER guard_approval_events BEFORE UPDATE OR DELETE ON orders.approval_events
    FOR EACH ROW EXECUTE FUNCTION orders.guard_approval_append_only();
CREATE TRIGGER guard_approval_commit_evidence BEFORE UPDATE OR DELETE ON orders.approval_commit_evidence
    FOR EACH ROW EXECUTE FUNCTION orders.guard_approval_append_only();
CREATE FUNCTION orders.guard_approval_event_insert() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE request orders.approval_requests%ROWTYPE; payload jsonb;
BEGIN
    SELECT * INTO request FROM orders.approval_requests WHERE tenant_id=NEW.tenant_id AND request_id=NEW.request_id;
    payload:=NEW.response_json->'payload';
    IF request.request_id IS NULL OR NEW.request_revision<>request.revision OR
        NEW.response_json->>'schemaVersion' IS DISTINCT FROM '1' OR NEW.response_json->>'operation' IS DISTINCT FROM NEW.operation OR
        payload->>'requestId' IS DISTINCT FROM request.request_id::text OR payload->>'tenantId' IS DISTINCT FROM request.tenant_id::text OR
        payload->>'orderId' IS DISTINCT FROM request.order_id::text OR payload->>'requestedByAccountId' IS DISTINCT FROM request.requester_id::text OR
        payload->>'assignedAccountId' IS DISTINCT FROM request.assigned_id::text OR payload->>'revision' IS DISTINCT FROM request.revision::text OR
        payload->'subject' IS DISTINCT FROM request.subject OR payload->>'lastActorAccountId' IS DISTINCT FROM NEW.actor_id::text OR
        payload->>'authorizationRevision' IS DISTINCT FROM NEW.authorization_revision::text OR NEW.recorded_at<>request.updated_at OR
        (payload->>'updatedAt')::timestamptz IS DISTINCT FROM request.updated_at OR
        (payload->>'createdAt')::timestamptz IS DISTINCT FROM request.created_at OR
        payload->>'state' IS DISTINCT FROM (CASE request.state WHEN 'pending' THEN '1' WHEN 'approved' THEN '2'
            WHEN 'rejected' THEN '3' WHEN 'canceled' THEN '4' ELSE '5' END) OR
        payload->>'change' IS DISTINCT FROM (CASE NEW.operation WHEN 'submit' THEN '1' WHEN 'approve' THEN '2'
            WHEN 'reject' THEN '3' WHEN 'reassign' THEN '4' WHEN 'supersede' THEN '6' ELSE '5' END) OR
        (NEW.operation='submit' AND (request.revision<>1 OR request.state<>'pending' OR NEW.actor_id<>request.requester_id)) OR
        (NEW.operation='reassign' AND (request.revision<2 OR request.state<>'pending')) OR
        (NEW.operation IN ('approve','reject') AND (NEW.actor_id<>request.assigned_id OR NEW.actor_id=request.requester_id OR
            NEW.actor_id=request.revision_author_id OR request.state<>(CASE NEW.operation WHEN 'approve' THEN 'approved' ELSE 'rejected' END))) OR
        (NEW.operation IN ('cancel','recover-cancel') AND request.state<>'canceled') OR
        (NEW.operation='cancel' AND NEW.actor_id<>request.requester_id) OR
        (NEW.operation='supersede' AND request.state<>'superseded') THEN
        RAISE EXCEPTION 'Approval receipt does not describe the protected transition' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER guard_approval_event_insert BEFORE INSERT ON orders.approval_events
    FOR EACH ROW EXECUTE FUNCTION orders.guard_approval_event_insert();
CREATE FUNCTION orders.require_approval_event() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE request orders.approval_requests%ROWTYPE; event orders.approval_events%ROWTYPE; payload jsonb;
BEGIN
    SELECT * INTO request FROM orders.approval_requests WHERE tenant_id=NEW.tenant_id AND request_id=NEW.request_id;
    SELECT * INTO event FROM orders.approval_events WHERE tenant_id=request.tenant_id AND request_id=request.request_id AND request_revision=request.revision;
    payload:=event.response_json->'payload';
    IF event.request_id IS NULL OR event.response_json->>'schemaVersion' IS DISTINCT FROM '1' OR
        payload->>'requestId' IS DISTINCT FROM request.request_id::text OR payload->>'tenantId' IS DISTINCT FROM request.tenant_id::text OR
        payload->>'orderId' IS DISTINCT FROM request.order_id::text OR payload->>'requestedByAccountId' IS DISTINCT FROM request.requester_id::text OR
        payload->>'assignedAccountId' IS DISTINCT FROM request.assigned_id::text OR payload->>'revision' IS DISTINCT FROM request.revision::text OR
        payload->'subject' IS DISTINCT FROM request.subject OR payload->>'lastActorAccountId' IS DISTINCT FROM event.actor_id::text OR
        payload->>'authorizationRevision' IS DISTINCT FROM event.authorization_revision::text OR
        event.recorded_at<>request.updated_at OR (payload->>'updatedAt')::timestamptz<>request.updated_at OR
        (CASE request.state WHEN 'pending' THEN payload->>'state' NOT IN ('1') WHEN 'approved' THEN payload->>'state'<>'2'
            WHEN 'rejected' THEN payload->>'state'<>'3' WHEN 'canceled' THEN payload->>'state'<>'4' WHEN 'superseded' THEN payload->>'state'<>'5' END) OR
        (request.state IN ('approved','rejected') AND event.actor_id<>request.assigned_id) THEN
        RAISE EXCEPTION 'Approval transition requires consistent atomic receipt evidence' USING ERRCODE='23514';
    END IF;
    RETURN NULL;
END;
$$;
CREATE CONSTRAINT TRIGGER require_approval_event AFTER INSERT OR UPDATE ON orders.approval_requests
    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION orders.require_approval_event();
CREATE FUNCTION orders.require_commit_approval() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.state='committed' AND NEW.customer_program_id IS NOT NULL AND EXISTS (
        SELECT 1 FROM orders.program_order_metadata WHERE tenant_id=NEW.tenant_id AND order_id=NEW.id AND require_approval) THEN
        IF NOT EXISTS (SELECT 1 FROM orders.approval_commit_evidence evidence JOIN orders.approval_requests request
            ON request.tenant_id=evidence.tenant_id AND request.request_id=evidence.request_id
            WHERE evidence.tenant_id=NEW.tenant_id AND evidence.order_id=NEW.id AND request.order_id=NEW.id
                AND request.state='approved' AND request.revision=evidence.request_revision
                AND evidence.order_revision=NEW.revision-1 AND request.order_revision=evidence.order_revision
                AND evidence.subject_fingerprint=request.subject_fingerprint) THEN
            RAISE EXCEPTION 'Order commitment requires exact approval evidence' USING ERRCODE='23514';
        END IF;
    END IF;
    RETURN NULL;
END;
$$;
CREATE CONSTRAINT TRIGGER require_commit_approval AFTER UPDATE ON orders.order_drafts
    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION orders.require_commit_approval();
CREATE FUNCTION orders.guard_approval_commit_insert() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE request orders.approval_requests%ROWTYPE; event orders.approval_events%ROWTYPE;
BEGIN
    SELECT * INTO request FROM orders.approval_requests WHERE tenant_id=NEW.tenant_id AND request_id=NEW.request_id;
    SELECT * INTO event FROM orders.approval_events WHERE tenant_id=NEW.tenant_id AND request_id=NEW.request_id AND request_revision=NEW.request_revision;
    IF request.request_id IS NULL OR request.state<>'approved' OR request.order_id<>NEW.order_id OR
        request.revision<>NEW.request_revision OR request.order_revision<>NEW.order_revision OR request.subject_fingerprint<>NEW.subject_fingerprint OR
        event.operation<>'approve' OR NEW.response_json->>'requestId' IS DISTINCT FROM NEW.request_id::text OR
        NEW.response_json->>'requestRevision' IS DISTINCT FROM NEW.request_revision::text OR NEW.response_json->>'orderRevision' IS DISTINCT FROM NEW.order_revision::text OR
        NEW.response_json->>'subjectFingerprint' IS DISTINCT FROM NEW.subject_fingerprint OR
        NEW.response_json->>'approvedByAccountId' IS DISTINCT FROM event.actor_id::text OR
        NEW.response_json->>'authorizationRevision' IS DISTINCT FROM event.authorization_revision::text OR
        (NEW.response_json->>'approvedAt')::timestamptz IS DISTINCT FROM event.recorded_at OR NOT EXISTS (
            SELECT 1 FROM orders.order_drafts WHERE tenant_id=NEW.tenant_id AND id=NEW.order_id AND state='committed' AND revision=NEW.order_revision+1) THEN
        RAISE EXCEPTION 'Committed approval evidence must retain the exact decision' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER guard_approval_commit_insert BEFORE INSERT ON orders.approval_commit_evidence
    FOR EACH ROW EXECUTE FUNCTION orders.guard_approval_commit_insert();
