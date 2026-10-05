SELECT count(*)
FROM tenancy.custom_roles
WHERE tenant_id = @tenant_id AND availability = 2;
