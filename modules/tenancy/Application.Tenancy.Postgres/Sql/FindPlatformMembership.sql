SELECT tenant_id, account_id, availability, revision, created_at, activated_at, suspended_at, removed_at, is_initial_owner
FROM tenancy.memberships
WHERE tenant_id = @tenant_id AND account_id = @account_id;
