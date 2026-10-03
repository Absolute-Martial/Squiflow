INSERT INTO tenancy.memberships
    (tenant_id, account_id, availability, created_at, suspended_at,
     revision, activated_at, removed_at, is_initial_owner)
VALUES
    (@tenant_id, @account_id, @availability, @created_at, NULL,
     1, @activated_at, NULL, @is_initial_owner);
