SELECT count(*)
FROM tenancy.custom_role_assignments
WHERE tenant_id = @tenant_id AND role_id = @role_id AND availability = 1;
