-- Only retained pre-profile create receipts qualify for grandfathering; missing or unknown evidence fails closed.
SELECT count(*)=1 AND coalesce(bool_and(
    CASE WHEN response_json::jsonb ? 'schemaVersion' THEN
        ((operation='create-order-draft' AND response_json::jsonb->>'schemaVersion' IN ('1','2','4'))
            OR (operation='create-quotation-order' AND response_json::jsonb->>'schemaVersion'='5'))
        AND response_json::jsonb->>'operation'=operation
        AND jsonb_typeof(response_json::jsonb->'payload')='object'
        AND NOT (response_json::jsonb->'payload' ? 'programPolicy')
        AND NOT (response_json::jsonb->'payload' ? 'externalProgramReference')
    ELSE NOT (response_json::jsonb ?| ARRAY['operation','resultType','payload','programPolicy','externalProgramReference'])
        AND jsonb_typeof(response_json::jsonb)='object'
    END),false)
FROM orders.command_receipts
WHERE tenant_id=@tenant_id AND order_id=@order_id AND operation IN ('create-order-draft','create-quotation-order');
