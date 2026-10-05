SELECT EXISTS (
    SELECT 1 FROM tenancy.memberships
    WHERE tenant_id = @tenant_id AND account_id = @account_id
      AND availability = 1 AND is_initial_owner
);
