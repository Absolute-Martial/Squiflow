SELECT id, organization_id, display_name, created_at FROM customers.programs
WHERE tenant_id = @tenant_id AND id = @program_id
