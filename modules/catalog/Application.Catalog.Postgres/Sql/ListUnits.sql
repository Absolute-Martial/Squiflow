SELECT id, code, name, precision, status, revision, created_by_account_id, created_at,
       retired_at, retired_by_account_id
FROM catalog.units
WHERE tenant_id = @tenant_id
  AND (@after_at IS NULL OR (created_at, id) < (@after_at, @after_id))
ORDER BY created_at DESC, id DESC
LIMIT @limit;
