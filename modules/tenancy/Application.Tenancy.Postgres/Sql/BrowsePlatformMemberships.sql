SELECT tenant_id, account_id, availability, revision, created_at, activated_at, suspended_at, removed_at, is_initial_owner
FROM tenancy.memberships
WHERE tenant_id = @tenant_id
  AND (@after_account_id IS NULL OR account_id > @after_account_id)
ORDER BY account_id
LIMIT @limit;
