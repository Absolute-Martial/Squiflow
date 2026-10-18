-- Financial facts share the invoice transaction and migration boundary.
-- All source history is validated before the one-time recognition INSERT.
SET LOCAL row_security=off;
DO $validate$
BEGIN
    IF EXISTS (
        SELECT 1 FROM invoices.issued_invoices i
        LEFT JOIN LATERAL (
            SELECT count(*) AS count, coalesce(sum(l.line_total),0) AS total,
                min(l.position) AS first_position, max(l.position) AS last_position,
                bool_and(l.quantity>0 AND l.unit_price>=0 AND scale(l.quantity)<=4 AND scale(l.unit_price)<=4
                    AND l.quantity<=999999999999999.9999 AND l.unit_price<=999999999999999.9999
                    AND length(l.description) BETWEEN 1 AND 300 AND l.description=btrim(l.description)
                    AND l.description !~ '[[:cntrl:]]' AND length(l.unit_code) BETWEEN 1 AND 64
                    AND l.unit_code=btrim(l.unit_code) AND l.unit_code !~ '[[:cntrl:]]'
                    AND l.line_total=trunc(l.quantity*l.unit_price,4)+CASE
                        WHEN l.quantity*l.unit_price-trunc(l.quantity*l.unit_price,4)>0.00005
                          OR (l.quantity*l.unit_price-trunc(l.quantity*l.unit_price,4)=0.00005
                              AND mod(trunc(l.quantity*l.unit_price*10000),2)=1)
                        THEN 0.0001 ELSE 0 END) AS valid
            FROM invoices.issued_lines l WHERE l.tenant_id=i.tenant_id AND l.invoice_id=i.invoice_id
        ) lines ON true
        LEFT JOIN invoices.issue_receipts receipt ON receipt.tenant_id=i.tenant_id AND receipt.invoice_id=i.invoice_id
        LEFT JOIN invoices.organization_ordinals ordinal ON ordinal.tenant_id=i.tenant_id AND ordinal.organization_id=i.organization_id
        WHERE i.schema_version<>1 OR i.tenant_id='00000000-0000-0000-0000-000000000000'
          OR i.invoice_id='00000000-0000-0000-0000-000000000000' OR i.source_order_revision<1
          OR i.source_order_id='00000000-0000-0000-0000-000000000000'
          OR i.issued_by_account_id='00000000-0000-0000-0000-000000000000'
          OR i.organization_id='00000000-0000-0000-0000-000000000000'
          OR i.program_id='00000000-0000-0000-0000-000000000000'
          OR i.line_count NOT BETWEEN 1 AND 100 OR i.organization_ordinal<1
          OR i.currency_code<>'NPR' OR i.total<0 OR i.total>999999999999999.9999 OR scale(i.total)>4
          OR i.business_date<>(i.issued_at AT TIME ZONE 'UTC')::date
          OR NOT invoices.valid_debtor(i.debtor) OR i.debtor->'schemaVersion'<>'1'::jsonb
          OR (i.debtor->>'kind')::integer NOT BETWEEN 1 AND 3
          OR CASE (i.debtor->>'kind')::integer
            WHEN 1 THEN (i.debtor->>'organizationId')::uuid<>i.organization_id
              OR length(btrim(i.debtor->>'organizationDisplayName'))=0 OR i.debtor->>'organizationDisplayName' ~ '[[:cntrl:]]'
            WHEN 2 THEN (i.debtor->>'organizationId')::uuid<>i.organization_id
              OR i.program_id IS NULL OR (i.debtor->>'programId')::uuid<>i.program_id
              OR length(btrim(i.debtor->>'organizationDisplayName'))=0 OR length(btrim(i.debtor->>'programDisplayName'))=0
              OR i.debtor->>'organizationDisplayName' ~ '[[:cntrl:]]' OR i.debtor->>'programDisplayName' ~ '[[:cntrl:]]'
            WHEN 3 THEN (i.debtor->>'individualId')::uuid='00000000-0000-0000-0000-000000000000'
              OR (i.debtor->>'observedRevision')::bigint<1 OR length(btrim(i.debtor->>'displayName'))=0
              OR i.debtor->>'displayName' ~ '[[:cntrl:]]'
            ELSE true END
          OR lines.count<>i.line_count OR lines.first_position<>1 OR lines.last_position<>i.line_count
          OR lines.total<>i.total OR lines.valid IS DISTINCT FROM true
          OR receipt.invoice_id IS NULL OR NOT invoices.valid_receipt(receipt.response)
          OR receipt.actor_account_id<>i.issued_by_account_id OR (receipt.response->>'invoiceId')::uuid<>i.invoice_id
          OR receipt.fingerprint<>encode(sha256(convert_to('v1:issue-invoice:'||replace(i.source_order_id::text,'-','')||':'||i.source_order_revision::text||':'||
              CASE (i.debtor->>'kind')::integer WHEN 1 THEN 'organization' WHEN 2 THEN 'program'
                  ELSE 'individual:'||replace(((i.debtor->>'individualId')::uuid)::text,'-','') END,'UTF8')),'hex')
          OR ordinal.last_ordinal IS NULL OR ordinal.last_ordinal<i.organization_ordinal
    ) OR EXISTS (
        SELECT 1 FROM invoices.issued_lines l LEFT JOIN invoices.issued_invoices i USING(tenant_id,invoice_id)
        WHERE i.invoice_id IS NULL
    ) OR EXISTS (
        SELECT 1 FROM invoices.issue_receipts r LEFT JOIN invoices.issued_invoices i USING(tenant_id,invoice_id)
        WHERE i.invoice_id IS NULL
    ) THEN
        RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Retained invoice history is not valid for financial recognition.';
    END IF;
