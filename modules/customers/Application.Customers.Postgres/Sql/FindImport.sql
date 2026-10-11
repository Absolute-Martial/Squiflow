SELECT import_id, account_id, idempotency_key, fingerprint, contract_version,
       manifest_hash, byte_length, row_count, created_by_account_id, created_at
FROM customers.imports
WHERE tenant_id = @tenant_id AND import_id = @import_id;
