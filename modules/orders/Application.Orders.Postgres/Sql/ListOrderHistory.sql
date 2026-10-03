WITH history AS (
    SELECT tenant_id, order_id, account_id, operation, created_at, fingerprint, response_json,
           COALESCE(response_json -> 'payload' ->> 'revision', response_json ->> 'revision')::bigint AS revision
    FROM orders.command_receipts
    WHERE tenant_id = @tenant_id
      AND order_id = @order_id
      AND operation = ANY(@operations)
)
SELECT draft.revision AS current_revision,
       entry.tenant_id, entry.order_id, entry.account_id, entry.operation,
       entry.created_at, entry.fingerprint, entry.response_json::text AS response_json
FROM orders.order_drafts AS draft
LEFT JOIN LATERAL (
    SELECT * FROM history
    WHERE @before_revision IS NULL OR revision < @before_revision
    ORDER BY revision DESC
    LIMIT @limit
) AS entry ON TRUE
WHERE draft.tenant_id = @tenant_id AND draft.id = @order_id
ORDER BY entry.revision DESC
