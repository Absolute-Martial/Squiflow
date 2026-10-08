SELECT revision_id
FROM pricing.price_revisions
WHERE tenant_id = @tenant_id
  AND item_id = @item_id
   AND unit_id = @unit_id
  AND currency_code = @currency_code
  AND scope_kind = @scope_kind
  AND scope_id = @scope_id
  AND state = 'published'
  AND revision_id <> @revision_id
  AND valid_from < COALESCE(@valid_to, 'infinity'::timestamptz)
   AND COALESCE(valid_to, 'infinity'::timestamptz) > @valid_from
LIMIT 1
FOR UPDATE;
