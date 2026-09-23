INSERT INTO orders.order_draft_lines
    (tenant_id, order_id, position, description, quantity, unit_code, unit_price, line_total)
VALUES
    (@tenant_id, @order_id, @position, @description, @quantity, @unit_code, @unit_price, @line_total)
