INSERT INTO catalog.command_receipts
    (tenant_id, account_id, operation, idempotency_key, fingerprint, response_json, created_at)
VALUES
    (@tenant_id, @account_id, @operation, @idempotency_key, @fingerprint,
     @response_json::jsonb, @created_at)
ON CONFLICT (tenant_id, account_id, operation, idempotency_key) DO NOTHING
RETURNING 1;
