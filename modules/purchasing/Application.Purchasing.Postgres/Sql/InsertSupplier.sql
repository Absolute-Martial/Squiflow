INSERT INTO purchasing.suppliers(tenant_id,supplier_id,revision,snapshot,created_at,updated_at)
VALUES(@tenant,@id,@revision,@snapshot::jsonb,@created,@updated);
