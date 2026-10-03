SELECT id, display_name, availability
FROM tenancy.tenants
WHERE id = @tenant_id
