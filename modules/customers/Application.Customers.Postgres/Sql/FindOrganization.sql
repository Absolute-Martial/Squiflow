SELECT id, display_name, created_at FROM customers.organizations
WHERE tenant_id = @tenant_id AND id = @organization_id
