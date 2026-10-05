SELECT EXISTS (
    SELECT 1
    FROM tenancy.tenant_permission_grants
    WHERE tenant_id = @tenant_id
      AND account_id = @account_id
      AND permission_id = @permission_id
      AND is_active
);
