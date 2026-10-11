SELECT quotation_id,issued_revision_id,number,revision_number FROM orders.quotation_origins
WHERE tenant_id=@tenant_id AND order_id=@order_id;
