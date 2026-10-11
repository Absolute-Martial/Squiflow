UPDATE orders.program_order_metadata SET external_reference=@external_reference WHERE tenant_id=@tenant_id AND order_id=@order_id;
