UPDATE catalog.items
SET availability = @availability, revision = revision + 1,
    availability_changed_at = @changed_at, availability_changed_by_account_id = @account_id
WHERE tenant_id = @tenant_id AND id = @item_id AND revision = @expected_revision
  AND status = 'active' AND stock_mode = 'availability_only' AND availability <> @availability
RETURNING id;
