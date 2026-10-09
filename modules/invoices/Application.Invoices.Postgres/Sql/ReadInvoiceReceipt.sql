SELECT actor_account_id,idempotency_key,fingerprint,response::text FROM invoices.issue_receipts
WHERE tenant_id=@tenant AND invoice_id=@invoice;
