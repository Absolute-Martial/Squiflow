UPDATE quotations.heads SET version=version+1 WHERE tenant_id=@tenant AND id=@id AND version=@expected;
