INSERT INTO purchasing.purchases(tenant_id,purchase_id,revision,snapshot,created_at,updated_at,supplier_id,source)
VALUES(@tenant,@id,@revision,@snapshot::jsonb,@created,@updated,@supplier,@source::jsonb);
