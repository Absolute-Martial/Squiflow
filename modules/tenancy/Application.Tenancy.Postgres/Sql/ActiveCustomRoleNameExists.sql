SELECT EXISTS (
    SELECT 1 FROM tenancy.custom_roles
    WHERE tenant_id = @tenant_id AND availability = 2
      AND lower(name) = lower(@role_name)
      AND (@except_role_id IS NULL OR role_id <> @except_role_id)
);