END;
$validate$;

CREATE TABLE invoices.receivables (
    tenant_id uuid NOT NULL,
    invoice_id uuid NOT NULL,
    debtor_kind integer NOT NULL CHECK(debtor_kind BETWEEN 1 AND 3),
    debtor_id uuid NOT NULL CHECK(debtor_id<>'00000000-0000-0000-0000-000000000000'),
    currency_code text NOT NULL CHECK(currency_code='NPR'),
    original_amount numeric NOT NULL CHECK(original_amount>=0 AND original_amount<=999999999999999.9999 AND scale(original_amount)<=4),
    invoice_date date NOT NULL,
    recognized_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    schema_version integer NOT NULL CHECK(schema_version=1),
    PRIMARY KEY(tenant_id,invoice_id),
    FOREIGN KEY(tenant_id,invoice_id) REFERENCES invoices.issued_invoices(tenant_id,invoice_id)
);

INSERT INTO invoices.receivables(tenant_id,invoice_id,debtor_kind,debtor_id,currency_code,original_amount,invoice_date,recognized_at,schema_version)
SELECT tenant_id,invoice_id,(debtor->>'kind')::integer,
    CASE (debtor->>'kind')::integer WHEN 1 THEN (debtor->>'organizationId')::uuid
        WHEN 2 THEN (debtor->>'programId')::uuid ELSE (debtor->>'individualId')::uuid END,
    currency_code,total,business_date,statement_timestamp(),1 FROM invoices.issued_invoices;

CREATE TABLE invoices.customer_payments (
    tenant_id uuid NOT NULL CHECK(tenant_id<>'00000000-0000-0000-0000-000000000000'),
    payment_id uuid NOT NULL CHECK(payment_id<>'00000000-0000-0000-0000-000000000000'),
    debtor_kind integer NOT NULL CHECK(debtor_kind BETWEEN 1 AND 3),
    debtor_id uuid NOT NULL CHECK(debtor_id<>'00000000-0000-0000-0000-000000000000'),
    currency_code text NOT NULL CHECK(currency_code='NPR'),
    amount numeric NOT NULL CHECK(amount>0 AND amount<=999999999999999.9999 AND scale(amount)<=4),
    channel integer NOT NULL CHECK(channel IN(1,2)),
    evidence text NOT NULL CHECK(length(evidence) BETWEEN 1 AND 1000 AND evidence=btrim(evidence) AND evidence !~ '[[:cntrl:]]'),
    external_reference text CHECK(external_reference IS NULL OR
        (length(external_reference) BETWEEN 1 AND 200 AND external_reference=btrim(external_reference) AND external_reference !~ '[[:cntrl:]]')),
    recorded_by_account_id uuid NOT NULL CHECK(recorded_by_account_id<>'00000000-0000-0000-0000-000000000000'),
    recorded_at timestamptz NOT NULL,
    schema_version integer NOT NULL CHECK(schema_version=1),
    PRIMARY KEY(tenant_id,payment_id),
    CHECK(channel<>2 OR external_reference IS NOT NULL)
);
CREATE UNIQUE INDEX bank_receipt_reference ON invoices.customer_payments(tenant_id,channel,external_reference COLLATE "C") WHERE channel=2;
CREATE INDEX payments_by_debtor ON invoices.customer_payments(tenant_id,debtor_kind,debtor_id,payment_id);
CREATE INDEX receivables_by_debtor ON invoices.receivables(tenant_id,debtor_kind,debtor_id,invoice_id);

