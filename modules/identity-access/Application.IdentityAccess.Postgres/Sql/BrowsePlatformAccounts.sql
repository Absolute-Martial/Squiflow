SELECT id, availability, created_at, disabled_at
FROM identity_access.accounts
WHERE (@after_account_id IS NULL OR id > @after_account_id)
ORDER BY id
LIMIT @limit;
