INSERT INTO customers.individual_command_receipts
    (tenant_id, account_id, operation, idempotency_key, fingerprint, individual_id,
     display_name, email, phone, availability, revision, created_by_account_id, created_at,
     availability_changed_by_account_id, availability_changed_at)
VALUES (@tenant_id, @account_id, @operation, @key, @fingerprint, @individual_id,
        @display_name, @email, @phone, @availability, @revision, @created_by_account_id, @created_at,
        @availability_changed_by_account_id, @availability_changed_at)
ON CONFLICT (tenant_id, account_id, operation, idempotency_key) DO NOTHING
RETURNING 1
