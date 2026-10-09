SELECT tenant_id,order_id,account_id,fingerprint,response_json::text,operation
FROM orders.command_receipts
WHERE tenant_id=@tenant_id AND order_id=@order_id AND
    COALESCE(response_json->'payload'->>'revision',response_json->>'revision')::bigint=@revision
    AND operation IN ('create-order-draft','create-quotation-order','revise-order-draft','set-order-program-reference')
LIMIT 2;
