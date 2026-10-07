INSERT INTO quotations.receipts(tenant_id,account_id,operation,key,fingerprint,response)
VALUES(@tenant,@actor,@operation,@key,@fingerprint,@response::jsonb);
