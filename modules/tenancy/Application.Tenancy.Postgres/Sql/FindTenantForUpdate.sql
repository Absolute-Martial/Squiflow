SELECT id, availability, revision, suspended_at
FROM tenancy.tenants
WHERE id = @tenant_id
FOR UPDATE;
