SELECT tenant_id, revision_id, revision_number, item_id, unit_code, currency_code,
       scope_kind, scope_id, base_unit_price, valid_from, valid_to, state,
       created_by_account_id, created_at, published_at, retired_at, unit_id, price_id, unit_conversion_revision
FROM pricing.price_revisions
WHERE tenant_id = @tenant_id AND revision_id = @revision_id
FOR UPDATE;
