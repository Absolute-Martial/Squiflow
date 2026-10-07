SELECT maximum_bytes, reserved_bytes, retained_bytes
FROM customers.object_storage_usage
WHERE tenant_id = @tenant_id AND provider_scope = @provider_scope
FOR UPDATE;
