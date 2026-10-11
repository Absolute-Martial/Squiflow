SELECT fingerprint, resolution_id, customer_id, other_customer_id, outcome, resolved_at, reason, evidence
FROM customers.duplicate_command_receipts
WHERE tenant_id = @tenant_id AND account_id = @account_id
  AND operation = @operation AND idempotency_key = @key;
