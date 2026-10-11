INSERT INTO customers.representative_command_receipts
    (tenant_id, account_id, operation, idempotency_key, fingerprint, representative_id,
     organization_id, program_id, individual_id, availability, revision,
     created_by_account_id, created_at, changed_by_account_id, changed_at)
VALUES (@tenant_id, @account_id, @operation, @key, @fingerprint, @representative_id,
        @organization_id, @program_id, @individual_id, @availability, @revision,
        @created_by_account_id, @created_at, @changed_by_account_id, @changed_at)
ON CONFLICT (tenant_id, account_id, operation, idempotency_key) DO NOTHING
RETURNING 1
