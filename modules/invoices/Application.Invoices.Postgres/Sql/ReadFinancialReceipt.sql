SELECT r.fingerprint,r.payment_id,r.event_id,e.revision,
    (r.schema_version=1 AND e.schema_version=1 AND e.actor_account_id=r.actor_account_id AND p.valid_history) AS valid
FROM invoices.customer_financial_receipts r
LEFT JOIN invoices.customer_payment_events e ON e.tenant_id=r.tenant_id AND e.payment_id=r.payment_id AND e.event_id=r.event_id
LEFT JOIN invoices.customer_payment_balances p ON p.tenant_id=r.tenant_id AND p.payment_id=r.payment_id
WHERE r.tenant_id=@tenant AND r.actor_account_id=@actor AND r.idempotency_key=@key;
