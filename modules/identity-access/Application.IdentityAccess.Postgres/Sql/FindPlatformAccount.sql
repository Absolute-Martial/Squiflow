SELECT id, availability, created_at, disabled_at
FROM identity_access.accounts
WHERE id = @account_id;
