SELECT id FROM quotations.heads WHERE tenant_id=@tenant AND id=@id FOR UPDATE;
