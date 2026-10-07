INSERT INTO quotations.receipts(tenant_id,account_id,operation,key,fingerprint,version,response)
VALUES(@tenant,@actor,@operation,@key,@fingerprint,@version,@response::jsonb);
