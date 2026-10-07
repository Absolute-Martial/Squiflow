SELECT tenant_id, revision_id, revision_number, item_id, unit_code, currency_code,
       scope_kind, scope_id, base_unit_price, valid_from, valid_to, state,
       created_by_account_id, created_at, published_at, retired_at, unit_id, price_id, unit_conversion_revision
FROM pricing.price_revisions
WHERE tenant_id = @tenant_id AND item_id = @item_id AND unit_id = @unit_id
  AND currency_code = @currency_code
  AND (@after_revision IS NULL OR revision_number > @after_revision)
  AND ((scope_kind = 'default')
    OR (scope_kind = 'customer' AND scope_id = @customer_id)
    OR (scope_kind = 'program' AND scope_id = @program_id)
    OR (scope_kind = 'organization' AND scope_id = @organization_id)
    OR (scope_kind = 'wholesale' AND @wholesale))
ORDER BY revision_number
LIMIT @limit;
