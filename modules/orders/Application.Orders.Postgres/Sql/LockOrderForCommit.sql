SELECT id FROM orders.order_drafts WHERE tenant_id = @tenant_id AND id = @order_id FOR UPDATE;
