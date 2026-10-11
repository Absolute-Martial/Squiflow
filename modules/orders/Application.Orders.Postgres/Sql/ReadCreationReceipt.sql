SELECT tenant_id,order_id,fingerprint,response_json,operation
FROM orders.command_receipts
WHERE tenant_id=@tenant_id AND order_id=@order_id
    AND operation IN ('create-order-draft','create-quotation-order');
