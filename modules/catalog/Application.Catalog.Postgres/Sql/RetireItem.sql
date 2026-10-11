UPDATE catalog.items
SET status = 'retired', revision = revision + 1,
    retired_at = @retired_at, retired_by_account_id = @account_id
WHERE tenant_id = @tenant_id AND id = @item_id
  AND status = 'active' AND revision = @expected_revision
RETURNING id;
