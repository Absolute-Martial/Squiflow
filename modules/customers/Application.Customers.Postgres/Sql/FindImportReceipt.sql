SELECT import_id, fingerprint
FROM customers.imports
WHERE tenant_id = @tenant_id AND account_id = @account_id AND idempotency_key = @key;
