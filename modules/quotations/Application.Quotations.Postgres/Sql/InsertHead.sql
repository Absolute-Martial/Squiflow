INSERT INTO quotations.heads(tenant_id,id,created_by,created_at,version,draft)
VALUES(@tenant,@id,@actor,@at,1,@draft::jsonb);
