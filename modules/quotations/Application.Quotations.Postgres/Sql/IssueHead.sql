UPDATE quotations.heads SET number=@number,current_issued_id=@revision_id,last_issued_revision=@revision,draft=NULL,version=version+1
WHERE tenant_id=@tenant AND id=@id AND version=@expected;
