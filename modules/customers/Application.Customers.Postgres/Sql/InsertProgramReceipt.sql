INSERT INTO customers.program_receipts
    (tenant_id, account_id, idempotency_key, fingerprint, program_id, created_at)
VALUES (@tenant_id, @account_id, @key, @fingerprint, @program_id, @created_at)
ON CONFLICT (tenant_id, account_id, idempotency_key) DO NOTHING
RETURNING 1
