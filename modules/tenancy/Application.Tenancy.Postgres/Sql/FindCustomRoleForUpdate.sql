SELECT tenant_id, role_id, name, availability, revision, permission_ids,
       created_at, updated_at, retired_at
FROM tenancy.custom_roles
WHERE tenant_id = @tenant_id AND role_id = @role_id
FOR UPDATE;
