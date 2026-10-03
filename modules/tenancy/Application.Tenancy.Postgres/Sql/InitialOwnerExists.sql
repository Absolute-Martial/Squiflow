SELECT EXISTS (
    SELECT 1
    FROM tenancy.memberships
    WHERE tenant_id = @tenant_id AND is_initial_owner
);
