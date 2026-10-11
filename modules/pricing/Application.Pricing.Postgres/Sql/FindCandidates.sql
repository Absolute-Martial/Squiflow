WITH scopes(kind, id, applicable) AS (
    VALUES ('default', '00000000-0000-0000-0000-000000000000'::uuid, true),
           ('customer', @customer_id::uuid, @customer_id IS NOT NULL),
           ('program', @program_id::uuid, @program_id IS NOT NULL),
           ('organization', @organization_id::uuid, @organization_id IS NOT NULL),
           ('wholesale', '00000000-0000-0000-0000-000000000001'::uuid, @wholesale)
), categories(kind) AS (VALUES ('active'), ('expired'), ('future'))
SELECT p.tenant_id, p.revision_id, p.revision_number, p.item_id, p.unit_code, p.currency_code,
       p.scope_kind, p.scope_id, p.base_unit_price, p.valid_from, p.valid_to, p.state,
       p.created_by_account_id, p.created_at, p.published_at, p.retired_at, p.unit_id, p.price_id, p.unit_conversion_revision
FROM scopes s CROSS JOIN categories c
CROSS JOIN LATERAL (
    SELECT * FROM pricing.price_revisions
    WHERE tenant_id = @tenant_id AND item_id = @item_id AND unit_id = @unit_id
      AND unit_conversion_revision IS NOT DISTINCT FROM @conversion_revision
      AND currency_code = @currency_code AND state = 'published'
      AND s.applicable AND scope_kind = s.kind AND scope_id = s.id
      AND ((c.kind = 'active' AND valid_from <= @at AND (valid_to IS NULL OR @at < valid_to))
        OR (c.kind = 'expired' AND valid_to <= @at)
        OR (c.kind = 'future' AND valid_from > @at))
    ORDER BY valid_from DESC, revision_number DESC LIMIT 2
) p
ORDER BY p.scope_kind, p.scope_id, p.valid_from, p.revision_number;
