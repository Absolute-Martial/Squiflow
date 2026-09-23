SELECT id, tenant_id, created_by_account_id, summary, currency_code, total, revision, created_at,
       state, abandoned_at, abandoned_by_account_id
FROM orders.order_drafts
WHERE tenant_id = @tenant_id AND id = @id
