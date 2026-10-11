SELECT id, code, name, description, kind, status, base_unit_id, stock_mode, revision,
       created_by_account_id, created_at, retired_at, retired_by_account_id,
       availability, availability_changed_at, availability_changed_by_account_id
FROM catalog.items
WHERE tenant_id = @tenant_id AND id = @item_id;
