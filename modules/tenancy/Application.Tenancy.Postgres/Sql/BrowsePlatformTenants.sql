SELECT id, display_name, availability, revision, created_at, suspended_at
FROM tenancy.tenants
WHERE (@after_tenant_id IS NULL OR id > @after_tenant_id)
ORDER BY id
LIMIT @limit;
