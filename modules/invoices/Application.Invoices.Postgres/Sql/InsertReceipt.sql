INSERT INTO invoices.issue_receipts(tenant_id,actor_account_id,idempotency_key,fingerprint,invoice_id,response)
VALUES(@tenant,@actor,@key,@fingerprint,@invoice,@response::jsonb);
