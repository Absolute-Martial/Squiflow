SELECT tenant_id, order_id, fingerprint, response_json::text AS response_json
FROM orders.command_receipts
WHERE tenant_id = @tenant_id
  AND account_id = @account_id
  AND operation = @operation
  AND idempotency_key = @idempotency_key
