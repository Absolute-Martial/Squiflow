UPDATE purchasing.suppliers SET revision=@revision,snapshot=@snapshot::jsonb,updated_at=@updated
WHERE tenant_id=@tenant AND supplier_id=@id AND revision=@expected;