CREATE TABLE invoices.customer_payment_events (
    tenant_id uuid NOT NULL,
    event_id uuid NOT NULL CHECK(event_id<>'00000000-0000-0000-0000-000000000000'),
    payment_id uuid NOT NULL,
    revision bigint NOT NULL CHECK(revision BETWEEN 1 AND 10000),
    operation integer NOT NULL CHECK(operation BETWEEN 1 AND 7),
    state integer NOT NULL CHECK(state BETWEEN 1 AND 5),
    actor_account_id uuid NOT NULL CHECK(actor_account_id<>'00000000-0000-0000-0000-000000000000'),
    recorded_at timestamptz NOT NULL,
    authorization_revision bigint NOT NULL CHECK(authorization_revision>0),
    evidence text CHECK(evidence IS NULL OR (length(evidence) BETWEEN 1 AND 1000 AND evidence=btrim(evidence) AND evidence !~ '[[:cntrl:]]')),
    receivable_id uuid,
    amount numeric CHECK(amount IS NULL OR (amount>0 AND amount<=999999999999999.9999 AND scale(amount)<=4)),
    allocation_id uuid,
    schema_version integer NOT NULL CHECK(schema_version=1),
    PRIMARY KEY(tenant_id,event_id),
    UNIQUE(tenant_id,payment_id,revision),
    UNIQUE(tenant_id,payment_id,event_id),
    FOREIGN KEY(tenant_id,payment_id) REFERENCES invoices.customer_payments(tenant_id,payment_id),
    FOREIGN KEY(tenant_id,receivable_id) REFERENCES invoices.receivables(tenant_id,invoice_id),
    FOREIGN KEY(tenant_id,payment_id,allocation_id) REFERENCES invoices.customer_payment_events(tenant_id,payment_id,event_id),
    CHECK((operation=1 AND revision=1 AND evidence IS NULL AND receivable_id IS NULL AND amount IS NULL AND allocation_id IS NULL)
        OR (operation BETWEEN 2 AND 5 AND evidence IS NOT NULL AND receivable_id IS NULL AND amount IS NULL AND allocation_id IS NULL)
        OR (operation=6 AND evidence IS NULL AND receivable_id IS NOT NULL AND amount IS NOT NULL AND allocation_id IS NULL)
        OR (operation=7 AND evidence IS NOT NULL AND receivable_id IS NOT NULL AND amount IS NOT NULL AND allocation_id IS NOT NULL))
);
CREATE UNIQUE INDEX allocation_released_once ON invoices.customer_payment_events(tenant_id,allocation_id) WHERE operation=7;
CREATE INDEX events_by_receivable ON invoices.customer_payment_events(tenant_id,receivable_id,operation);

CREATE TABLE invoices.customer_financial_receipts (
    tenant_id uuid NOT NULL,
    actor_account_id uuid NOT NULL,
    idempotency_key text NOT NULL CHECK(length(idempotency_key) BETWEEN 1 AND 128 AND idempotency_key ~ '^[!-~]+$'),
    fingerprint text NOT NULL CHECK(fingerprint ~ '^[A-F0-9]{64}$'),
    payment_id uuid NOT NULL,
    event_id uuid NOT NULL,
    schema_version integer NOT NULL CHECK(schema_version=1),
    PRIMARY KEY(tenant_id,actor_account_id,idempotency_key),
    UNIQUE(tenant_id,event_id),
    FOREIGN KEY(tenant_id,payment_id,event_id) REFERENCES invoices.customer_payment_events(tenant_id,payment_id,event_id)
);

