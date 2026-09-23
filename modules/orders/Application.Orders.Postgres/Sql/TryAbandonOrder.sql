UPDATE orders.order_drafts
SET state = 'abandoned',
    revision = revision + 1,
    abandoned_at = @abandoned_at,
    abandoned_by_account_id = @account_id
WHERE tenant_id = @tenant_id
  AND id = @order_id
  AND state = 'draft'
  AND revision = @expected_revision
RETURNING 1
