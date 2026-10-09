INSERT INTO purchasing.supplier_command_receipts(tenant_id,actor_id,operation,idempotency_key,fingerprint,supplier_id,revision,response)
VALUES(@tenant,@actor,@operation,@key,@fingerprint,@id,@revision,@response::jsonb);