CREATE FUNCTION invoices.require_recognition() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE retained invoices.issued_invoices; recognized invoices.receivables;
BEGIN
    SELECT * INTO retained FROM invoices.issued_invoices WHERE tenant_id=NEW.tenant_id AND invoice_id=NEW.invoice_id;
    SELECT * INTO recognized FROM invoices.receivables WHERE tenant_id=NEW.tenant_id AND invoice_id=NEW.invoice_id;
    IF retained.invoice_id IS NULL OR recognized.invoice_id IS NULL OR retained.schema_version<>1 OR recognized.schema_version<>1
       OR recognized.currency_code<>retained.currency_code OR recognized.original_amount<>retained.total
       OR recognized.invoice_date<>retained.business_date OR recognized.debtor_kind<>(retained.debtor->>'kind')::integer
       OR recognized.debtor_id<>(CASE (retained.debtor->>'kind')::integer WHEN 1 THEN (retained.debtor->>'organizationId')::uuid
           WHEN 2 THEN (retained.debtor->>'programId')::uuid ELSE (retained.debtor->>'individualId')::uuid END) THEN
        RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Invoice recognition must retain exactly the issued facts.';
    END IF;
    RETURN NULL;
END;
$body$;
CREATE CONSTRAINT TRIGGER invoice_recognition AFTER INSERT ON invoices.issued_invoices
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION invoices.require_recognition();
CREATE CONSTRAINT TRIGGER recognized_invoice AFTER INSERT ON invoices.receivables
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION invoices.require_recognition();

CREATE FUNCTION invoices.guard_customer_payment_event() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE payment invoices.customer_payments; previous invoices.customer_payment_events;
    allocation invoices.customer_payment_events; receivable invoices.receivables;
    allocated numeric; outstanding numeric; expected_state integer; last_evidence text;
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended('customer-payment:'||NEW.tenant_id::text||':'||NEW.payment_id::text,0));
    SELECT * INTO payment FROM invoices.customer_payments WHERE tenant_id=NEW.tenant_id AND payment_id=NEW.payment_id;
    SELECT * INTO previous FROM invoices.customer_payment_events WHERE tenant_id=NEW.tenant_id AND payment_id=NEW.payment_id
        ORDER BY revision DESC LIMIT 1;
    IF payment.payment_id IS NULL OR NEW.revision<>coalesce(previous.revision,0)+1 OR previous.state IN(3,5)
        OR (NEW.revision>5000 AND NEW.operation NOT IN(5,7)) THEN
        RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Customer payment event sequence is invalid.';
    END IF;
    SELECT coalesce(sum(CASE WHEN operation=6 THEN amount WHEN operation=7 THEN -amount ELSE 0 END),0) INTO allocated
        FROM invoices.customer_payment_events WHERE tenant_id=NEW.tenant_id AND payment_id=NEW.payment_id;
    IF NEW.operation=1 THEN
        IF previous.event_id IS NOT NULL OR NEW.actor_account_id<>payment.recorded_by_account_id OR NEW.recorded_at<>payment.recorded_at THEN
            RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Customer receipt must start pending.';
        END IF;
        expected_state:=1;
    ELSIF NEW.operation=2 AND previous.state IN(1,4) AND NEW.actor_account_id<>payment.recorded_by_account_id THEN
        SELECT evidence INTO last_evidence FROM invoices.customer_payment_events
            WHERE tenant_id=NEW.tenant_id AND payment_id=NEW.payment_id AND operation=2 ORDER BY revision DESC LIMIT 1;
        IF NEW.evidence IS NOT DISTINCT FROM last_evidence THEN
            RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Confirmation requires new received-funds evidence.';
        END IF;
        expected_state:=2;
    ELSIF NEW.operation=3 AND previous.state=1 THEN expected_state:=3;
    ELSIF NEW.operation=4 AND previous.state IN(1,2) THEN expected_state:=4;
    ELSIF NEW.operation=5 AND previous.state IN(2,4) AND allocated=0 THEN expected_state:=5;
    ELSIF NEW.operation IN(6,7) THEN
        PERFORM pg_advisory_xact_lock(hashtextextended('customer-receivable:'||NEW.tenant_id::text||':'||NEW.receivable_id::text,0));
        SELECT * INTO receivable FROM invoices.receivables WHERE tenant_id=NEW.tenant_id AND invoice_id=NEW.receivable_id;
        IF receivable.invoice_id IS NULL OR receivable.debtor_kind<>payment.debtor_kind OR receivable.debtor_id<>payment.debtor_id
           OR receivable.currency_code<>payment.currency_code THEN
            RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Allocation debtor identity must match.';
        END IF;
        IF NEW.operation=6 THEN
            SELECT receivable.original_amount-coalesce(sum(CASE WHEN operation=6 THEN amount WHEN operation=7 THEN -amount ELSE 0 END),0)
                INTO outstanding FROM invoices.customer_payment_events WHERE tenant_id=NEW.tenant_id AND receivable_id=NEW.receivable_id;
            IF previous.state<>2 OR NEW.amount>payment.amount-allocated OR NEW.amount>outstanding THEN
                RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Allocation exceeds available funds or outstanding debt.';
            END IF;
        ELSE
            SELECT * INTO allocation FROM invoices.customer_payment_events
                WHERE tenant_id=NEW.tenant_id AND payment_id=NEW.payment_id AND event_id=NEW.allocation_id AND operation=6;
            IF allocation.event_id IS NULL OR allocation.receivable_id<>NEW.receivable_id OR allocation.amount<>NEW.amount THEN
                RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Release must retain the entire original allocation.';
            END IF;
        END IF;
        expected_state:=previous.state;
    ELSE
        RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Customer payment transition is invalid.';
    END IF;
    IF NEW.state<>expected_state THEN
        RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Customer payment state must follow its immutable event.';
    END IF;
    RETURN NEW;
