SELECT fingerprint, individual_id AS id, display_name, email, phone, availability, revision,
       created_by_account_id, created_at, availability_changed_by_account_id, availability_changed_at
FROM customers.individual_command_receipts
WHERE tenant_id = @tenant_id AND account_id = @account_id
  AND operation = @operation AND idempotency_key = @key
