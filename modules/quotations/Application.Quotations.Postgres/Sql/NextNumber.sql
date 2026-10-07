INSERT INTO quotations.numbers(tenant_id,value) VALUES(@tenant,1)
ON CONFLICT(tenant_id) DO UPDATE SET value=quotations.numbers.value+1 RETURNING value;
