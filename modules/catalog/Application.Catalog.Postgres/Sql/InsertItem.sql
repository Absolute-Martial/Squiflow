INSERT INTO catalog.items
    (tenant_id, id, code, name, description, kind, status, base_unit_id, stock_mode,
     revision, created_by_account_id, created_at, availability)
VALUES
    (@tenant_id, @id, @code, @name, @description, @kind, 'active', @base_unit_id, @stock_mode,
     1, @account_id, @created_at, @availability);
