SELECT p.valid_history,e.event_id,e.payment_id,e.revision,e.operation,e.state,e.actor_account_id,e.recorded_at,e.authorization_revision,
    e.evidence,e.receivable_id,e.amount,e.allocation_id,e.schema_version
FROM invoices.customer_payment_balances p LEFT JOIN LATERAL(
    SELECT * FROM invoices.customer_payment_events e WHERE e.tenant_id=p.tenant_id AND e.payment_id=p.payment_id AND e.revision>@after
    ORDER BY revision LIMIT 51
) e ON true WHERE p.tenant_id=@tenant AND p.payment_id=@payment ORDER BY e.revision;