END;
$body$;
CREATE TRIGGER valid_customer_payment_event BEFORE INSERT ON invoices.customer_payment_events
FOR EACH ROW EXECUTE FUNCTION invoices.guard_customer_payment_event();

CREATE FUNCTION invoices.require_complete_customer_payment() RETURNS trigger LANGUAGE plpgsql AS $body$
DECLARE events bigint; receipts bigint; initial_events bigint;
BEGIN
    SELECT count(*),count(*) FILTER(WHERE operation=1 AND revision=1) INTO events,initial_events
        FROM invoices.customer_payment_events WHERE tenant_id=NEW.tenant_id AND payment_id=NEW.payment_id;
    SELECT count(*) INTO receipts FROM invoices.customer_financial_receipts r
        JOIN invoices.customer_payment_events e ON e.tenant_id=r.tenant_id AND e.payment_id=r.payment_id AND e.event_id=r.event_id
            AND e.actor_account_id=r.actor_account_id
        WHERE r.tenant_id=NEW.tenant_id AND r.payment_id=NEW.payment_id;
    IF initial_events<>1 OR events<>receipts THEN
        RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Customer payment, events and receipts must commit together.';
    END IF;
    RETURN NULL;
END;
$body$;

DO $security$
DECLARE table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY['receivables','customer_payments','customer_payment_events','customer_financial_receipts'] LOOP
        EXECUTE format('ALTER TABLE invoices.%I ENABLE ROW LEVEL SECURITY',table_name);
        EXECUTE format('ALTER TABLE invoices.%I FORCE ROW LEVEL SECURITY',table_name);
        EXECUTE format('CREATE POLICY tenant_financial ON invoices.%I USING(tenant_id::text=current_setting(''app.current_tenant'',true)) WITH CHECK(tenant_id::text=current_setting(''app.current_tenant'',true))',table_name);
        EXECUTE format('CREATE TRIGGER immutable_financial BEFORE UPDATE OR DELETE ON invoices.%I FOR EACH ROW EXECUTE FUNCTION invoices.reject_issued_mutation()',table_name);
        EXECUTE format('REVOKE ALL ON invoices.%I FROM PUBLIC',table_name);
        IF table_name<>'receivables' THEN
            EXECUTE format('CREATE CONSTRAINT TRIGGER complete_customer_payment AFTER INSERT ON invoices.%I DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION invoices.require_complete_customer_payment()',table_name);
        END IF;
    END LOOP;
END;
$security$;
REVOKE ALL ON FUNCTION invoices.require_recognition(),invoices.guard_customer_payment_event(),invoices.require_complete_customer_payment() FROM PUBLIC;

