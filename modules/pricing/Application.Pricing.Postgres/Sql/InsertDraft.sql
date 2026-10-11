INSERT INTO pricing.price_revisions
    (tenant_id, revision_id, revision_number, item_id, unit_code, currency_code,
     scope_kind, scope_id, base_unit_price, valid_from, valid_to, state,
     created_by_account_id, created_at, unit_id, price_id, unit_conversion_revision)
VALUES
    (@tenant_id, @revision_id, @revision_number, @item_id, @unit_code, @currency_code,
     @scope_kind, @scope_id, @base_unit_price, @valid_from, @valid_to, 'draft',
     @account_id, @created_at, @unit_id, @price_id, @unit_conversion_revision);
