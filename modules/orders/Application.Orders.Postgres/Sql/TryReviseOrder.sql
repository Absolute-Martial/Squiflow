UPDATE orders.order_drafts
SET summary = @summary,
    currency_code = @currency_code,
    total = @total,
    customer_organization_id = @customer_organization_id,
    customer_program_id = @customer_program_id,
    revision = revision + 1
WHERE tenant_id = @tenant_id
  AND id = @order_id
  AND state = 'draft'
  AND revision = @expected_revision
  AND NOT EXISTS (SELECT 1 FROM orders.quotation_origins o WHERE o.tenant_id=@tenant_id AND o.order_id=@order_id)
RETURNING 1
