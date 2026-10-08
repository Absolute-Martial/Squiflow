SELECT state, retention, byte_length, sha256, provider_scope, created_at, expires_at, failure_code
FROM customers.import_source_objects
WHERE tenant_id = @tenant_id AND object_key = @object_key
FOR UPDATE;
