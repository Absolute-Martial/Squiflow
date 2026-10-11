UPDATE purchasing.purchases SET revision=@revision,snapshot=@snapshot::jsonb,updated_at=@updated
WHERE tenant_id=@tenant AND purchase_id=@id AND revision=@expected;
