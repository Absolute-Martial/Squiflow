SELECT EXISTS (
    SELECT 1 FROM tenancy.custom_role_assignments
    WHERE tenant_id = @tenant_id AND role_id = @role_id
      AND account_id = @account_id AND availability = 1
);
