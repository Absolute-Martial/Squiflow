INSERT INTO orders.command_receipts
    (tenant_id, account_id, operation, idempotency_key, fingerprint, order_id, response_json, created_at)
VALUES
    (@tenant_id, @account_id, @operation, @idempotency_key, @fingerprint, @order_id, @response_json::jsonb, @created_at)
ON CONFLICT (tenant_id, account_id, operation, idempotency_key) DO NOTHING
RETURNING 1
