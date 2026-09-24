DELETE FROM orders.order_draft_lines
WHERE tenant_id = @tenant_id AND order_id = @order_id
