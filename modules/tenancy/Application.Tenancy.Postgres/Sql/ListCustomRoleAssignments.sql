SELECT tenant_id, role_id, account_id, availability, revision, assigned_at, removed_at
FROM tenancy.custom_role_assignments
WHERE tenant_id = @tenant_id AND role_id = @role_id
ORDER BY account_id;
