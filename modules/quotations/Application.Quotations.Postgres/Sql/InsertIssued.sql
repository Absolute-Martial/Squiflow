INSERT INTO quotations.issued(tenant_id,quotation_id,id,revision,facts) VALUES(@tenant,@id,@revision_id,@revision,@facts::jsonb);