CREATE VIEW invoices.customer_payment_balances WITH(security_invoker=true) AS
SELECT p.tenant_id,p.payment_id,p.debtor_kind,p.debtor_id,p.currency_code,p.amount,p.channel,p.evidence,p.external_reference,
    p.recorded_by_account_id,p.recorded_at,coalesce(last_event.revision,0) AS revision,coalesce(last_event.state,0) AS state,
    history.allocated,confirmation.evidence AS last_confirmation_evidence,
    (p.schema_version=1 AND history.valid IS TRUE AND history.event_count=history.last_revision
        AND history.first_revision=1 AND history.event_count=receipts.receipt_count
        AND history.allocated>=0 AND history.allocated<=p.amount) AS valid_history
FROM invoices.customer_payments p
LEFT JOIN LATERAL (
    SELECT revision,state FROM invoices.customer_payment_events e WHERE e.tenant_id=p.tenant_id AND e.payment_id=p.payment_id
    ORDER BY revision DESC LIMIT 1
) last_event ON true
LEFT JOIN LATERAL (
    SELECT count(*) AS event_count,min(revision) AS first_revision,max(revision) AS last_revision,
        coalesce(sum(CASE WHEN operation=6 THEN amount WHEN operation=7 THEN -amount ELSE 0 END),0) AS allocated,
        bool_and(schema_version=1) AS valid
    FROM invoices.customer_payment_events e WHERE e.tenant_id=p.tenant_id AND e.payment_id=p.payment_id
) history ON true
LEFT JOIN LATERAL (
    SELECT count(*) AS receipt_count FROM invoices.customer_financial_receipts r
    JOIN invoices.customer_payment_events e ON e.tenant_id=r.tenant_id AND e.payment_id=r.payment_id AND e.event_id=r.event_id
        AND e.actor_account_id=r.actor_account_id AND r.schema_version=1
    WHERE r.tenant_id=p.tenant_id AND r.payment_id=p.payment_id
) receipts ON true
LEFT JOIN LATERAL (
    SELECT evidence FROM invoices.customer_payment_events e WHERE e.tenant_id=p.tenant_id AND e.payment_id=p.payment_id AND operation=2
    ORDER BY revision DESC LIMIT 1
) confirmation ON true;

CREATE VIEW invoices.receivable_balances WITH(security_invoker=true) AS
SELECT r.tenant_id,r.invoice_id,r.debtor_kind,r.debtor_id,r.currency_code,r.original_amount,r.invoice_date,r.recognized_at,
    history.allocated,
    (r.schema_version=1 AND i.schema_version=1 AND r.currency_code=i.currency_code AND r.original_amount=i.total
        AND r.invoice_date=i.business_date AND r.debtor_kind=(i.debtor->>'kind')::integer
        AND r.debtor_id=CASE (i.debtor->>'kind')::integer WHEN 1 THEN (i.debtor->>'organizationId')::uuid
            WHEN 2 THEN (i.debtor->>'programId')::uuid ELSE (i.debtor->>'individualId')::uuid END
        AND history.valid IS NOT FALSE AND history.allocated>=0 AND history.allocated<=r.original_amount
        AND NOT EXISTS(
            SELECT 1 FROM (SELECT DISTINCT payment_id FROM invoices.customer_payment_events e
                WHERE e.tenant_id=r.tenant_id AND e.receivable_id=r.invoice_id) affected
            JOIN invoices.customer_payment_balances p ON p.tenant_id=r.tenant_id AND p.payment_id=affected.payment_id
            WHERE p.valid_history IS DISTINCT FROM true
        )) AS valid_history
FROM invoices.receivables r
JOIN invoices.issued_invoices i USING(tenant_id,invoice_id)
LEFT JOIN LATERAL (
    SELECT coalesce(sum(CASE WHEN e.operation=6 THEN e.amount WHEN e.operation=7 THEN -e.amount ELSE 0 END),0) AS allocated,
        bool_and(e.schema_version=1) AS valid
    FROM invoices.customer_payment_events e
    WHERE e.tenant_id=r.tenant_id AND e.receivable_id=r.invoice_id
) history ON true;
REVOKE ALL ON invoices.customer_payment_balances,invoices.receivable_balances FROM PUBLIC;
