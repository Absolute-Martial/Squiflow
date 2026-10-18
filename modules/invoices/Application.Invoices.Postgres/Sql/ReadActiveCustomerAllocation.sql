SELECT e.event_id,e.payment_id,e.revision,e.operation,e.state,e.actor_account_id,e.recorded_at,e.authorization_revision,
    e.evidence,e.receivable_id,e.amount,e.allocation_id,e.schema_version
FROM invoices.customer_payment_events e
WHERE e.tenant_id=@tenant AND e.payment_id=@payment AND e.event_id=@allocation AND e.operation=6
    AND NOT EXISTS(SELECT 1 FROM invoices.customer_payment_events r WHERE r.tenant_id=e.tenant_id AND r.allocation_id=e.event_id AND r.operation=7);
