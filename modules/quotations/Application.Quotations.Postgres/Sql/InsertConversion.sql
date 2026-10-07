INSERT INTO quotations.conversions(tenant_id,quotation_id,issued_revision_id,order_id,facts)
VALUES(@tenant,@id,@revision_id,@order_id,@facts::jsonb);
