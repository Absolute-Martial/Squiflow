INSERT INTO invoices.customer_financial_receipts(tenant_id,actor_account_id,idempotency_key,fingerprint,payment_id,event_id,schema_version)
VALUES(@tenant,@actor,@key,@fingerprint,@payment,@event,1);
