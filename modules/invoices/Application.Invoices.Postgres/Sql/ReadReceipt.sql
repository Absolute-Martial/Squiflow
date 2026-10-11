SELECT fingerprint,invoice_id,response::text FROM invoices.issue_receipts
WHERE tenant_id=@tenant AND actor_account_id=@actor AND idempotency_key=@key;
