SELECT fingerprint, response_json, created_at
FROM pricing.command_receipts
WHERE tenant_id = @tenant_id AND account_id = @account_id
  AND operation = @operation AND idempotency_key = @idempotency_key;
