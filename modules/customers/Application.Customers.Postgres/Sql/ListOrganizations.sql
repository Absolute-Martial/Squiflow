SELECT id, display_name, created_at FROM customers.organizations
WHERE tenant_id = @tenant_id
  AND (@after_at IS NULL OR created_at < @after_at
       OR (created_at = @after_at AND id < @after_id))
ORDER BY created_at DESC, id DESC LIMIT @limit
