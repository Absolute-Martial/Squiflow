SELECT position, description, quantity, unit_code, unit_price, line_total, commercial_facts::text
FROM orders.order_draft_lines
WHERE tenant_id = @tenant_id AND order_id = @order_id
ORDER BY position
