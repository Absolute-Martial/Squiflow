SELECT object_key, provider_scope, byte_length, sha256, content_type, state, retention,
       created_at, expires_at, failure_code
FROM customers.import_source_objects
WHERE tenant_id = @tenant_id AND object_key = @object_key
FOR UPDATE;
