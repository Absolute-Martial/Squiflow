INSERT INTO purchasing.purchase_command_receipts(tenant_id,actor_id,operation,idempotency_key,fingerprint,purchase_id,revision,response)
VALUES(@tenant,@actor,@operation,@key,@fingerprint,@id,@revision,@response::jsonb);
