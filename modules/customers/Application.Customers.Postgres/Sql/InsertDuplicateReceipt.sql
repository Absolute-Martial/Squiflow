INSERT INTO customers.duplicate_command_receipts
    (tenant_id, account_id, operation, idempotency_key, fingerprint, resolution_id,
     customer_id, other_customer_id, outcome, resolved_at, reason, evidence)
VALUES (@tenant_id, @account_id, @operation, @key, @fingerprint, @resolution_id,
        @customer_id, @other_customer_id, @outcome, @resolved_at, @reason, @evidence)
ON CONFLICT (tenant_id, account_id, operation, idempotency_key) DO NOTHING
RETURNING 1;
