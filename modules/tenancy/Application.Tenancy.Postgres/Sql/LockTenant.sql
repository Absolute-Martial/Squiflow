SELECT availability, revision
FROM tenancy.tenants
WHERE id = @tenant_id
FOR UPDATE;
