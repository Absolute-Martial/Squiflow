SELECT id, display_name, availability, revision, created_at, suspended_at
FROM tenancy.tenants
WHERE id = @tenant_id;
