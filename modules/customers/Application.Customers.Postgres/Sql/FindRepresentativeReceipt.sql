SELECT fingerprint, representative_id AS id, organization_id, program_id, individual_id,
       availability, revision, created_by_account_id, created_at, changed_by_account_id, changed_at
FROM customers.representative_command_receipts
WHERE tenant_id = @tenant_id AND account_id = @account_id
  AND operation = @operation AND idempotency_key = @key
