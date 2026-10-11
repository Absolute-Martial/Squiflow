INSERT INTO customers.imports
    (tenant_id, import_id, account_id, idempotency_key, fingerprint, created_by_account_id,
     contract_version, manifest_hash, byte_length, row_count, created_at)
VALUES (@tenant_id, @import_id, @account_id, @key, @fingerprint, @account_id,
        @contract_version, @manifest_hash, @byte_length, @row_count, @created_at)
ON CONFLICT DO NOTHING;
