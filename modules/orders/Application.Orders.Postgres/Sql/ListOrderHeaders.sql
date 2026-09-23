SELECT id, summary, currency_code, total, revision, created_at, state, abandoned_at
FROM orders.order_drafts
WHERE tenant_id = @tenant_id
  AND (
        @after_created_at IS NULL
        OR created_at < @after_created_at
        OR (created_at = @after_created_at AND id < @after_id)
      )
ORDER BY created_at DESC, id DESC
LIMIT @limit
