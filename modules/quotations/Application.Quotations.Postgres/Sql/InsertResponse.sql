INSERT INTO quotations.responses(tenant_id,quotation_id,issued_revision_id,kind,facts)
VALUES(@tenant,@id,@revision_id,@kind,@facts::jsonb);
