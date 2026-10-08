SELECT EXISTS (
    SELECT 1 FROM orders.command_receipts
    WHERE tenant_id=@tenant_id AND order_id=@order_id
        AND operation IN ('create-order-draft','create-quotation-order')
        AND (response_json->>'schemaVersion'='6'
            OR COALESCE(response_json->'programPolicy', 'null'::jsonb) <> 'null'::jsonb
            OR COALESCE(response_json->'payload'->'programPolicy', 'null'::jsonb) <> 'null'::jsonb)
);
